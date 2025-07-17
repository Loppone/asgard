namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// Publisher che converte un CloudEvent in un messaggio RabbitMQ e lo pubblica sull'exchange configurato.
/// Supporta routing key opzionale.
/// </summary>
internal sealed class RabbitPublisher(
    IConnectionFactory connectionFactory,
    IOptions<RabbitMQOptions> options,
    IOptions<RabbitMQSubscriptionOptions> subscriptionOptions,
    ICloudEventTypeMapper typeMapper,
    ICloudEventSerializer serializer) : IEventPublisher
{
    private readonly RabbitMQConfiguration _config = subscriptionOptions.Value.Configurations
        .FirstOrDefault() ??
            throw new InvalidOperationException("No RabbitMQConfiguration was found. Please ensure at least one configuration is registered.");

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
            exchange: _config.Exchange,
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
    string? routingKey = null,
    CancellationToken cancellationToken = default)
    {
        var type = typeMapper.GetTypeName(typeof(TPayload))
            ?? throw new InvalidOperationException(
                $"Missing CloudEvent type mapping for {typeof(TPayload).FullName}");

        if (string.IsNullOrWhiteSpace(options.Source))
            throw new ArgumentException("The 'Source' field in CloudEventOptions must be provided.", nameof(options));

        var cloudEvent = CloudEvent.Create(payload, type, options.Source, options.ContentType);

        if (!string.IsNullOrWhiteSpace(routingKey))
        {
            var ext = new Dictionary<string, object>(cloudEvent.Extensions ?? new Dictionary<string, object>())
            {
                ["routingKey"] = routingKey
            };
            cloudEvent = cloudEvent with { Extensions = ext };
        }

        await PublishAsync(cloudEvent, cancellationToken);
    }
}
