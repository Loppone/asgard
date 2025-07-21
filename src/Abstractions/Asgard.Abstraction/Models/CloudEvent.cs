using System.Text.Json.Serialization;

namespace Asgard.Abstraction.Models;

/// <summary>
/// Rappresentazione interna di un evento conforme allo standard
/// <see href="https://cloudevents.io">CloudEvents</see>.
/// L’oggetto rimane totalmente agnostico rispetto al broker di trasporto.
/// </summary>
public sealed record CloudEvent
{
    // JsonConstructor perchè per la serializzazione con System.Text.Json è necessario un
    // costruttore pubblico senza parametri.
    [JsonConstructor]
    private CloudEvent() { }

    public required string SpecVersion { get; init; }
    public required string Id { get; init; }
    public required string Type { get; init; }
    public required string Source { get; init; }

    public string? Subject { get; init; }
    public DateTimeOffset Time { get; init; }
    public string? DataContentType { get; init; }
    public object? Data { get; init; }
    public IDictionary<string, object>? Extensions { get; init; }

    /// <summary>
    /// Crea un nuovo CloudEvent valido secondo lo standard 1.0.
    /// </summary>
    public static CloudEvent Create<T>(
        T payload,
        string type,
        string source,
        string? subject = null,
        string contentType = "application/json",
        IDictionary<string, object>? extensions = null,
        DateTimeOffset? time = null)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("CloudEvent.Type is required and cannot be null or empty.", nameof(type));

        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("CloudEvent.Source is required and cannot be null or empty.", nameof(source));

        return new CloudEvent
        {
            Id = Guid.NewGuid().ToString(),
            SpecVersion = "1.0",
            Type = type,
            Source = source,
            Subject = subject,
            Time = time ?? DateTimeOffset.UtcNow,
            DataContentType = contentType,
            Data = payload,
            Extensions = extensions
        };
    }
}
