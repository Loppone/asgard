using System.Text;
using System;
using System.Threading.Channels;
using Asgard.Abstraction.Events;
using Asgard.Abstraction.Messaging.Consumers;
using Asgard.Abstraction.Messaging.Dispatchers;
using Asgard.Abstraction.Messaging.Handlers;
using Asgard.Abstraction.Messaging.Serialization;
using Asgard.Abstraction.Models;
using Asgard.RabbitMQ.Configuration;
using Asgard.RabbitMQ.Messaging;
using Asgard.RabbitMQ.Topology;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
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
            .WithPortBinding(5672, true)
            .WithPortBinding(15672, true)
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
                        RoutingKey = "",
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
        await publisher.PublishAsync(payload, new CloudEventOptions
        {
            Source = "test-suite"
        });

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var result = await received.Reader.ReadAsync(cts.Token);

        result.Should().NotBeNull();
        result.Value.Should().Be("hello-world");

        await host.StopAsync();
    }

    [Fact]
    public async Task Event_Should_Be_Retried_Then_Sent_To_Dlq()
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

        // Invio evento che fallisce e va in DLQ
        var payload = new TestPayload("failing-payload");

        var cloudEvent = CloudEvent.Create(payload, "asgard.test", "test-source")
            with
        {
            Extensions = new Dictionary<string, object>
            {
                ["routingKey"] = "rk.test"
            }
        };

        var publisher = rabbitHost.Services.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(cloudEvent, cts.Token);

        // Attesa per i due retry e DLQ
        await Task.Delay(TimeSpan.FromSeconds(20), cts.Token);

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

        var result = await channel.BasicGetAsync("queue.asgard-dead", autoAck: true);
        result.Should().NotBeNull("Message should end up in DLQ after retries");

        var messageBody = Encoding.UTF8.GetString(result.Body.ToArray());
        messageBody.Should().Contain("failing-payload");
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

        var cloudEvent = CloudEvent.Create(payload, "asgard.test", "test-source")
            with
        {
            Extensions = new Dictionary<string, object>
            {
                ["routingKey"] = "rk.test"
            }
        };

        var publisher = rabbitHost.Services.GetRequiredService<IEventPublisher>();

        var stopwatch = Stopwatch.StartNew();
        await publisher.PublishAsync(cloudEvent, cts.Token);

        // Attendi arrivo messaggio in DLQ
        var factory = new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = "guest",
            Password = "guest"
        };

        BasicGetResult? result = null;
        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Polling attivo per attendere la comparsa in DLQ
        for (var i = 0; i < 30; i++)
        {
            result = await channel.BasicGetAsync("queue.asgard-dead", autoAck: true);
            if (result is not null)
                break;

            await Task.Delay(7000, cts.Token);
        }

        stopwatch.Stop();

        result.Should().NotBeNull("Message should end up in DLQ after retry delays");

        var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;

        elapsedSeconds.Should().BeGreaterThanOrEqualTo(6.0,
            "retry delays are 2s and 4s, so minimum expected time is 6s");

        var body = Encoding.UTF8.GetString(result!.Body.ToArray());
        body.Should().Contain("delayed-retry");
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
            Bindings =
            [
                new()
            {
                RetryQueue = "queue.no-routingkey-retry",
                Retry = new RetrySettings
                {
                    MaxRetries = 1,
                    DelaysSeconds = [ 2 ]
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

        await publisher.PublishAsync(cloudEvent, cts.Token);

        var result = await received.Reader.ReadAsync(cts.Token);
        result.Should().NotBeNull();
        result.Value.Should().Be("no-routing-key");

        await hostBuilder.StopAsync();
    }

    [Fact]
    public async Task Should_Retry_And_Dlq_Without_RoutingKey()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

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
            Bindings =
            [
                new()
            {
                RetryQueue = "queue.no-routingkey-retry",
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

        await publisher.PublishAsync(cloudEvent, cts.Token);

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

        BasicGetResult? result = null;

        for (int i = 0; i < 20; i++)
        {
            result = await channel.BasicGetAsync("queue.no-routingkey-dead", autoAck: true);
            if (result is not null)
                break;

            await Task.Delay(1000, cts.Token);
        }

        result.Should().NotBeNull("Message should be dead-lettered after retries");
        var body = Encoding.UTF8.GetString(result!.Body.ToArray());
        body.Should().Contain("fail-me");

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
            Exchange = "ex.shared",
            RetryExchange = "ex.shared.retry",
            DeadLetterExchange = "ex.shared.dead",
            Queue = "queue.shared.main",
            DeadLetterQueue = "queue.shared.dead",
            Bindings =
            [
                new()
                {
                    RetryQueue = "queue.shared.retry",
                    RoutingKey = "rk.alpha",
                    Retry = new RetrySettings
                    {

                        MaxRetries = 2,
                        DelaysSeconds = [2, 4]
                    }
                },
                new()
                {
                    RetryQueue = "queue.shared.retry",
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

        var cloudEvent1 = CloudEvent.Create(new TestPayload("payload-alpha"), "asgard.test", "test-source") with
        {
            Extensions = new Dictionary<string, object> { ["routingKey"] = "rk.alpha" }
        };

        var cloudEvent2 = CloudEvent.Create(new TestPayload("payload-beta"), "asgard.test", "test-source") with
        {
            Extensions = new Dictionary<string, object> { ["routingKey"] = "rk.beta" }
        };

        await publisher.PublishAsync(cloudEvent1, cts.Token);
        await publisher.PublishAsync(cloudEvent2, cts.Token);

        await using var connection = await new ConnectionFactory
        {
            HostName = host,
            Port = port,
            UserName = "guest",
            Password = "guest"
        }.CreateConnectionAsync();

        await using var channel = await connection.CreateChannelAsync();

        var foundPayloads = new HashSet<string>();

        for (int i = 0; i < 20 && foundPayloads.Count < 2; i++)
        {
            var result = await channel.BasicGetAsync("queue.shared.dead", autoAck: true);

            if (result is not null)
            {
                var body = Encoding.UTF8.GetString(result.Body.ToArray());
                if (body.Contains("payload-alpha")) foundPayloads.Add("payload-alpha");
                if (body.Contains("payload-beta")) foundPayloads.Add("payload-beta");
            }

            await Task.Delay(1000, cts.Token);
        }

        foundPayloads.Should().Contain("payload-alpha");
        foundPayloads.Should().Contain("payload-beta");

        await hostBuilder.StopAsync();
    }

}