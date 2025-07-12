using Asgard.Abstraction.Models;

namespace Asgard.Abstraction.Messaging.Consumers;

/// <summary>
/// Interfaccia generica per gestire eventi di tipo T in modo tipizzato e indipendente dal broker.
/// </summary>
public interface IEventConsumer<T>
{
    /// <summary>
    /// Gestisce un CloudEvent deserializzato con payload di tipo T.
    /// </summary>
    Task HandleAsync(CloudEvent cloudEvent, T payload, CancellationToken cancellationToken);
}
