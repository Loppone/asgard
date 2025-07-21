using Asgard.Abstraction.Models;

namespace Asgard.Abstraction.Events;

/// <summary>
/// Contratto per la pubblicazione di eventi nel formato <see cref="CloudEvent"/>.
/// Totalmente broker-agnostico. Qualsiasi dettaglio implementativo (routing, exchange, topic, ecc.)
/// deve essere gestito nei pacchetti specifici (es. Asgard.RabbitMQ, Asgard.Kafka).
/// </summary>
/// <summary>
public interface IEventPublisher
{
    /// <summary>
    /// Pubblica un evento già costruito.
    /// </summary>
    /// <param name="cloudEvent">Evento conforme al formato CloudEvent.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    Task PublishAsync(
        CloudEvent cloudEvent,
        string? bindingKey = null,
        CancellationToken cancellationToken = default);
}