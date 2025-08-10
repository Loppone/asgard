using Asgard.Abstraction.Events;
using Asgard.Abstraction.Models;
using Bogus;

namespace MultiRoutingKeyWorker;

public class SampleEventPublisher(IEventPublisher publisher)
{
    private readonly IEventPublisher _publisher = publisher;
    private readonly Faker _faker = new("it");
    private readonly Random _random = new();

    private const string BindingKeyUser = "pub-asgard-user";
    private const string BindingKeyPerson = "pub-asgard-person";

    public async Task PublishRandomEventsAsync(int count, CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < count; i++)
        {
            var publishUser = _random.Next(0, 2) == 0;

            if (publishUser)
            {
                var email = _faker.Internet.Email();
                var user = new UserCreated(_faker.Internet.UserName(), email);

                var ce = CloudEvent.Create(
                    payload: user,
                    type: "asgard-user",
                    source: new Uri("https://example.com").ToString());

                await _publisher.PublishAsync(ce, BindingKeyUser, cancellationToken);
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

                var ce = CloudEvent.Create(
                    payload: person,
                    type: "asgard-person",
                    source: new Uri("https://example.com").ToString());

                await _publisher.PublishAsync(ce, BindingKeyPerson, cancellationToken);
            }
        }
    }
}
