namespace Asgard.RabbitMQ.Configuration;

/// <summary>
/// Opzioni di configurazione per una o più subscription RabbitMQ.
/// Contiene tutte le configurazioni topologiche definite dall'utente.
/// </summary>
public class RabbitMQSubscriptionOptions
{
    public List<RabbitMQConfiguration> Configurations { get; set; } = [];
}
