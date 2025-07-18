using Asgard.Abstraction.Models;
using Asgard.Abstraction.Messaging.Dispatchers;
using Asgard.Abstraction.Messaging.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Text.Json;
using Asgard.Abstraction.Messaging.Serialization;
using FluentAssertions;

namespace Asgard.Abstraction.Test.Messaging.Dispatcher;

public sealed class CloudEventDispatcherTests
{
    private static ServiceProvider BuildServiceProvider<TPayload>(
        Mock<ICloudEventHandler<TPayload>> handlerMock,
        ICloudEventTypeMapper mapper,
        ICloudEventSerializer serializer)
        where TPayload : class
    {
        return new ServiceCollection()
            .AddSingleton(mapper)
            .AddSingleton(serializer)
            .AddSingleton(typeof(ICloudEventHandler<TPayload>), _ => handlerMock.Object)
            .BuildServiceProvider();
    }

    [Fact]
    public async Task DispatchAsync_Invokes_Correct_Handler()
    {
        var targetType = typeof(TestPayload);
        var mapperMock = new Mock<ICloudEventTypeMapper>();
        mapperMock.Setup(m => m.Resolve("test.event")).Returns(targetType);

        var payload = new TestPayload { Value = 42 };
        var serializerMock = new Mock<ICloudEventSerializer>();
        serializerMock.Setup(s => s.Deserialize(It.IsAny<object?>(), targetType))
                      .Returns(payload);

        var handlerMock = new Mock<ICloudEventHandler<TestPayload>>();
        var sp = BuildServiceProvider(handlerMock, mapperMock.Object, serializerMock.Object);

        var dispatcher = new CloudEventDispatcher(sp, mapperMock.Object, serializerMock.Object);

        var cloudEvent = new CloudEvent
        {
            Type = "test.event",
            Data = new { value = 42 }
        };

        await dispatcher.DispatchAsync(cloudEvent, CancellationToken.None);

        handlerMock.Verify(
            h => h.HandleAsync(payload, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DispatchAsync_Throws_When_Handler_Missing()
    {
        var mapperMock = new Mock<ICloudEventTypeMapper>();
        mapperMock.Setup(m => m.Resolve("unknown.event")).Returns(typeof(TestPayload));

        var serializerMock = new Mock<ICloudEventSerializer>();
        serializerMock.Setup(s => s.Deserialize(It.IsAny<object?>(), typeof(TestPayload)))
                      .Returns(new TestPayload());

        var sp = new ServiceCollection()
            .AddSingleton(mapperMock.Object)
            .AddSingleton(serializerMock.Object)
            .BuildServiceProvider();

        var dispatcher = new CloudEventDispatcher(sp, mapperMock.Object, serializerMock.Object);

        var cloudEvent = new CloudEvent { Type = "unknown.event" };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(cloudEvent, CancellationToken.None));
    }

    [Fact]
    public async Task DispatchAsync_Throws_If_Handler_Not_Registered()
    {
        var payload = new WillNeverRegisterPayload();
        var json = JsonSerializer.SerializeToElement(payload);

        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.1",
            Id = Guid.NewGuid().ToString(),
            Type = "asgard.event.never",
            Source = "test",
            Time = DateTimeOffset.UtcNow,
            DataContentType = "application/json",
            Data = json
        };

        var mapper = new TestMapper();
        mapper.Register<WillNeverRegisterPayload>("asgard.event.never");

        var dispatcher = new CloudEventDispatcher(
            new ServiceCollection().BuildServiceProvider(),
            mapper,
            new FakeSerializer()
        );

        var act = () => dispatcher.DispatchAsync(cloudEvent, CancellationToken.None);

        await act.Should()
            .ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage($"No ICloudEventHandler registered for type 'asgard.event.never'.");
    }

    [Fact]
    public async Task DispatchAsync_Throws_If_Type_Not_Mapped()
    {
        var payload = new TestPayload { Value = 123 };
        var json = JsonSerializer.SerializeToElement(payload);

        var cloudEvent = new CloudEvent
        {
            SpecVersion = "1.1",
            Id = Guid.NewGuid().ToString(),
            Type = "unregistered.type",
            Source = "test",
            Time = DateTimeOffset.UtcNow,
            DataContentType = "application/json",
            Data = json
        };

        var mapper = new TestMapper();
        var dispatcher = new CloudEventDispatcher(
            new ServiceCollection().BuildServiceProvider(),
            mapper,
            new FakeSerializer()
        );

        var act = () => dispatcher.DispatchAsync(cloudEvent, CancellationToken.None);

        await act.Should()
            .ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage("No type mapping found for CloudEvent.Type 'unregistered.type'.");
    }


    [Fact]
    public async Task DispatchAsync_Throws_If_Payload_Invalid()
    {
        var mapper = new TestMapper();
        mapper.Register<TestPayload>("asgard.event");

        var serializer = new BrokenSerializer();

        var sp = new ServiceCollection()
            .AddSingleton(Mock.Of<ICloudEventHandler<TestPayload>>())
            .BuildServiceProvider();

        var dispatcher = new CloudEventDispatcher(sp, mapper, serializer);
        var cloudEvent = CreateCloudEvent(new TestPayload());

        await Assert.ThrowsAsync<JsonException>(() =>
            dispatcher.DispatchAsync(cloudEvent, CancellationToken.None));
    }


    [Fact]
    public async Task DispatchAsync_Propagates_Handler_Exception()
    {
        var handlerMock = new Mock<ICloudEventHandler<TestPayload>>();
        handlerMock
            .Setup(h => h.HandleAsync(It.IsAny<TestPayload>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var mapper = new TestMapper();
        mapper.Register<TestPayload>("asgard.event");

        var serializer = new CloudEventJsonSerializer();

        var sp = new ServiceCollection()
            .AddSingleton(handlerMock.Object)
            .BuildServiceProvider();

        var dispatcher = new CloudEventDispatcher(sp, mapper, serializer);
        var cloudEvent = CreateCloudEvent(new TestPayload());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            dispatcher.DispatchAsync(cloudEvent, CancellationToken.None));
    }


    public sealed record TestPayload
    {
        public int Value { get; init; }
    }

    public sealed record WillNeverRegisterPayload;


    private static CloudEvent CreateCloudEvent(object payload)
    {
        var json = JsonSerializer.SerializeToElement(payload);

        return new CloudEvent
        {
            Id = Guid.NewGuid().ToString(),
            Type = "asgard.event", // il mio routing key per dispatchare l'evento
            Source = "test",
            DataContentType = "application/json",
            Time = DateTimeOffset.UtcNow,
            Data = json
        };
    }


    private sealed class TestMapper : ICloudEventTypeMapper
    {
        private readonly Dictionary<string, Type> _map = [];
        private readonly Dictionary<Type, string> _reverseMap = [];

        public string? GetTypeName(Type type)
        {
            ArgumentNullException.ThrowIfNull(type);

            return _reverseMap.TryGetValue(type, out var typeName) ? typeName : null;
        }

        public void Register<T>(string typeName)
        {
            _map[typeName] = typeof(T);
        }

        public Type? Resolve(string cloudEventType)
        {
            _map.TryGetValue(cloudEventType, out var type);

            return type;
        }
    }


    private sealed class BrokenSerializer : ICloudEventSerializer
    {
        public object? Deserialize(object? rawData, Type targetType)
            => throw new JsonException("Fake deserialization failure");

        public ReadOnlyMemory<byte> Serialize(CloudEvent cloudEvent)
            => throw new NotImplementedException("Serialize is not used in this test.");
    }


    private sealed class FakeSerializer : ICloudEventSerializer
    {
        public object? Deserialize(object? rawData, Type targetType) => rawData;

        public ReadOnlyMemory<byte> Serialize(CloudEvent cloudEvent)
            => JsonSerializer.SerializeToUtf8Bytes(cloudEvent);
    }
}