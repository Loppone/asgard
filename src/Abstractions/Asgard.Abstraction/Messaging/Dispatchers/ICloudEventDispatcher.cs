using Asgard.Abstraction.Models;

namespace Asgard.Abstraction.Messaging.Dispatchers;

/// <summary>
/// Dispatch dinamico di un evento <see cref="CloudEvent"/> verso l’handler registrato
/// per il tipo specifico.
/// </summary>
public interface ICloudEventDispatcher
{
    /// <summary>
    /// Elabora il CloudEvent indirizzandolo all’handler corretto.
    /// </summary>
    /// <param name="cloudEvent">Evento da dispatchare.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    Task DispatchAsync(
        CloudEvent cloudEvent, 
        CancellationToken cancellationToken);
}
