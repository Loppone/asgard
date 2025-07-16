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
            Type = "asgard.test",
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
}