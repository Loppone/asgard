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


        foreach (var config in configs)
        {

            // Exchange principale
            await builder.DeclareExchangeAsync(config.Exchange, config.ExchangeType, config.ExchangeArguments);

            // Retry exchange
            if (!string.IsNullOrWhiteSpace(config.RetryExchange))
                await builder.DeclareExchangeAsync(config.RetryExchange, config.RetryExchangeType ?? "fanout", config.RetryExchangeArguments);

            // Dead letter exchange
            if (!string.IsNullOrWhiteSpace(config.DeadLetterExchange))
                await builder.DeclareExchangeAsync(config.DeadLetterExchange, config.DeadLetterExchangeType ?? "fanout", config.DeadLetterExchangeArguments);

            // Coda principale (verifico se esiste perchè se un servizio ha solo il publisher la coda non serve)
            if (!string.IsNullOrWhiteSpace(config.Queue))
                await builder.DeclareQueueAsync(config.Queue, config.QueueArguments);

            // Binding delle routing key principali per i subscribers
            foreach (var binding in config.Bindings)
            {
                var routingKey = binding.RoutingKey ?? string.Empty;
                await builder.BindQueueAsync(config.Queue, config.Exchange, routingKey);
            }

            if (!string.IsNullOrWhiteSpace(config.RetryQueue))
            {
                await builder.DeclareQueueAsync(config.RetryQueue, config.RetryQueueArguments);

                // RetryExchange se definito, altrimenti fallback su Exchange principale
                var retryExchange = config.RetryExchange ?? config.Exchange;

                foreach (var binding in config.Bindings)
                {
                    if (binding.Retry is null)
                        continue;

                    var routingKey = binding.RoutingKey ?? string.Empty;
                    await builder.BindQueueAsync(config.RetryQueue, retryExchange, routingKey);
                }
            }

            // Dead-letter queue 
            if (!string.IsNullOrWhiteSpace(config.DeadLetterQueue))
            {
                // Usa DLX se specificato, altrimenti l’exchange principale
                var deadLetterExchange = config.DeadLetterExchange ?? config.Exchange;

                await builder.DeclareQueueAsync(config.DeadLetterQueue, config.DeadLetterQueueArguments);
                await builder.BindQueueAsync(config.DeadLetterQueue, deadLetterExchange);
            }
        }

        // Segnala che la topologia è pronta
        synchronizer.SignalTopologyReady();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
