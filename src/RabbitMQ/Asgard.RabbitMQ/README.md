# DoggoLabs.Asgard.RabbitMQ

**RabbitMQ transport adapter for the Asgard messaging abstraction.**  
Implements `IEventPublisher` and `IEventSubscriber` using RabbitMQ as message broker.

---

## ✨ Key Features

- 🔁 Automatic retry handling with dead-lettering
- 🧵 Fanout exchange-based topology with optional routing keys
- 📥 Declarative subscription model with bindings
- 🪝 Supports message extensions like `routingKey`
- 🧪 Full support for integration testing (e.g. Testcontainers)

---

## 📦 Installation

```powershell
Install-Package DoggoLabs.Asgard.RabbitMQ
```

---

## 🛠️ Usage Example

```csharp
// 1. Configure the publisher/subscriber
services.AddRabbitMQ(
    configureOptions: opt =>
    {
        opt.HostName = "localhost";
        opt.Port = 5672;
        opt.ClientName = "my-service";
    },
    configurations: new[]
    {
        new RabbitMQConfiguration
        {
            Exchange = "ex.my-service",
            RetryExchange = "ex.my-service.retry",
            DeadLetterExchange = "ex.my-service.dead",
            Queue = "queue.my-service.main",
            DeadLetterQueue = "queue.my-service.dead",
            Bindings = new[]
            {
                new RoutingKeyBinding
                {
                    RoutingKey = "my.routing.key",
                    RetryQueue = "queue.my-service.retry",
                    Retry = new RetrySettings
                    {
                        MaxRetries = 3,
                        DelaysSeconds = new[] { 2, 5, 10 }
                    }
                }
            }
        }
    }
);
```

```csharp
// 2. Register your CloudEvent handler and dispatcher
services.AddSingleton<ICloudEventHandler<UserCreated>, UserCreatedHandler>();
services.AddSingleton<ICloudEventDispatcher, CloudEventDispatcher>();
```

---

## 🧱 Architecture

This package maps the abstract CloudEvent-based contract from `Asgard.Abstraction` into RabbitMQ:

- A main exchange is used to fanout messages to a queue
- Retry messages are redirected to a TTL-based retry queue, then back to the main queue
- Messages that exceed retry count are moved to a DLQ
- Routing keys are optional, but supported via `routingKey` in `CloudEvent.Extensions`

---

## 🔐 Requirements

- RabbitMQ 3.12+ (with support for quorum/classic queues)
- RabbitMQ.Client 7.1.2

---

## 📄 License

This project is licensed under the MIT License.
