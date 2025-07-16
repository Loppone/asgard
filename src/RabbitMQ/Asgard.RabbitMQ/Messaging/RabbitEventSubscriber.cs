namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// Subscriber che riceve messaggi RabbitMQ serializzati come CloudEvent,
/// li deserializza e li instrada tramite <see cref="ICloudEventDispatcher"/>.
/// </summary>
internal sealed class RabbitEventSubscriber(
    IConnectionFactory connectionFactory,
    ICloudEventSerializer serializer,
    ICloudEventDispatcher dispatcher,
    IRabbitMQRetryHandler retryHandler,
    IOptions<RabbitMQOptions> options,
    IOptions<RabbitMQSubscriptionOptions> config)
    : IEventSubscriber
{
    public async Task SubscribeAsync(CancellationToken cancellationToken = default)
    {
        var configuation = config.Value.Configurations.Single();

        var connection = await connectionFactory.CreateConnectionAsync(options.Value.ClientName, cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = JsonSerializer.Deserialize<JsonElement>(ea.Body.Span);

                var cloudEvent = serializer.Deserialize(json, typeof(CloudEvent)) as CloudEvent
                    ?? throw new InvalidOperationException("Failed to deserialize CloudEvent.");

                await dispatcher.DispatchAsync(cloudEvent, cancellationToken);

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch
            {
                await retryHandler.HandleRetryAsync(channel, ea, configuation, cancellationToken);
            }

        };

        await channel.BasicConsumeAsync(
            queue: configuation.Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: cancellationToken);
    }
}