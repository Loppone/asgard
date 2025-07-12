namespace Asgard.RabbitMQ.Messaging;

/// <summary>
/// HostedService wrapper per gestire il ciclo di vita di RabbitEventSubscriber.
/// </summary>
internal sealed class RabbitEventSubscriberHostedService(IEventSubscriber subscriber) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        subscriber.SubscribeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask; 
}
