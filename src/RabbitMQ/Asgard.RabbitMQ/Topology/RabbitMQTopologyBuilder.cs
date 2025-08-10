using RabbitMQ.Client.Exceptions;

namespace Asgard.RabbitMQ.Topology;

/// <summary>
/// Implementazione concreta del costruttore topologico che usa RabbitMQ.Client per dichiarare exchange, queue e binding.
/// </summary>
internal sealed class RabbitMQTopologyBuilder(
    IConnectionFactory connectionFactory,
    IOptions<RabbitMQOptions> options)
    : IRabbitMQTopologyBuilder
{
    public async Task DeclareExchangeAsync(
        string exchange, 
        string type, 
        IDictionary<string, object?>? arguments = null)
    {
        TopologyExchangeGuard.Exchange(exchange, type);
        arguments ??= new Dictionary<string, object?>(StringComparer.Ordinal);

        await using var connection = await connectionFactory.CreateConnectionAsync(options.Value.ClientName);
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.ExchangeDeclarePassiveAsync(exchange);
        }
        catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404)
        {
            try
            {
                await channel.ExchangeDeclareAsync(
                    exchange: exchange,
                    type: type,
                    durable: true,
                    autoDelete: false,
                    arguments: arguments);
            }
            catch (OperationInterruptedException ex2) when (ex2.ShutdownReason?.ReplyCode == 406)
            {
                throw new InvalidOperationException(
                    $"Existing exchange '{exchange}' is incompatible with requested declaration.", ex2);
            }
        }
    }

    public async Task DeclareQueueAsync(string queue, IDictionary<string, object?>? arguments = null)
    {
        TopologyQueueGuard.Queue(queue);
        arguments = TopologyQueueGuard.Arguments(arguments);

        await using var connection = await connectionFactory.CreateConnectionAsync(options.Value.ClientName);
        await using var channel = await connection.CreateChannelAsync();

        try
        {
            await channel.QueueDeclarePassiveAsync(queue);
        }
        catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404)
        {
            await channel.QueueDeclareAsync(
                queue: queue,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: arguments);
        }
        catch (OperationInterruptedException ex2) when (ex2.ShutdownReason?.ReplyCode == 406)
        {
            throw new InvalidOperationException(
                $"Existing queue '{queue}' is incompatible with requested declaration.", ex2);
        }
    }

    public async Task BindQueueAsync(string queue, string exchange, string? routingKey = "")
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(options.Value.ClientName);
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueBindAsync(
            queue: queue,
            exchange: exchange,
            routingKey: routingKey ?? string.Empty,
            arguments: null);
    }
}


internal static class TopologyExchangeGuard
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
        { "direct","fanout","topic","headers" };

    public static void Exchange(string exchange, string type)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchange);
        if (exchange.Length == 0)
            throw new ArgumentException("Default exchange cannot be declared.", nameof(exchange));

        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        if (!Allowed.Contains(type))
            throw new ArgumentException($"Unsupported exchange type '{type}'.", nameof(type));
    }
}

internal static class TopologyQueueGuard
{
    public static void Queue(string queue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queue);
    }

    public static IDictionary<string, object?> Arguments(IDictionary<string, object?>? args)
        => args ?? new Dictionary<string, object?>(StringComparer.Ordinal);
}