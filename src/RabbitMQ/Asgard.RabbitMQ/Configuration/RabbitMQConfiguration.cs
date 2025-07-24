namespace Asgard.RabbitMQ.Configuration;

/// <summary>
/// Configura la topologia di una subscription RabbitMQ, inclusi coda, exchange e policy di retry.
/// Supporta configurazioni distinte per ogni routing key.
/// </summary>
public class RabbitMQConfiguration
{
    // Main Queue
    public string Queue { get; set; } = default!;
    public IDictionary<string, object?>? QueueArguments { get; set; }

    // Main Exchange
    public string Exchange { get; set; } = default!;
    public string ExchangeType { get; set; } = "fanout";
    public IDictionary<string, object?>? ExchangeArguments { get; set; }

    // Retry
    public string? RetryQueue { get; set; }
    public IDictionary<string, object?>? RetryQueueArguments { get; set; }
    public string? RetryExchange { get; set; }
    public string? RetryExchangeType { get; set; } = "fanout";
    public IDictionary<string, object?>? RetryExchangeArguments { get; set; }

    // Dead Letter
    public string? DeadLetterQueue { get; set; }
    public IDictionary<string, object?>? DeadLetterQueueArguments { get; set; }
    public string? DeadLetterExchange { get; set; }
    public string? DeadLetterExchangeType { get; set; } = "fanout";
    public IDictionary<string, object?>? DeadLetterExchangeArguments { get; set; }

    /// <summary>
    /// Configurazioni per ciascuna routing key associata a questa coda.
    /// Se RoutingKey è null, si applica una configurazione neutra (senza RK).
    /// </summary>
    public List<RoutingKeyBinding> Bindings { get; set; } = [];
    public List<RoutingKeyPublish> Publish { get; set; } = [];
}

/// <summary>
/// Descrive la configurazione dei parametri del publisher.
/// Si assume che l'exchange sia quello principale.
/// </summary>
public class RoutingKeyPublish
{
    /// <summary>
    /// Chiave logica che rappresenta il tipo di messaggio (usata dall'applicazione)
    /// </summary>
    public string? Key { get; set; } = default!;

    /// <summary>
    /// Routing key associata. Se null, si assume gestione "senza routing key".
    /// </summary>
    public string? RoutingKey { get; set; }
}


/// <summary>
/// Descrive la configurazione di binding e retry per una singola routing key.
/// </summary>
public class RoutingKeyBinding
{
    /// <summary>
    /// Chiave logica che rappresenta il tipo di messaggio (usata dall'applicazione)
    /// </summary>
    public string? Key { get; set; } = default!;

    /// <summary>
    /// Routing key associata. Se null, si assume gestione "senza routing key".
    /// </summary>
    public string? RoutingKey { get; set; }

    /// <summary>
    /// Impostazioni per il retry specifiche di questa routing key.
    /// </summary>
    public RetrySettings? Retry { get; set; }
}


/// <summary>
/// Impostazioni per il meccanismo di retry via RabbitMQ (con TTL e tentativi).
/// </summary>
public class RetrySettings
{
    public int MaxRetries { get; set; }

    /// <summary>
    /// Delay (in secondi) tra un retry e l’altro. 
    /// La lunghezza della lista determina il numero di livelli di ritentativo.
    /// </summary>
    public List<int> DelaysSeconds { get; set; } = [];
}