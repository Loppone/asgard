namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// Publisher che converte un CloudEvent in un messaggio RabbitMQ e lo pubblica sull'exchange configurato.
/// Supporta routing key opzionale.
/// </summary>
internal sealed class RabbitPublisher(
    IConnectionFactory connectionFactory,
    IOptions<RabbitMQOptions> options,
    ICloudEventSerializer serializer) : IEventPublisher
{
    public async Task PublishAsync(CloudEvent cloudEvent, CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.CreateConnectionAsync(options.Value.ClientName, cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var body = serializer.Serialize(cloudEvent);

        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
        };

        // Routing key se presente nel CloudEvent.Extensions (o vuota se non c'è)
        var routingKey = cloudEvent.Extensions?.TryGetValue("routingKey", out var rk) == true 
            ? rk as string ?? string.Empty 
            : string.Empty;

        await channel.BasicPublishAsync(
            exchange: options.Value.Exchange,
            routingKey: routingKey!,
            mandatory: false,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken
        );
    }

    public async Task PublishAsync<TPayload>(
    TPayload payload,
    CloudEventOptions options,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Type))
            throw new ArgumentException("The 'Type' field in CloudEventOptions must be provided.", nameof(options));

        if (string.IsNullOrWhiteSpace(options.Source))
            throw new ArgumentException("The 'Source' field in CloudEventOptions must be provided.", nameof(options));

        var cloudEvent = CloudEvent.Create(payload, options.Type, options.Source, options.ContentType);

        if (!string.IsNullOrWhiteSpace(options.Subject))
            cloudEvent = cloudEvent with { Subject = options.Subject };

        await PublishAsync(cloudEvent, cancellationToken);
    }
}
