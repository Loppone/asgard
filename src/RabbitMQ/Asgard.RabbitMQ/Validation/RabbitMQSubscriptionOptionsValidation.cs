namespace Asgard.RabbitMQ.Validation;

/// <summary>
/// Valida la presenza di almeno una configurazione RabbitMQ valida.
/// </summary>
internal sealed class RabbitMQSubscriptionOptionsValidation : IValidateOptions<RabbitMQSubscriptionOptions>
{
    public ValidateOptionsResult Validate(string? name, RabbitMQSubscriptionOptions options)
    {
        if (options.Configurations is null || options.Configurations.Count == 0)
        {
            return ValidateOptionsResult.Fail("At least one RabbitMQ configuration must be defined in 'RabbitMqSubscriptionOptions'.");
        }

        return ValidateOptionsResult.Success;
    }
}
