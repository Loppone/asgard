namespace Asgard.RabbitMQ.Topology;

/// <summary>
/// Servizio avviato a runtime che legge la configurazione e crea la topologia RabbitMQ.
/// </summary>
internal sealed class RabbitMQTopologyInitializer(
    IRabbitMQTopologyBuilder builder,
    IOptions<RabbitMQSubscriptionOptions> subscriptionOptions)
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

            var declaredRetryQueues = new HashSet<string>();

            foreach (var binding in conf.Bindings)
            {
                if (binding.RetryQueue is not null && binding.Retry is not null)
                {
                    if (declaredRetryQueues.Add(binding.RetryQueue))
                    {
                        var args = new Dictionary<string, object?>
                        {
                            ["x-dead-letter-exchange"] = conf.Exchange
                        };

                        await builder.DeclareQueueAsync(binding.RetryQueue, args);

                        if (!string.IsNullOrWhiteSpace(conf.RetryExchange))
                        {
                            await builder.DeclareExchangeAsync(conf.RetryExchange, conf.RetryExchangeType ?? "fanout");
                        }
                    }

                    // Binding della coda retry alla routing key specifica
                    await builder.BindQueueAsync(binding.RetryQueue, conf.RetryExchange!, binding.RoutingKey);
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
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
