namespace Asgard.Abstraction.Messaging.Handlers;

/// <summary>
/// Contratto per gestire la logica di elaborazione di un evento <see cref="CloudEvent"/> tipizzato.
/// </summary>
/// <typeparam name="T">Tipo del payload dell’evento.</typeparam>
public interface ICloudEventHandler<T>
{
    /// <summary>
    /// Elabora l’evento ricevuto.
    /// </summary>
    /// <param name="message">Evento tipizzato da processare.</param>
    /// <param name="cancellationToken">Token di cancellazione.</param>
    Task HandleAsync(
        T message, 
        CancellationToken cancellationToken);
}
