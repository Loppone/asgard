namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// Publisher che converte un CloudEvent in un messaggio RabbitMQ e lo pubblica sull'exchange configurato.
/// Supporta routing key opzionale.
/// </summary>
internal sealed class RabbitPublisher(
    IConnectionFactory connectionFactory,
    IOptions<RabbitMQOptions> options,
    IOptions<RabbitMQSubscriptionOptions> subscriptionOptions,
    ICloudEventSerializer serializer) : IEventPublisher
{
    private readonly RabbitMQConfiguration _config = subscriptionOptions.Value.Configurations
        .FirstOrDefault() ??
            throw new InvalidOperationException("No RabbitMQConfiguration was found. Please ensure at least one configuration is registered.");

    public async Task PublishAsync(
        CloudEvent cloudEvent,
        string? publishKey = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await connectionFactory.CreateConnectionAsync(options.Value.ClientName, cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var body = serializer.Serialize(cloudEvent);

        var props = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
        };

        var routingKey = ResolveRoutingKey(publishKey);

        channel.BasicReturnAsync += async (_, args) =>
        {
            Console.WriteLine($"[RabbitMQ] Message not routed!");
            Console.WriteLine($"Exchange: {args.Exchange}");
            Console.WriteLine($"RoutingKey: {args.RoutingKey}");
            Console.WriteLine($"ReplyCode: {args.ReplyCode} - {args.ReplyText}");

            await Task.CompletedTask;
        };

        await channel.BasicPublishAsync(
            exchange: _config.Exchange,
            routingKey: routingKey!,
            mandatory: true,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken
        );
    }

    /// <summary>
    /// Risolve la routing key in base alla publishKey specificata.
    /// Se nulla, ritorna string.Empty (es. exchange fanout).
    /// </summary>
    private string? ResolveRoutingKey(string? publishKey)
    {
        if (publishKey is null)
            return null!;

        var publish = _config.Publish.FirstOrDefault(b => b.Key == publishKey)
            ?? throw new InvalidOperationException($"No binding found for key '{publishKey}'");

        return publish.RoutingKey ?? string.Empty;
    }
}
