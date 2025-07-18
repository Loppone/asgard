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
        // Recupera la routing key (vuota se null)
        var routingKey = args.RoutingKey ?? string.Empty;

        // Trova la configurazione di retry associata alla routing key
        var retryConfig = config.Bindings.FirstOrDefault(b => b.RoutingKey == routingKey)
                       ?? config.Bindings.FirstOrDefault(b => b.RoutingKey is null);

        if (retryConfig is null || retryConfig.Retry is null || string.IsNullOrWhiteSpace(config.RetryQueue))
        {
            // Nessuna configurazione valida, manda direttamente in DLQ
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        var retrySettings = retryConfig.Retry;

        // Recupera l'header x-death per sapere quante volte è stato ritentato
        var headers = args.BasicProperties?.Headers;
        var xDeath = headers != null && headers.TryGetValue("x-death", out var deathRaw)
            ? deathRaw as IList<object>
            : null;


        var retryCount = 0;

        if (xDeath is [var entryRaw] && entryRaw is Dictionary<string, object> entry)
        {
            if (entry.TryGetValue("queue", out var queueObj) &&
                Encoding.UTF8.GetString((byte[])queueObj) == config.RetryQueue &&
                entry.TryGetValue("count", out var countObj))
            {
                retryCount = Convert.ToInt32(countObj);
            }
        }

        if (retryCount >= retrySettings.MaxRetries)
        {
            // Superato numero massimo -> DLQ
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        // Ripubblica sul retry exchange usando la routing key della retry queue
        if (string.IsNullOrWhiteSpace(config.RetryExchange))
        {
            // Exchange non configurato, manda in DLQ
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken);
            return;
        }

        // Delay dinamico per questo retry (es. DelaysSeconds[retryCount])
        var delaySeconds = retrySettings.DelaysSeconds.ElementAtOrDefault(retryCount);

        if (delaySeconds <= 0)
        {
            // Se delay non previsto → manda in DLQ
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken);
            return;
        }

        var originalProps = args.BasicProperties;

        var props = new BasicProperties
        {
            ContentType = originalProps?.ContentType,
            DeliveryMode = originalProps?.DeliveryMode ?? DeliveryModes.Persistent,
            Headers = originalProps?.Headers,
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

        // Ack del messaggio originale
        //await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);

        await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken);

    }
}
