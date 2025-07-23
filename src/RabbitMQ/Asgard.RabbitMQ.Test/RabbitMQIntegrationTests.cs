using System.Text;
using System.Threading.Channels;
using Asgard.Abstraction.Events;
using Asgard.Abstraction.Messaging.Dispatchers;
using Asgard.Abstraction.Messaging.Handlers;
using Asgard.Abstraction.Models;
using Asgard.RabbitMQ.Configuration;
using Asgard.RabbitMQ.Messaging;
using Asgard.RabbitMQ.Topology;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Testcontainers.RabbitMq;
using RabbitMQ.Client;
using System.Diagnostics;

namespace Asgard.RabbitMQ.Test;

public sealed class RabbitMqIntegrationTests : IAsyncLifetime
{
    private readonly RabbitMqContainer _container;

    public RabbitMqIntegrationTests()
    {
        _container = new RabbitMqBuilder()
            .WithImage("rabbitmq:3.12-management")
            .WithUsername("guest")
            .WithPassword("guest")
            .WithPortBinding(5672, 5672)
            .WithPortBinding(15672, 15672)
            .WithName("Pippo")
            .Build();
    }

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    [Fact]
    public async Task Should_Publish_And_Consume_A_Message()
    {
        var received = Channel.CreateUnbounded<TestPayload>();

        using var host = Host.CreateDefaultBuilder()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Debug);
            })
            .ConfigureServices(services =>
            {
                // Registrazione di ICloudEventTypeMapper con mappatura per TestPayload
                services.AddSingleton<ICloudEventHandler<TestPayload>>(new TestPayloadHandler(received.Writer));

                // Configurazione RabbitMQ
                var rabbitConfig = new RabbitMQConfiguration
                {
                    Exchange = "test.exchange",
                    ExchangeType = "fanout",
                    Queue = "test.queue",
                    Bindings =
                    [
                        new()
                        {
                            Key = null!,
                            RoutingKey = null,
                            Retry = null
                        }
                    ]
                };

                services.AddSingleton(rabbitConfig);

                services.AddRabbitMQ(
                    configureOptions: opt =>
                    {
                        opt.HostName = _container.Hostname;
                        opt.Port = _container.GetMappedPublicPort(5672);
                        opt.ClientName = "asgard-test";
                    },
                    configurations: [rabbitConfig]
                );

                services.AddSingleton<ICloudEventTypeMapper>(sp =>
                {
                    var mapper = new CloudEventTypeMapper();
                    mapper.Register<TestPayload>("asgard.test");
                    return mapper;
                });
            })
            .Build();

        var topologyInitializer = host.Services.GetServices<IHostedService>()
            .OfType<RabbitMQTopologyInitializer>()
            .FirstOrDefault();

        if (topologyInitializer != null)
            await topologyInitializer.StartAsync(default);

        await host.StartAsync();

        var publisher = host.Services.GetRequiredService<IEventPublisher>();


        var payload = new TestPayload("hello-world");
        var cloudEvent = CloudEvent.Create(payload, "asgard.test", "test-source");

        await publisher.PublishAsync(cloudEvent, cancellationToken: default);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var result = await received.Reader.ReadAsync(cts.Token);

        result.Should().NotBeNull();
        result.Value.Should().Be("hello-world");

        await host.StopAsync();
    }

    [Fact]
    public async Task Event_Should_Be_Retried_Then_Sent_To_Dlq()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        await _container.StartAsync(cts.Token);

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5672);

        var rabbitHost = RabbitMQTestHostFactory.Build(host, port);

        var topologyInitializer = rabbitHost.Services.GetServices<IHostedService>()
            .OfType<RabbitMQTopologyInitializer>()
            .FirstOrDefault();

        if (topologyInitializer is not null)
            await topologyInitializer.StartAsync(cts.Token);

        await rabbitHost.StartAsync(cts.Token);

        // Invio evento che fallisce e va in DLQ
        var payload = new TestPayload("failing-payload");

        var cloudEvent = CloudEvent.Create(payload, "asgard.test", "test-source");

        var publisher = rabbitHost.Services.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(cloudEvent, bindingKey: "failing", cancellationToken: cts.Token);

        // Verifica che il messaggio sia arrivato nella DLQ
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var message = await WaitForMessageInQueueAsync(
            channel,
            queueName: "queue.asgard-dead",
            expectedContent: "failing-payload",
            timeout: TimeSpan.FromSeconds(250),
            cancellationToken: cts.Token);

        message.Should().NotBeNull("Message should end up in DLQ after retries");
        message.Should().Contain("failing-payload");
    }

    [Fact]
    public async Task Event_Should_Be_Retried_Twice_Before_Dlq()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        await _container.StartAsync(cts.Token);

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5672);

        var rabbitHost = RabbitMQTestHostFactory.Build(host, port);

        var topologyInitializer = rabbitHost.Services.GetServices<IHostedService>()
            .OfType<RabbitMQTopologyInitializer>()
            .FirstOrDefault();

        if (topologyInitializer is not null)
            await topologyInitializer.StartAsync(cts.Token);

        await rabbitHost.StartAsync(cts.Token);

        var publisher = rabbitHost.Services.GetRequiredService<IEventPublisher>();

        var payload = new TestPayload("failing-payload");
        var cloudEvent = CloudEvent.Create(payload, "asgard.test", "test-source");

        await publisher.PublishAsync(cloudEvent, bindingKey: "failing", cancellationToken: cts.Token);

        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var dlqMessage = await WaitForMessageInQueueAsync(
            channel,
            queueName: "queue.dead",
            expectedContent: "failing-payload",
            timeout: TimeSpan.FromSeconds(40),
            cancellationToken: cts.Token);

        dlqMessage.Should().NotBeNull("After max retries, the message should go to DLQ");

        var result = await channel.BasicGetAsync("queue.dead", true, cts.Token);
        result.Should().NotBeNull();

        var headers = result.BasicProperties?.Headers;
        headers.Should().NotBeNull("Message should have headers");

        if (headers!.TryGetValue("x-death", out var rawDeath))
        {
            var xDeath = rawDeath as IList<object>;
            var retryDeath = xDeath?.OfType<Dictionary<string, object>>()
                .FirstOrDefault(d =>
                    Encoding.UTF8.GetString((byte[])d["queue"]) == "queue.retry");

            var retryCount = retryDeath != null && retryDeath.TryGetValue("count", out var countObj)
                ? Convert.ToInt32(countObj)
                : -1;

            retryCount.Should().Be(2, "Expected message to be retried exactly twice before going to DLQ");
        }
        else
        {
            Assert.Fail("Missing x-death header");
        }

        await rabbitHost.StopAsync();
    }



    [Fact]
    public async Task Should_Respect_Retry_Delays()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await _container.StartAsync(cts.Token);

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5672);

        var rabbitHost = RabbitMQTestHostFactory.Build(host, port);

        var topologyInitializer = rabbitHost.Services.GetServices<IHostedService>()
            .OfType<RabbitMQTopologyInitializer>()
            .FirstOrDefault();

        if (topologyInitializer is not null)
            await topologyInitializer.StartAsync(cts.Token);

        await rabbitHost.StartAsync(cts.Token);

        // Preparazione messaggio che fallirà
        var payload = new TestPayload("delayed-retry");

        var cloudEvent = CloudEvent.Create(payload, "asgard.test", "test-source");

        var publisher = rabbitHost.Services.GetRequiredService<IEventPublisher>();

        var stopwatch = Stopwatch.StartNew();

        await publisher.PublishAsync(cloudEvent, bindingKey: "failing", cancellationToken: cts.Token);

        // Attendi arrivo messaggio in DLQ
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var message = await WaitForMessageInQueueAsync(
            channel,
            queueName: "queue.dead",
            expectedContent: "delayed-retry",
            timeout: TimeSpan.FromSeconds(25),
            cancellationToken: cts.Token);

        stopwatch.Stop();

        message.Should().NotBeNull("Message should end up in DLQ after retry delays");

        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

        elapsedSeconds.Should().BeGreaterThanOrEqualTo(6.0,
            "retry delays are 2s and 4s, so minimum expected time is 6s");

        message.Should().Contain("delayed-retry");
    }

    [Fact]
    public async Task Should_Handle_Event_Without_RoutingKey()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await _container.StartAsync(cts.Token);
        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5672);

        var config = new RabbitMQConfiguration
        {
            Exchange = "ex.no-routingkey",
            RetryExchange = "ex.no-routingkey-retry",
            DeadLetterExchange = "ex.no-routingkey-dead",
            Queue = "queue.no-routingkey",
            DeadLetterQueue = "queue.no-routingkey-dead",
            RetryQueue = "queue.no-routingkey-retry",
            Bindings =
            [
                new()
                {
                    Key = string.Empty,
                    Retry = new RetrySettings
                    {
                        MaxRetries = 1,
                        DelaysSeconds = [ 6 ]
                    }
                }
            ]
        };

        var received = Channel.CreateUnbounded<TestPayload>();

        using var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddRabbitMQ(
                    configureOptions: opt =>
                    {
                        opt.HostName = host;
                        opt.Port = port;
                        opt.ClientName = "test-noroutingkey";
                    },
                    configurations: [config]
                );

                services.AddSingleton<ICloudEventHandler<TestPayload>>(new TestPayloadHandler(received.Writer));
                services.AddSingleton<ICloudEventDispatcher, CloudEventDispatcher>();
                services.AddSingleton<ICloudEventTypeMapper>(sp =>
                {
                    var mapper = new CloudEventTypeMapper();
                    mapper.Register<TestPayload>("asgard.test");
                    return mapper;
                });
            })
            .Build();

        var topologyInitializer = hostBuilder.Services.GetServices<IHostedService>()
            .OfType<RabbitMQTopologyInitializer>()
            .FirstOrDefault();

        if (topologyInitializer is not null)
            await topologyInitializer.StartAsync(cts.Token);

        await hostBuilder.StartAsync(cts.Token);

        var publisher = hostBuilder.Services.GetRequiredService<IEventPublisher>();
        var payload = new TestPayload("no-routing-key");
        var cloudEvent = CloudEvent.Create(payload, "asgard.test", "test-source");

        await publisher.PublishAsync(cloudEvent, cancellationToken: cts.Token);

        var result = await received.Reader.ReadAsync(cts.Token);
        result.Should().NotBeNull();
        result.Value.Should().Be("no-routing-key");

        await hostBuilder.StopAsync();
    }

    [Fact]
    public async Task Should_Retry_And_Dlq_Without_RoutingKey()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30));

        await _container.StartAsync(cts.Token);
        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5672);

        var config = new RabbitMQConfiguration
        {
            Exchange = "ex.main",
            ExchangeArguments = new Dictionary<string, object?>
            {
                ["alternate-exchange"] = "ex.dead"
            },
            RetryExchange = "ex.retry",
            DeadLetterExchange = "ex.dead",
            Queue = "queue.main",
            QueueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "ex.dead",
                ["x-queue-type"] = "quorum"
            },
            RetryQueue = "queue.retry",
            RetryQueueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "ex.main",
                ["x-queue-type"] = "quorum"
            },
            DeadLetterQueue = "queue.dead",
            DeadLetterExchangeArguments = new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum"
            },
            Bindings =
            [
                new()
                {
                    Key = string.Empty,
                    Retry = new RetrySettings
                    {
                        MaxRetries = 2,
                        DelaysSeconds = [2, 2]
                    }
                }
            ]
        };

        using var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddRabbitMQ(
                    configureOptions: opt =>
                    {
                        opt.HostName = host;
                        opt.Port = port;
                        opt.ClientName = "test-noroutingkey";
                    },
                    configurations: [config]
                );

                services.AddSingleton<ICloudEventHandler<TestPayload>, FailingTestPayloadHandler>();
                services.AddSingleton<ICloudEventDispatcher, CloudEventDispatcher>();
                services.AddSingleton<ICloudEventTypeMapper>(sp =>
                {
                    var mapper = new CloudEventTypeMapper();
                    mapper.Register<TestPayload>("asgard.test");
                    return mapper;
                });
            })
            .Build();

        var topologyInitializer = hostBuilder.Services.GetServices<IHostedService>()
            .OfType<RabbitMQTopologyInitializer>()
            .FirstOrDefault();

        if (topologyInitializer is not null)
            await topologyInitializer.StartAsync(cts.Token);

        await hostBuilder.StartAsync(cts.Token);

        var publisher = hostBuilder.Services.GetRequiredService<IEventPublisher>();
        var payload = new TestPayload("fail-me");

        var cloudEvent = CloudEvent.Create(payload, "asgard.test", "test-source");

        await publisher.PublishAsync(cloudEvent, cancellationToken: cts.Token);

        // Polling attivo per verificare arrivo in DLQ
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var messasge = await WaitForMessageInQueueAsync(
            channel,
            queueName: "queue.dead",
            expectedContent: "fail-me",
            timeout: TimeSpan.FromSeconds(20),
            cancellationToken: cts.Token);

        messasge.Should().NotBeNull("Message should be dead-lettered after retries");
        messasge.Should().Contain("fail-me");

        await hostBuilder.StopAsync();
    }

    [Fact]
    public async Task Should_Handle_MultipleRoutingKeys_WithSharedRetryQueue()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));

        await _container.StartAsync(cts.Token);
        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5672);

        var config = new RabbitMQConfiguration
        {
            Exchange = "ex.main",
            ExchangeArguments = new Dictionary<string, object?>
            {
                ["alternate-exchange"] = "ex.dead"
            },
            RetryExchange = "ex.retry",
            DeadLetterExchange = "ex.dead",
            Queue = "queue.main",
            QueueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "ex.dead",
                ["x-queue-type"] = "quorum"
            },
            RetryQueue = "queue.retry",
            RetryQueueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = "ex.main",
                ["x-queue-type"] = "quorum"
            },
            DeadLetterQueue = "queue.dead",
            DeadLetterExchangeArguments = new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum"
            },
            Bindings =
            [
                new()
                {
                    Key = "alpha",
                    RoutingKey = "rk.alpha",
                    Retry = new RetrySettings
                    {

                        MaxRetries = 2,
                        DelaysSeconds = [2, 4]
                    }
                },
                new()
                {
                    Key = "beta",
                    RoutingKey = "rk.beta",
                    Retry = new RetrySettings
                    {
                        MaxRetries = 3,
                        DelaysSeconds = [3, 3, 5]
                    }
                }
            ]
        };

        using var hostBuilder = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddRabbitMQ(
                    configureOptions: opt =>
                    {
                        opt.HostName = host;
                        opt.Port = port;
                        opt.ClientName = "test-shared";
                    },
                    configurations: [config]
                );

                services.AddSingleton<ICloudEventHandler<TestPayload>, FailingTestPayloadHandler>();
                services.AddSingleton<ICloudEventDispatcher, CloudEventDispatcher>();
                services.AddSingleton<ICloudEventTypeMapper>(sp =>
                {
                    var mapper = new CloudEventTypeMapper();
                    mapper.Register<TestPayload>("asgard.test");
                    return mapper;
                });
            })
            .Build();

        var topologyInitializer = hostBuilder.Services.GetServices<IHostedService>()
            .OfType<RabbitMQTopologyInitializer>()
            .FirstOrDefault();

        if (topologyInitializer is not null)
            await topologyInitializer.StartAsync(cts.Token);

        await hostBuilder.StartAsync(cts.Token);

        var publisher = hostBuilder.Services.GetRequiredService<IEventPublisher>();

        var cloudEvent1 = CloudEvent.Create(new TestPayload("payload-alpha"), "asgard.test", "test-source");
        var cloudEvent2 = CloudEvent.Create(new TestPayload("payload-beta"), "asgard.test", "test-source");

        await publisher.PublishAsync(cloudEvent1, bindingKey: "alpha", cancellationToken: cts.Token);
        await publisher.PublishAsync(cloudEvent2, bindingKey: "beta", cancellationToken: cts.Token);


        await using var connection = await new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = "guest",
            Password = "guest"
        }.CreateConnectionAsync();

        await using var channel = await connection.CreateChannelAsync();

        var msgAlpha = await WaitForMessageInQueueAsync(
            channel,
            queueName: "queue.dead",
            expectedContent: "payload-alpha",
            timeout: TimeSpan.FromSeconds(20),
            cancellationToken: cts.Token);

        var msgBeta = await WaitForMessageInQueueAsync(
            channel,
            queueName: "queue.dead",
            expectedContent: "payload-beta",
            timeout: TimeSpan.FromSeconds(20),
            cancellationToken: cts.Token);

        msgAlpha.Should().NotBeNull("Expected 'payload-alpha' to end up in DLQ");
        msgBeta.Should().NotBeNull("Expected 'payload-beta' to end up in DLQ");

        await hostBuilder.StopAsync();
    }


    #region PRIVATE METHODS
    private static async Task<string?> WaitForMessageInQueueAsync(
    IChannel channel,
    string queueName,
    string expectedContent,
    TimeSpan timeout,
    CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(queueName, autoAck: true, cancellationToken: cancellationToken);
            if (result != null)
            {
                var messageBody = Encoding.UTF8.GetString(result.Body.ToArray());
                if (messageBody.Contains(expectedContent))
                    return messageBody;
            }

            await Task.Delay(1000, cancellationToken);
        }

        return null;
    }
    #endregion

}