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
            services.Configure<RabbitMQOptions>(configuration.GetSection("RabbitMqOptions"));

        if (configurations is not null)
            services.Configure<RabbitMQSubscriptionOptions>(opts =>
            {
                opts.Configurations = [.. configurations];
            });
        else if (configuration is not null)
            services.Configure<RabbitMQSubscriptionOptions>(configuration.GetSection("RabbitMQConfiguration"));

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

        RegisterCoreServices(services);
        
        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        services.AddSingleton<IEventPublisher, RabbitPublisher>();

        services.AddSingleton<IEventSubscriber, RabbitEventSubscriber>();
        services.AddHostedService<RabbitEventSubscriberHostedService>();

        services.AddSingleton<IHostedService, RabbitMQTopologyInitializer>();
        services.AddSingleton<IRabbitMQTopologyBuilder, RabbitMQTopologyBuilder>();

        services.TryAddSingleton<ICloudEventSerializer, CloudEventJsonSerializer>();
        services.AddSingleton<IValidateOptions<RabbitMQSubscriptionOptions>, RabbitMQSubscriptionOptionsValidation>();
        services.AddSingleton<ICloudEventTypeMapper, CloudEventTypeMapper>();
        services.AddSingleton<IRabbitMQRetryHandler, RabbitMQRetryHandler>();

        services.AddSingleton<ICloudEventDispatcher>(sp =>
            new CloudEventDispatcher(
                sp,
                sp.GetRequiredService<ICloudEventTypeMapper>(),
                sp.GetRequiredService<ICloudEventSerializer>()));
    }
}