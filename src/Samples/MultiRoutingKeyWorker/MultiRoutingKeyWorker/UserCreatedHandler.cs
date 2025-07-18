using Asgard.Abstraction.Messaging.Handlers;

namespace MultiRoutingKeyWorker;

public sealed class UserCreatedHandler : ICloudEventHandler<UserCreated>
{
    public Task HandleAsync(UserCreated message, CancellationToken ct)
    {
        Console.WriteLine("User created");
        Console.WriteLine($"UserId: {message.UserId} with email: {message.Email}");
        Console.WriteLine();

        return Task.CompletedTask;
    }
}
