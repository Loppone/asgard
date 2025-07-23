using System.Text;

namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// Gestisce la logica di retry per i messaggi RabbitMQ che falliscono la gestione.
/// In base alla configurazione dei binding, decide se ripubblicare su una retry queue oppure
/// inviare direttamente in DLQ.
/// </summary>
internal sealed class RabbitMQRetryHandler : IRabbitMQRetryHandler
{
    public async Task HandleRetryAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        RabbitMQConfiguration config,
        CancellationToken cancellationToken)
    {
        var routingKey = args.RoutingKey ?? string.Empty;

        var retryConfig = config.Bindings.FirstOrDefault(b => b.RoutingKey == routingKey)
                       ?? config.Bindings.FirstOrDefault(b => b.RoutingKey is null);

        if (retryConfig is null || retryConfig.Retry is null || string.IsNullOrWhiteSpace(config.RetryQueue))
        {
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        var retrySettings = retryConfig.Retry;

        // Estrai x-death
        var headers = args.BasicProperties?.Headers;
        var xDeath = headers != null && headers.TryGetValue("x-death", out var deathRaw)
            ? deathRaw as IList<object>
            : null;

        var retryCount = 0;

        if (xDeath != null)
        {
            foreach (var entryRaw in xDeath.OfType<Dictionary<string, object>>())
            {
                var queueName = entryRaw.TryGetValue("queue", out var q)
                    ? Encoding.UTF8.GetString((byte[])q)
                    : null;

                var rk = entryRaw.TryGetValue("routing-keys", out var rkList) && rkList is IList<object> rks && rks.Count > 0
                    ? Encoding.UTF8.GetString((byte[])rks[0])
                    : null;

                if (queueName == config.RetryQueue && rk == routingKey &&
                    entryRaw.TryGetValue("count", out var countObj))
                {
                    retryCount = Convert.ToInt32(countObj);
                    break;
                }
            }
        }

        Console.WriteLine($"[RetryHandler] RetryCount: {retryCount}");

        if (retryCount >= retrySettings.MaxRetries)
        {
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(config.RetryExchange))
        {
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        var delaySeconds = retrySettings.DelaysSeconds.ElementAtOrDefault(retryCount);

        if (delaySeconds <= 0)
        {
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        // Copia gli header esistenti e aggiungi un ID unico per differenziare i retry
        var originalProps = args.BasicProperties;
        var newHeaders = originalProps?.Headers != null
            ? new Dictionary<string, object>(originalProps.Headers!)
            : new Dictionary<string, object>();

        newHeaders["x-retry-id"] = Guid.NewGuid().ToString();

        var props = new BasicProperties
        {
            ContentType = originalProps?.ContentType,
            DeliveryMode = originalProps?.DeliveryMode ?? DeliveryModes.Persistent,
            Headers = newHeaders!,
            Expiration = (delaySeconds * 1000).ToString()
        };

        await channel.BasicPublishAsync(
            exchange: config.RetryExchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: args.Body,
            cancellationToken: cancellationToken
        );

        await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
    }
}
