using Asgard.Abstraction.Messaging.Handlers;

namespace Asgard.RabbitMQ.Test;

/// <summary>
/// Handler che fallisce sempre, per simulare errori e testare retry e DLQ in RabbitMQ.
/// </summary>
public sealed class FailingTestPayloadHandler : ICloudEventHandler<TestPayload>
{
    public Task HandleAsync(TestPayload payload, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("Simulated failure for testing RabbitMQ retry mechanism.");
    }
}
