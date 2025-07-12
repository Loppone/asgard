namespace Asgard.Abstraction.Models;

/// <summary>
/// Opzioni facoltative per modulare il comportamento della sottoscrizione.
/// Tutti i campi sono broker-agnostici: l’adapter concreto li mapperà
/// sul costrutto equivalente (topic, queue, subject, ecc.).
/// </summary>
public sealed class SubscriptionOptions
{
    /// <summary>
    /// Filtro sul tipo semantico dell’evento (campo <c>Type</c> di CloudEvent).
    /// Se <c>null</c> vengono consegnati tutti gli eventi.
    /// </summary>
    public string? EventTypeFilter { get; init; }

    /// <summary>
    /// Numero massimo di eventi elaborati in parallelo (default 1).
    /// </summary>
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>
    /// Dizionario di metadati broker-specifici (facoltativo):
    /// l’adapter decide se e come usarlo.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Extensions { get; init; }
}
