using Asgard.Abstraction.Models;
using Asgard.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Asgard.Abstraction.Messaging.Handlers;
using SimpleWorker;
using Asgard.Abstraction.Events;
using Asgard.Abstraction.Messaging.Dispatchers;
using Asgard.RabbitMQ.Messaging;

const string ROUTINGKEY = "asgard";

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile("appsettings.json", optional: false);

builder.Services.AddRabbitMQ(builder.Configuration);

builder.Services.AddSingleton<ICloudEventHandler<UserCreated>, UserCreatedHandler>();

builder.Services.AddSingleton<ICloudEventTypeMapper>(sp =>
 {
     var mapper = new CloudEventTypeMapper();
     mapper.Register<UserCreated>("asgard");
     return mapper;
 });

var app = builder.Build();
await app.StartAsync();

Console.Clear();

// Simula una pubblicazione all'avvio
var publisher = app.Services.GetRequiredService<IEventPublisher>();

await publisher.PublishAsync(
    new UserCreated(Guid.NewGuid(), "example@email.com"),
    new CloudEventOptions
    {
        Source = "sample-app"
    },
    ROUTINGKEY,
    CancellationToken.None);

await app.RunAsync();
