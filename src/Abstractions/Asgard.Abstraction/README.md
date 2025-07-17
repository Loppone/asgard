# DoggoLabs.Asgard.Abstraction

**Lightweight abstraction for broker-independent CloudEvent-based messaging.**  
This library defines the foundational contracts, models, and interfaces for the Asgard messaging system.

---

## ✨ Key Features

- ✅ `CloudEvent` model: fully typed, serializable, CloudEvents-compliant
- ✅ `IEventPublisher` and `IEventSubscriber` interfaces
- ✅ `ICloudEventDispatcher` to route events to handlers
- ✅ `ICloudEventHandler<T>` for typed event handling
- ✅ `ICloudEventTypeMapper` for runtime type resolution
- ✅ Serialization via `ICloudEventSerializer`
- ✅ Support for retry and subscription options

---

## 📦 Installation

```powershell
Install-Package DoggoLabs.Asgard.Abstraction
```

---

## 🛠️ Example Usage

### 1. Define your event payload

```csharp
public record UserCreated(Guid UserId, string Email);
```

### 2. Register the type mapping

```csharp
services.AddSingleton<ICloudEventTypeMapper>(sp =>
{
    var mapper = new CloudEventTypeMapper();
    mapper.Register<UserCreated>("myapp.user.created");
    return mapper;
});
```

### 3. Create an event handler

```csharp
public class UserCreatedHandler : ICloudEventHandler<UserCreated>
{
    public Task HandleAsync(CloudEvent<UserCreated> cloudEvent, CancellationToken ct)
    {
        Console.WriteLine($"User created: {cloudEvent.Data.Email}");
        return Task.CompletedTask;
    }
}
```

### 4. Register dependencies

```csharp
services.AddSingleton<ICloudEventHandler<UserCreated>, UserCreatedHandler>();
services.AddSingleton<ICloudEventDispatcher, CloudEventDispatcher>();
```

---

## 🔄 Dispatcher Behavior

The built-in `CloudEventDispatcher`:

- Resolves the target type using `CloudEvent.Type`
- Deserializes `CloudEvent.Data` accordingly
- Invokes the matching `ICloudEventHandler<T>`

This enables type-safe and decoupled event processing.

---

## 🎯 Design Goals

- 🔌 Broker-agnostic: usable with RabbitMQ, Kafka, in-memory, etc.
- 🔒 Type-safe CloudEvent wrapping and serialization
- 🔍 Extensible: all behaviors are override-friendly and injectable
- 🧱 Minimal assumptions: only event contracts, no transport logic

---

## 📄 License

This project is licensed under the MIT License.