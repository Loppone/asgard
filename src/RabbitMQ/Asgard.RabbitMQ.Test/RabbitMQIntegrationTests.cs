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

                //services.AddSingleton<IEventSubscriber, Asgard.RabbitMQ.Messaging.RabbitEventSubscriber>();
                //services.AddSingleton<IHostedService, TestRabbitEventSubscriberHostedService>();
                //services.AddSingleton<IRabbitMQRetryHandler, RabbitMQRetryHandler>();

                //// Registrazione di ICloudEventTypeMapper con mappatura per TestPayload
                //var typeMapper = new CloudEventTypeMapper();
                //typeMapper.Register<TestPayload>();
                //services.AddSingleton<ICloudEventTypeMapper>(typeMapper);

                //// Serializer per CloudEvent
                //services.AddSingleton<ICloudEventSerializer, CloudEventJsonSerializer>();

                //// Registrazione esplicita del dispatcher con service provider root
                //services.AddSingleton<ICloudEventDispatcher>(sp =>
                //    new CloudEventDispatcher(
                //        sp,
                //        sp.GetRequiredService<ICloudEventTypeMapper>(),
                //        sp.GetRequiredService<ICloudEventSerializer>()));

                // Configurazione RabbitMQ
                services.AddRabbitMQ(
                    configureOptions: opt =>
                    {
                        opt.HostName = _container.Hostname;
                        opt.Port = _container.GetMappedPublicPort(5672);
                        opt.Exchange = "test.exchange";
                        opt.ClientName = "asgard-test";
                    },
                    configurations:
                    [
                        new RabbitMQConfiguration
                        {
                            Queue = "test.queue",
                            Bindings =
                            [
                                new() {
                                    RoutingKey = "",
                                    Retry = null
                                }
                            ]
                        }
                    ]);

                // Registrazione esplicita dell'istanza RabbitMQConfiguration per il subscriber
                services.AddSingleton(sp =>
                {
                    var subscriptionOptions = sp.GetRequiredService<IOptions<RabbitMQSubscriptionOptions>>().Value;
                    return subscriptionOptions.Configurations.First();
                });

                // Registrazione dei tipi
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
}