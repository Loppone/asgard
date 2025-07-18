using Asgard.RabbitMQ.Internal;

namespace Asgard.RabbitMQ.Topology;

/// <summary>
/// Servizio avviato a runtime che legge la configurazione e crea la topologia RabbitMQ.
/// </summary>
internal sealed class RabbitMQTopologyInitializer(
    IRabbitMQTopologyBuilder builder,
    IOptions<RabbitMQSubscriptionOptions> subscriptionOptions,
    RabbitMQStartupSynchronizer synchronizer)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var configs = subscriptionOptions.Value.Configurations;

        if (configs.Count == 0)
            throw new InvalidOperationException("No RabbitMQ configurations found.");

        var mainConfig = configs.First();

        // Exchange principale
        await builder.DeclareExchangeAsync(mainConfig.Exchange, mainConfig.ExchangeType);

        foreach (var conf in configs)
        {
            // Coda principale
            var mainQueueArgs = new Dictionary<string, object?>();

            if (!string.IsNullOrWhiteSpace(conf.DeadLetterExchange))
            {
                mainQueueArgs["x-dead-letter-exchange"] = conf.DeadLetterExchange;
                mainQueueArgs["x-dead-letter-routing-key"] = conf.Bindings.FirstOrDefault()?.RoutingKey ?? "";
            }
            await builder.DeclareQueueAsync(conf.Queue, mainQueueArgs);

            foreach (var binding in conf.Bindings)
            {
                // Binding queue principale su tutte le routing key
                await builder.BindQueueAsync(
                    conf.Queue,
                    mainConfig.Exchange,
                    binding.RoutingKey
                );

                // Coda retry (una sola, TTL nel messaggio)
                if (conf.RetryQueue is not null && binding.Retry is not null)
                {
                    var args = new Dictionary<string, object?>
                    {
                        ["x-dead-letter-exchange"] = mainConfig.Exchange
                    };

                    await builder.DeclareQueueAsync(conf.RetryQueue, args);

                    if (!string.IsNullOrWhiteSpace(conf.RetryExchange))
                    {
                        await builder.DeclareExchangeAsync(conf.RetryExchange, conf.RetryExchangeType ?? "fanout");
                        await builder.BindQueueAsync(conf.RetryQueue, conf.RetryExchange);
                    }
                }
            }

            // DLQ
            if (conf.DeadLetterQueue is not null && conf.DeadLetterExchange is not null)
            {
                await builder.DeclareExchangeAsync(conf.DeadLetterExchange, conf.DeadLetterExchangeType ?? "fanout");
                await builder.DeclareQueueAsync(conf.DeadLetterQueue);
                await builder.BindQueueAsync(conf.DeadLetterQueue, conf.DeadLetterExchange);
            }
        }

        // Segnala che la topologia è pronta
        synchronizer.SignalTopologyReady();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
