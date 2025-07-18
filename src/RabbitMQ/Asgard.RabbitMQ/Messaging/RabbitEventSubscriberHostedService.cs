using Asgard.RabbitMQ.Internal;

namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// HostedService wrapper per gestire il ciclo di vita di RabbitEventSubscriber.
/// </summary>
internal sealed class RabbitEventSubscriberHostedService(
    IEventSubscriber subscriber,
    RabbitMQStartupSynchronizer synchronizer) 
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken) 
    {
        // Attende che la topologia sia pronta prima di iniziare la sottoscrizione
        await synchronizer.WaitForTopologyAsync(cancellationToken);

        // Avvia la sottoscrizione agli eventi
        await subscriber.SubscribeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask; 
}
