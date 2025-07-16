using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Asgard.Abstraction.Messaging.Dispatchers;
using Asgard.Abstraction.Messaging.Handlers;
using Asgard.RabbitMQ.Configuration;
using Asgard.RabbitMQ.Messaging;

namespace Asgard.RabbitMQ.Test;

/// <summary>
/// Factory per costruire un ServiceProvider completo per test di integrazione RabbitMQ.
/// </summary>
public static class RabbitMQTestServiceProviderFactory
{
    public static IServiceProvider Build(string host, int port)
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

        var services = new ServiceCollection();

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
        services.AddLogging(builder => builder.AddConsole());

        return services.BuildServiceProvider();
    }
}
