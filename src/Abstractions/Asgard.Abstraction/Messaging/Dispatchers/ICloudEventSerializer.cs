using Asgard.Abstraction.Models;

namespace Asgard.Abstraction.Messaging.Dispatchers;

/// <summary>Servizio di serializzazione/deserializzazione del campo <c>Data</c> del CloudEvent.</summary>
public interface ICloudEventSerializer
{
    object? Deserialize(object? rawData, Type targetType);

    /// <summary>
    /// Serializza un CloudEvent nel formato binario per l'invio.
    /// </summary>
    ReadOnlyMemory<byte> Serialize(CloudEvent cloudEvent);
}
