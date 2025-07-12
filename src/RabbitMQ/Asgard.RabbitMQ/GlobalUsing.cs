global using System.Text.Json;

global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Options;

global using RabbitMQ.Client;
global using RabbitMQ.Client.Events;

global using Asgard.Abstraction.Events;
global using Asgard.Abstraction.Messaging.Dispatchers;
global using Asgard.Abstraction.Messaging.Serialization;
global using Asgard.Abstraction.Models;

global using Asgard.RabbitMQ.Configuration;
global using Asgard.RabbitMQ.Messaging;
global using Asgard.RabbitMQ.Topology;
global using Asgard.RabbitMQ.Validation;