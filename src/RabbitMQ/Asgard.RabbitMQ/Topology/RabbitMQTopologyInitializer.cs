namespace Asgard.RabbitMQ.Topology;

/// <summary>
/// Servizio avviato a runtime che legge la configurazione e crea la topologia RabbitMQ.
/// </summary>
internal sealed class RabbitMQTopologyInitializer(
    IRabbitMQTopologyBuilder builder,
    IOptions<RabbitMQOptions> options,
    IOptions<RabbitMQSubscriptionOptions> subscriptionOptions) 
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var opts = options.Value;
        var configs = subscriptionOptions.Value.Configurations;

        // Exchange principale
        await builder.DeclareExchangeAsync(opts.Exchange, opts.ExchangeType);

        foreach (var config in configs)
        {
            // Queue principale
            await builder.DeclareQueueAsync(config.Queue);

            foreach (var binding in config.Bindings)
            {
                // Binding queue principale su tutte le routing key
                await builder.BindQueueAsync(
                    config.Queue,
                    opts.Exchange,
                    binding.RoutingKey
                );

                // Retry queue(s)
                if (binding.RetryQueue is not null && binding.Retry is not null)
                {
                    foreach (var delay in binding.Retry.DelaysSeconds)
                    {
                        var retryQueue = $"{binding.RetryQueue}.{delay}s";

                        var args = new Dictionary<string, object?>
                        {
                            ["x-dead-letter-exchange"] = opts.Exchange,
                            ["x-dead-letter-routing-key"] = binding.RoutingKey ?? "",
                            ["x-message-ttl"] = delay * 1000
                        };

                        await builder.DeclareQueueAsync(retryQueue, args);

                        if (!string.IsNullOrWhiteSpace(config.RetryExchange))
                        {
                            await builder.DeclareExchangeAsync(config.RetryExchange, config.RetryExchangeType ?? "fanout");
                            await builder.BindQueueAsync(retryQueue, config.RetryExchange, binding.RoutingKey);
                        }
                    }
                }
            }

            // DLQ
            if (config.DeadLetterQueue is not null && config.DeadLetterExchange is not null)
            {
                await builder.DeclareExchangeAsync(config.DeadLetterExchange, config.DeadLetterExchangeType ?? "fanout");
                await builder.DeclareQueueAsync(config.DeadLetterQueue);
                await builder.BindQueueAsync(config.DeadLetterQueue, config.DeadLetterExchange);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
