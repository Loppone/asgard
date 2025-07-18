using Asgard.Abstraction.Messaging.Handlers;

namespace MultiRoutingKeyWorker;

public sealed class PersonCreatedHandler : ICloudEventHandler<PersonCreated>
{
    public Task HandleAsync(PersonCreated message, CancellationToken ct)
    {
        var pronoun = message.Gender == Gender.Male ? "He" : "She";

        Console.WriteLine("Person created");
        Console.WriteLine($"{message.Name} created. {pronoun}'s {message.Age} years old.");
        Console.WriteLine();

        return Task.CompletedTask;
    }
}
