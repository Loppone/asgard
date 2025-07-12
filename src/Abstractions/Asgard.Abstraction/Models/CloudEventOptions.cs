namespace Asgard.Abstraction.Models;

/// <summary>
/// Contenitore dei metadati opzionali usati da <see cref="IEventPublisher"/>
/// per arricchire un <see cref="CloudEvent"/> creato da un payload.
/// Nessun valore è obbligatorio: se omesso, l'implementazione applicherà
/// i propri default (es. <c>Type = typeof(T).FullName</c>).
/// </summary>
public sealed class CloudEventOptions
{
    /// <summary>Identifica semanticamente il tipo dell’evento (es. "Customer.Created").</summary>
    public string? Type { get; init; }

    /// <summary>Identifica la sorgente logica dell'evento (es. "crm-service").</summary>
    public string? Source { get; init; }

    /// <summary>Permette di specificare un soggetto/chiave business (es. customerId).</summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Content-Type del payload.
    /// </summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>
    /// Proprietà aggiuntive da serializzare nel campo <c>extensions</c> del CloudEvent.
    /// Devono essere JSON-serializzabili.
    /// </summary>
    public IReadOnlyDictionary<string, object>? Extensions { get; init; }
}
