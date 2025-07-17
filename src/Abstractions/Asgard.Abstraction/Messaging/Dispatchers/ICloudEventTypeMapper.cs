namespace Asgard.Abstraction.Messaging.Dispatchers;

/// <summary>Risolutore dal valore <c>CloudEvent.Type</c> al corrispondente tipo .NET.</summary>
public interface ICloudEventTypeMapper
{
    void Register<T>(string typeName);
    Type? Resolve(string typeName);
    public string? GetTypeName(Type type);
}
