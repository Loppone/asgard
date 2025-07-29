using Asgard.RabbitMQ.Internal;
using RabbitMQClient = RabbitMQ.Client;

namespace Asgard.RabbitMQ;

public static class RabbitMQServiceCollectionExtensions
{
    /// <summary>
    /// Registra i componenti RabbitMQ con supporto a configurazione via appsettings.json o programmatica.
    /// Se entrambi sono forniti, la configurazione via codice ha precedenza.
    /// </summary>
    public static IServiceCollection AddRabbitMQ(
    this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<RabbitMQOptions>? configureOptions = null,
        IEnumerable<RabbitMQConfiguration>? configurations = null)
    {
        if (configureOptions is not null)
            services.Configure(configureOptions);
        else if (configuration is not null)
            services.Configure<RabbitMQOptions>(configuration.GetSection("RabbitMq"));

        RabbitMQConfiguration? rabbitConfig = null;

        if (configurations is not null)
        {
            var list = configurations.ToList();
            services.Configure<RabbitMQSubscriptionOptions>(opts => opts.Configurations = list);
            rabbitConfig = list.FirstOrDefault();
        }
        else if (configuration is not null)
        {
            var section = configuration.GetSection("RabbitMQSubscriptionOptions");
            var opts = section.Get<RabbitMQSubscriptionOptions>() ?? new();
            services.Configure<RabbitMQSubscriptionOptions>(section);
            rabbitConfig = opts.Configurations.FirstOrDefault();
        }

        services.AddSingleton<RabbitMQClient.IConnectionFactory>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
            return new RabbitMQClient.ConnectionFactory()
            {
                HostName = options.HostName,
                Port = options.Port,
                UserName = options.UserName ?? "guest",
                Password = options.Password ?? "guest",
                ClientProvidedName = options.ClientName
            };
        });

       // RegisterCoreServices(services, configuration!);

        services.AddSingleton<RabbitMQStartupSynchronizer>();
        services.AddSingleton<IHostedService, RabbitMQTopologyInitializer>();
        services.AddSingleton<IRabbitMQTopologyBuilder, RabbitMQTopologyBuilder>();
        services.AddSingleton<IEventPublisher, RabbitPublisher>();

        services.TryAddSingleton<ICloudEventSerializer, CloudEventJsonSerializer>();
        services.AddSingleton<IValidateOptions<RabbitMQSubscriptionOptions>, RabbitMQSubscriptionOptionsValidation>();
        services.AddSingleton<ICloudEventTypeMapper, CloudEventTypeMapper>();
        services.AddSingleton<IRabbitMQRetryHandler, RabbitMQRetryHandler>();

        services.AddSingleton<ICloudEventDispatcher>(sp =>
            new CloudEventDispatcher(
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ICloudEventTypeMapper>(),
                sp.GetRequiredService<ICloudEventSerializer>()));

        // Registrazione del subscriber SOLO se richiesto da configurazione
        if (rabbitConfig?.HasConsumerTopology() == true)
        {
            services.AddSingleton<IEventSubscriber, RabbitEventSubscriber>();
            services.AddHostedService<RabbitEventSubscriberHostedService>();
        }

        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services, IConfiguration configuration)
    {

    }
}