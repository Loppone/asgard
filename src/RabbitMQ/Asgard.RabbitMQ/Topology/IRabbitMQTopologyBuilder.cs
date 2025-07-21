namespace Asgard.RabbitMQ.Topology;

/// <summary>
/// Interfaccia che espone metodi per dichiarare exchange, queue e binding su RabbitMQ.
/// Utilizzata internamente per applicare la configurazione topologica.
/// </summary>
internal interface IRabbitMQTopologyBuilder
{
    Task DeclareExchangeAsync(string name, string type, IDictionary<string, object?>? arguments = null);

    Task DeclareQueueAsync(string queueName, IDictionary<string, object?>? arguments = null);

    Task BindQueueAsync(string queueName, string exchangeName, string? routingKey = null);
}
