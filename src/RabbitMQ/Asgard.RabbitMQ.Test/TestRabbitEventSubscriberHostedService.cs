using Asgard.Abstraction.Events;
using Microsoft.Extensions.Hosting;

internal class TestRabbitEventSubscriberHostedService(IEventSubscriber subscriber) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        subscriber.SubscribeAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
