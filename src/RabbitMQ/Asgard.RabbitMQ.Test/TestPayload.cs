using Asgard.Abstraction.Messaging.Handlers;
using System.Threading.Channels;

namespace Asgard.RabbitMQ.Test
{
    public record TestPayload(string Value);

    internal sealed class TestPayloadHandler(ChannelWriter<TestPayload> writer)
        : ICloudEventHandler<TestPayload>
    {
        public Task HandleAsync(TestPayload message, CancellationToken cancellationToken)
        {
            Console.WriteLine($"[TestPayloadHandler] Ricevuto: {message.Value}");
            writer.TryWrite(message);
            return Task.CompletedTask;
        }
    }
}