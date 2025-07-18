using Asgard.Abstraction.Models;
using Asgard.RabbitMQ;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Asgard.Abstraction.Messaging.Handlers;
using SimpleWorker;
using Asgard.Abstraction.Events;

internal class Program
{
    private static async Task Main(string[] args)
    {
        const string ROUTINGKEY = "asgard";
        
        var builder = Host.CreateApplicationBuilder(args);

        builder.Configuration.AddJsonFile("appsettings.json", optional: false);
        
        builder.Services.AddRabbitMQ(builder.Configuration);

        builder.Services.AddSingleton<ICloudEventHandler<UserCreated>, UserCreatedHandler>();

        var app = builder.Build();
        await app.StartAsync();

        // Simula una pubblicazione all'avvio
        var publisher = app.Services.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(
            new UserCreated(Guid.NewGuid(), "example@email.com"),
            new CloudEventOptions
            {
                Type = "user.created",
                Source = "sample-app"
            },
            ROUTINGKEY,
            CancellationToken.None);

        await app.RunAsync();
    }
}