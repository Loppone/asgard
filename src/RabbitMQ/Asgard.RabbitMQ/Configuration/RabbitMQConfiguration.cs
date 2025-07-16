namespace Asgard.RabbitMQ.Configuration;

/// <summary>
/// Configura la topologia di una subscription RabbitMQ, inclusi coda, exchange e policy di retry.
/// Supporta configurazioni distinte per ogni routing key.
/// </summary>
public class RabbitMQConfiguration
{
    public string Queue { get; set; } = default!;

    public string Exchange { get; set; } = default!;
    public string ExchangeType { get; set; } = "fanout";

    public string? RetryExchange { get; set; }
    public string? RetryExchangeType { get; set; } = "fanout";

    public string? DeadLetterExchange { get; set; }
    public string? DeadLetterExchangeType { get; set; } = "fanout";
    public string? DeadLetterQueue { get; set; }

    /// <summary>
    /// Configurazioni per ciascuna routing key associata a questa coda.
    /// Se RoutingKey è null, si applica una configurazione neutra (senza RK).
    /// </summary>
    public List<RoutingKeyBinding> Bindings { get; set; } = [];
}


/// <summary>
/// Descrive la configurazione di binding e retry per una singola routing key.
/// </summary>
public class RoutingKeyBinding
{
    /// <summary>
    /// Routing key associata. Se null, si assume gestione "senza routing key".
    /// </summary>
    public string? RoutingKey { get; set; }

    public string? RetryQueue { get; set; }

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
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay (in secondi) tra un retry e l’altro. 
    /// La lunghezza della lista determina il numero di livelli di ritentativo.
    /// </summary>
    public List<int> DelaysSeconds { get; set; } = [];
}