using Asgard.Abstraction.Messaging.Handlers;

namespace SimpleWorker;

public sealed class UserCreatedHandler : ICloudEventHandler<UserCreated>
{
    public Task HandleAsync(UserCreated message, CancellationToken ct)
    {
        Console.WriteLine($"User created: {message.Email}");

        return Task.CompletedTask;
    }
}
