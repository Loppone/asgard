using Asgard.Abstraction.Events;
using Asgard.Abstraction.Messaging.Dispatchers;
using Asgard.Abstraction.Messaging.Handlers;
using Asgard.Abstraction.Models;
using Asgard.RabbitMQ;
using Asgard.RabbitMQ.Messaging;
using Bogus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MultiRoutingKeyWorker;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

builder.Services.AddRabbitMQ(builder.Configuration);

builder.Services.AddSingleton<ICloudEventHandler<UserCreated>, UserCreatedHandler>();
builder.Services.AddSingleton<ICloudEventHandler<PersonCreated>, PersonCreatedHandler>();

builder.Services.AddSingleton<ICloudEventTypeMapper>(sp =>
{
    var mapper = new CloudEventTypeMapper();
    mapper.Register<UserCreated>("user.created");
    mapper.Register<PersonCreated>("person.created");
    return mapper;
});

var app = builder.Build();
await app.StartAsync();

Console.Clear();

var publisher = app.Services.GetRequiredService<IEventPublisher>();

var eventPublisher = new SampleEventPublisher(publisher);

await eventPublisher.PublishRandomEventsAsync(10, CancellationToken.None);


await app.RunAsync();