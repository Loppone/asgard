using System.Reflection;
using Asgard.Abstraction.Models;
using Asgard.Abstraction.Messaging.Handlers;

namespace Asgard.Abstraction.Messaging.Dispatchers;

/// <summary>
/// Individua l’handler corretto per il <see cref="CloudEvent.Type"/> e ne invoca
/// l’elaborazione passando il payload deserializzato.
/// </summary>
public sealed class CloudEventDispatcher(
    IServiceProvider serviceProvider,
    ICloudEventTypeMapper typeMapper,
    ICloudEventSerializer serializer) : ICloudEventDispatcher
{
    public async Task DispatchAsync(
        CloudEvent cloudEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudEvent);

        // 1. Risolvi il .NET type dal valore CloudEvent.Type
        var targetType = typeMapper.Resolve(cloudEvent.Type) ?? 
            throw new InvalidOperationException($"No type mapping found for CloudEvent.Type '{cloudEvent.Type}'.");

        // 2. Deserializza il payload nel tipo corretto
        var message = serializer.Deserialize(cloudEvent.Data, targetType);

        // 3. Risolvi dinamicamente ICloudEventHandler<T>
        var handlerType = typeof(ICloudEventHandler<>).MakeGenericType(targetType);
        var handler = serviceProvider.GetService(handlerType) ?? 
            throw new InvalidOperationException($"No ICloudEventHandler registered for type '{cloudEvent.Type}'.");

        // 4. Invoca HandleAsync tramite reflection (o DynamicInvoke)
        var method = handlerType.GetMethod(
            nameof(ICloudEventHandler<object>.HandleAsync),
            BindingFlags.Public | BindingFlags.Instance) ?? throw new MissingMethodException(handlerType.FullName,
                nameof(ICloudEventHandler<object>.HandleAsync));

        var task = (Task)method.Invoke(handler, [message!, cancellationToken])!;

        await task.ConfigureAwait(false);
    }
}
