namespace Asgard.Abstraction.Messaging.Configuration;


/// <summary>
/// Configura la sottoscrizione a un tipo di evento specifico.
/// 
/// Ogni istanza rappresenta un'associazione tra un <c>EventType</c>
/// (cioè il tipo dell'evento, come da specifica CloudEvent) e un <c>HandlerType</c>
/// che implementa <see cref="ICloudEventHandler{T}"/>.
/// 
/// Il sistema di messaggistica userà questa configurazione per instradare
/// correttamente gli eventi verso l'handler corrispondente.
/// 
/// Gli aspetti legati al routing (es. exchange, queue, topic) sono demandati
/// alle implementazioni specifiche nei pacchetti come <c>Asgard.RabbitMQ</c>.
/// </summary>
public sealed class SubscriberConfig
{
    /// <summary>
    /// Tipo dell'evento da intercettare (es. "com.example.order.created").
    /// Deve corrispondere esattamente al campo <c>Type</c> del <see cref="CloudEvent"/>.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Tipo dell'handler che verrà invocato quando viene ricevuto un evento del tipo specificato.
    /// Deve implementare <see cref="ICloudEventHandler{T}"/> per un tipo compatibile con l'evento.
    /// </summary>
    public required Type HandlerType { get; init; }
}