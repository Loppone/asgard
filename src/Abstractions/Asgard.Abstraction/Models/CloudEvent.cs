namespace Asgard.Abstraction.Models;

/// <summary>
/// Rappresentazione interna di un evento conforme allo standard
/// <see href="https://cloudevents.io">CloudEvents</see>.
/// L’oggetto rimane totalmente agnostico rispetto al broker di trasporto.
/// </summary>
public sealed record CloudEvent
{
    public string SpecVersion { get; init; } = "1.0";
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Type { get; init; } = default!;
    public string Source { get; init; } = default!;
    public string? Subject { get; init; }
    public DateTimeOffset Time { get; init; } = DateTimeOffset.UtcNow;
    public string? DataContentType { get; init; } = "application/json";
    public object? Data { get; init; }
    public IDictionary<string, object>? Extensions { get; init; }


    public static CloudEvent Create<T>(
    T payload,
    string type,
    string source,
    string contentType = "application/json")
    {
        return new CloudEvent
        {
            Id = Guid.NewGuid().ToString(),
            Type = type,
            Source = source,
            DataContentType = contentType,
            Time = DateTimeOffset.UtcNow,
            Data = payload,
            Extensions = new Dictionary<string, object>()
        };
    }
}

