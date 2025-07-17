namespace Asgard.RabbitMQ.Internal;

/// <summary>
/// Coordinatore per sincronizzare la creazione della topologia RabbitMQ.
/// Permette di ritardare l'avvio del subscriber finché la topologia non è pronta.
/// </summary>
internal sealed class RabbitMQStartupSynchronizer
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Attende che la topologia sia pronta.
    /// </summary>
    public Task WaitForTopologyAsync(CancellationToken cancellationToken = default) =>
        _tcs.Task.WaitAsync(cancellationToken);

    /// <summary>
    /// Segnala che la topologia è pronta.
    /// </summary>
    public void SignalTopologyReady() => _tcs.TrySetResult();
}
