using System.Diagnostics.Metrics;
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
            await builder.DeclareExchangeAsync(config.Exchange, config.ExchangeType);

            // Retry exchange
            if (!string.IsNullOrWhiteSpace(config.RetryExchange))
                await builder.DeclareExchangeAsync(config.RetryExchange, config.RetryExchangeType ?? "fanout");

            // Dead letter exchange
            if (!string.IsNullOrWhiteSpace(config.DeadLetterExchange))
                await builder.DeclareExchangeAsync(config.DeadLetterExchange, config.DeadLetterExchangeType ?? "fanout");

            // Coda principale (con eventuale DLX)
            var mainQueueArgs = BuildMainQueueArguments(config.DeadLetterExchange, config.Exchange);
            await builder.DeclareQueueAsync(config.Queue, mainQueueArgs);

            // Binding delle routing key principali
            foreach (var binding in config.Bindings)
            {
                await builder.BindQueueAsync(config.Queue, config.Exchange, binding.RoutingKey);
            }

            // Se non c'è una coda di retry allora il binding non serve
            if (!string.IsNullOrWhiteSpace(config.RetryQueue))
            {
                var retryQueueArgs = BuildRetryQueueArguments(config.DeadLetterExchange, config.Exchange);

                await builder.DeclareQueueAsync(config.RetryQueue, retryQueueArgs);

                // RetryExchange se definito, altrimenti fallback su Exchange principale
                var retryExchange = config.RetryExchange ?? config.Exchange;

                foreach (var binding in config.Bindings)
                {
                    if (binding.Retry is null)
                        continue;

                    await builder.BindQueueAsync(config.RetryQueue, retryExchange, binding.RoutingKey);
                }
            }

            // Dead-letter queue 
            if (!string.IsNullOrWhiteSpace(config.DeadLetterQueue))
            {
                // Usa DLX se specificato, altrimenti l’exchange principale
                var deadLetterExchange = config.DeadLetterExchange ?? config.Exchange;

                await builder.DeclareQueueAsync(config.DeadLetterQueue);
                await builder.BindQueueAsync(config.DeadLetterQueue, deadLetterExchange);
            }
        }

        // Segnala che la topologia è pronta
        synchronizer.SignalTopologyReady();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // Helper: main queue args (senza routing key)
    private static Dictionary<string, object?> BuildMainQueueArguments(string? deadLetterExchange, string fallbackExchange)
    {
        var args = new Dictionary<string, object?>();

        var exchange = !string.IsNullOrWhiteSpace(deadLetterExchange)
            ? deadLetterExchange
            : fallbackExchange;

        args["x-dead-letter-exchange"] = exchange;

        return args;
    }

    // Helper: retry queue args (con DLX e routing key)
    private static Dictionary<string, object?> BuildRetryQueueArguments(string? deadLetterExchange, string fallbackExchange)
    {
        var exchange = !string.IsNullOrWhiteSpace(deadLetterExchange)
            ? deadLetterExchange
            : fallbackExchange;

        return new()
        {
            ["x-dead-letter-exchange"] = exchange
        };
    }
}
