using System.Collections.Concurrent;

namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// Mappa tra il valore di <c>CloudEvent.Type</c> (stringa logica) e il tipo .NET da deserializzare.
/// Viene usato dal dispatcher per istanziare correttamente il payload.
/// </summary>
public sealed class CloudEventTypeMapper : ICloudEventTypeMapper
{
    private readonly ConcurrentDictionary<string, Type> _map = new();
    private readonly ConcurrentDictionary<Type, string> _reverseMap = new();

    /// <summary>
    /// Risolve il valore CloudEvent.Type associato a un tipo .NET, se registrato.
    /// </summary>
    public string? GetTypeName(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return _reverseMap.TryGetValue(type, out var typeName) ? typeName : null;
    }

    /// <summary>
    /// Registra il tipo .NET associato a un identificatore logico (CloudEvent.Type).
    /// </summary>
    public void Register<T>(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        var type = typeof(T);
        _map[typeName] = type;
        _reverseMap[type] = typeName;
    }

    /// <summary>
    /// Risolve il tipo .NET associato al valore di CloudEvent.Type, se registrato.
    /// </summary>
    public Type? Resolve(string typeName)
    {
        ArgumentNullException.ThrowIfNull(typeName);

        return _map.TryGetValue(typeName, out var type) ? type : null;
    }
}
