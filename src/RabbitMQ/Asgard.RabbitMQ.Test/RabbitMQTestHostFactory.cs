using Asgard.Abstraction.Messaging.Dispatchers;
using Asgard.Abstraction.Messaging.Handlers;
using Asgard.RabbitMQ.Configuration;
using Asgard.RabbitMQ.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asgard.RabbitMQ.Test;

/// <summary>
/// Factory per costruire un host completo per test di integrazione RabbitMQ.
/// </summary>
public static class RabbitMQTestHostFactory
{
    public static IHost Build(string host, int port)
    {
        var rabbitOptions = new RabbitMQOptions
        {
            HostName = host,
            Port = port,
            VirtualHost = "/",
            UserName = "guest",
            Password = "guest",
            UseSsl = false,
            ClientName = "asgard-client"
        };

        var rabbitConfig = new RabbitMQConfiguration
        {
            Exchange = "ex.asgard-test",
            RetryExchange = "ex.asgard-retry",
            DeadLetterExchange = "ex.asgard-dead",
            Queue = "queue.asgard-main",
            DeadLetterQueue = "queue.asgard-dead",
            Bindings =
            [
                new()
                {
                    RoutingKey = "rk.test",
                    RetryQueue = "queue.asgard-retry.rk.test",
                    Retry = new RetrySettings
                    {
                        MaxRetries = 2,
                        DelaysSeconds = [2, 4]
                    }
                }
            ]
        };

        return Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddRabbitMQ(
                    configureOptions: opts =>
                    {
                        opts.HostName = rabbitOptions.HostName;
                        opts.Port = rabbitOptions.Port;
                        opts.VirtualHost = rabbitOptions.VirtualHost;
                        opts.UserName = rabbitOptions.UserName;
                        opts.Password = rabbitOptions.Password;
                        opts.UseSsl = rabbitOptions.UseSsl;
                        opts.ClientName = rabbitOptions.ClientName;
                    },
                    configurations: [rabbitConfig]);

                services.AddSingleton<ICloudEventHandler<TestPayload>, FailingTestPayloadHandler>();
                services.AddSingleton<ICloudEventDispatcher, CloudEventDispatcher>();
                services.AddSingleton<ICloudEventTypeMapper>(sp =>
                {
                    var mapper = new CloudEventTypeMapper();
                    mapper.Register<TestPayload>("asgard.test");
                    return mapper;
                });
                services.AddLogging(builder => builder.AddConsole());
            })
            .Build();
    }
}
