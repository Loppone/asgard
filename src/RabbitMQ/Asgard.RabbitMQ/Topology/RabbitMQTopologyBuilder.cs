namespace Asgard.RabbitMQ.Topology;

/// <summary>
/// Implementazione concreta del costruttore topologico che usa RabbitMQ.Client per dichiarare exchange, queue e binding.
/// </summary>
internal sealed class RabbitMQTopologyBuilder(
    IConnectionFactory connectionFactory,
    IOptions<RabbitMQOptions> options) 
    : IRabbitMQTopologyBuilder
{
    public async Task DeclareExchangeAsync(string exchange, string type, IDictionary<string, object?>? arguments = null)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(options.Value.ClientName);
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: type,
            durable: true,
            autoDelete: false,
            arguments: arguments);
    }

    public async Task DeclareQueueAsync(string queue, IDictionary<string, object?>? arguments = null)
    {
        await using var connection = await connectionFactory.CreateConnectionAsync(options.Value.ClientName);
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: arguments);
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
