namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// Determina se un messaggio deve essere ritentato o inviato alla DLQ,
/// in base alla politica di retry configurata per la coda e la routing key.
/// </summary>
internal interface IRabbitMQRetryHandler
{
    Task HandleRetryAsync(
        IChannel channel,
        BasicDeliverEventArgs args,
        RabbitMQConfiguration config,
        CancellationToken cancellationToken);
}
