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

        var headers = args.BasicProperties?.Headers != null
            ? new Dictionary<string, object>(args.BasicProperties.Headers!)
            : [];

        var retryCount = 0;

        if (headers.TryGetValue("x-retry-count", out var raw) && raw is byte[] bytes)
        {
            var str = Encoding.UTF8.GetString(bytes);
            _ = int.TryParse(str, out retryCount);
        }

        retryCount++;

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

        headers["x-retry-count"] = Encoding.UTF8.GetBytes(retryCount.ToString());

        var originalProps = args.BasicProperties;

        var props = new BasicProperties
        {
            ContentType = originalProps?.ContentType,
            DeliveryMode = originalProps?.DeliveryMode ?? DeliveryModes.Persistent,
            Headers = headers!,
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
