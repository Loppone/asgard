namespace Asgard.Abstraction.Events;

/// <summary>
/// Contratto per la sottoscrizione a eventi logici.
/// La gestione della topologia e dei dettagli di trasporto è a carico dell’implementazione.
/// </summary>
public interface IEventSubscriber
{
    /// <summary>
    /// Inizia la sottoscrizione agli eventi in arrivo.
    /// </summary>
    Task SubscribeAsync(CancellationToken cancellationToken = default);
}
