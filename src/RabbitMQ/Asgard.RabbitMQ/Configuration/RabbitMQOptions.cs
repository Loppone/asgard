namespace Asgard.RabbitMQ.Configuration;

/// <summary>
/// Opzioni generali di connessione e configurazione dell'exchange RabbitMQ.
/// Vengono usate dal publisher e per la dichiarazione iniziale della topologia.
/// </summary>
public class RabbitMQOptions
{
    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public bool UseSsl { get; set; } = false;

    /// <summary>
    /// Nome dell'exchange principale su cui pubblicare.
    /// (es. "ex.harleydikkinson")
    /// </summary>
    public string Exchange { get; set; } = default!;

    /// <summary>
    /// Tipo di exchange (direct, fanout, topic).
    /// Default = direct.
    /// </summary>
    public string ExchangeType { get; set; } = "direct";

    /// <summary>
    /// Nome identificativo del client (usato nella connessione).
    /// </summary>
    public string ClientName { get; set; } = "asgard-client";
}