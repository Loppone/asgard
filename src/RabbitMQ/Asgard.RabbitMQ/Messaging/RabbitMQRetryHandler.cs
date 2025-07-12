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
        var retryConfig = config.Bindings.FirstOrDefault(b => b.RoutingKey == routingKey);
        if (retryConfig is null || retryConfig.Retry is null || string.IsNullOrWhiteSpace(retryConfig.RetryQueue))
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

        var retryCount = xDeath?.Count ?? 0;
        if (retryCount >= retrySettings.MaxRetries)
        {
            // Superato numero massimo -> DLQ
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        // Ripubblica sul retry exchange usando la routing key della retry queue
        var originalProps = args.BasicProperties;

        var props = new BasicProperties
        {
            ContentType = originalProps?.ContentType,
            DeliveryMode = originalProps?.DeliveryMode ?? DeliveryModes.Persistent,
            Headers = originalProps?.Headers
        };


        if (string.IsNullOrWhiteSpace(config.RetryExchange))
        {
            // Configurazione mancante → DLQ
            await channel.BasicNackAsync(args.DeliveryTag, false, requeue: false, cancellationToken: cancellationToken);
            return;
        }

        await channel.BasicPublishAsync(
            exchange: config.RetryExchange,
            routingKey: retryConfig.RetryQueue,
            mandatory: false,
            basicProperties: props,
            body: args.Body,
            cancellationToken: cancellationToken
        );

        // Ack del messaggio originale
        await channel.BasicAckAsync(args.DeliveryTag, false, cancellationToken);
    }
}
