using System.Text.Json;
using Asgard.Abstraction.Messaging.Dispatchers;
using Asgard.Abstraction.Models;

namespace Asgard.Abstraction.Messaging.Serialization;

/// <summary>
/// Serializer predefinito che usa System.Text.Json per deserializzare il payload da CloudEvent.
/// </summary>
public sealed class CloudEventJsonSerializer : ICloudEventSerializer
{
    private readonly JsonSerializerOptions _options;

    public CloudEventJsonSerializer(JsonSerializerOptions? options = null)
    {
        _options = options ?? new JsonSerializerOptions { PropertyNamingPolicy = null };
    }

    public object? Deserialize(object? rawData, Type targetType)
    {
        if (rawData is not JsonElement json)
            throw new JsonException("Il dato raw non è un JsonElement valido");

        return JsonSerializer.Deserialize(json.GetRawText(), targetType, _options);
    }

    public ReadOnlyMemory<byte> Serialize(CloudEvent cloudEvent)
    {
        return JsonSerializer.SerializeToUtf8Bytes(cloudEvent, _options);
    }
}
