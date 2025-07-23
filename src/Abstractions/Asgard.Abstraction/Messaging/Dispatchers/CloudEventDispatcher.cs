using System.Reflection;
using Asgard.Abstraction.Models;
using Asgard.Abstraction.Messaging.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Asgard.Abstraction.Messaging.Dispatchers;

/// <summary>
/// Individua l’handler corretto per il <see cref="CloudEvent.Type"/> e ne invoca
/// l’elaborazione passando il payload deserializzato.
/// </summary>
public sealed class CloudEventDispatcher(
    IServiceScopeFactory scopeFactory,
    ICloudEventTypeMapper typeMapper,
    ICloudEventSerializer serializer) : ICloudEventDispatcher
{
    public async Task DispatchAsync(
        CloudEvent cloudEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cloudEvent);

        // Risolve il tipo dal valore CloudEvent.Type
        var targetType = typeMapper.Resolve(cloudEvent.Type) ?? 
            throw new InvalidOperationException($"No type mapping found for CloudEvent.Type '{cloudEvent.Type}'.");

        // Deserializza il payload nel tipo corretto
        var message = serializer.Deserialize(cloudEvent.Data, targetType);

        using var scope = scopeFactory.CreateScope();

        // Risolve dinamicamente ICloudEventHandler<T>
        var handlerType = typeof(ICloudEventHandler<>).MakeGenericType(targetType);
        var handler = scope.ServiceProvider.GetService(handlerType) ?? 
            throw new InvalidOperationException($"No ICloudEventHandler registered for type '{cloudEvent.Type}'.");

        // Invoca l'handler tramite reflection
        var method = handlerType.GetMethod(
            nameof(ICloudEventHandler<object>.HandleAsync),
            BindingFlags.Public | BindingFlags.Instance) ?? throw new MissingMethodException(handlerType.FullName,
                nameof(ICloudEventHandler<object>.HandleAsync));

        var task = (Task)method.Invoke(handler, [message!, cancellationToken])!;

        await task.ConfigureAwait(false);
    }
}
