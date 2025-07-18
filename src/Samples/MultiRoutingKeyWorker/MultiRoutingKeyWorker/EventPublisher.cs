using Asgard.Abstraction.Events;
using Asgard.Abstraction.Models;
using Bogus;

namespace MultiRoutingKeyWorker;

public class SampleEventPublisher(IEventPublisher publisher)
{
    private readonly IEventPublisher _publisher = publisher;
    private readonly Faker _faker = new("it");
    private readonly Random _random = new();

    private const string Source = "sample-app";
    private const string RoutingKeyUser = "user";
    private const string RoutingKeyPerson = "person";

    public async Task PublishRandomEventsAsync(int count, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < count; i++)
        {
            var publishUser = _random.Next(0, 2) == 0;

            if (publishUser)
            {
                var email = _faker.Internet.Email();
                var user = new UserCreated(_faker.Internet.UserName(), email);

                await _publisher.PublishAsync(user, new CloudEventOptions
                {
                    Source = Source
                }, RoutingKeyUser, cancellationToken);
            }
            else
            {
                var gender = _faker.PickRandom<Bogus.DataSets.Name.Gender>();

                var person = new PersonCreated(
                    _random.Next(1, 10001),
                    $"{_faker.Name.FirstName(gender)} {_faker.Name.LastName(gender)}",
                    _random.Next(1, 100),
                    gender == Bogus.DataSets.Name.Gender.Male 
                        ? Gender.Male
                        : Gender.Female);

                await _publisher.PublishAsync(person, new CloudEventOptions
                {
                    Source = Source
                }, RoutingKeyPerson, cancellationToken);
            }
        }
    }
}
