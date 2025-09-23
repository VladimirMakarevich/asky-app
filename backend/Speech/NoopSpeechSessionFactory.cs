using AskyBackend.Contracts;
using Microsoft.Extensions.Logging;

namespace AskyBackend.Speech;

/// <summary>
/// Development-time stub that fulfils the speech session contract without external dependencies.
/// </summary>
public sealed class NoopSpeechSessionFactory : ISpeechSessionFactory
{
    private readonly ILogger<NoopSpeechSessionFactory> _logger;

    public NoopSpeechSessionFactory(ILogger<NoopSpeechSessionFactory> logger)
    {
        _logger = logger;
    }

    public Task<ISpeechSession> CreateAsync(string connectionId, ISpeechObserver observer, CancellationToken cancellationToken)
    {
        _logger.LogWarning("Using NoopSpeechSession for connection {ConnectionId}. Azure Speech integration is not yet active.", connectionId);
        return Task.FromResult<ISpeechSession>(new NoopSpeechSession());
    }

    private sealed class NoopSpeechSession : ISpeechSession
    {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public ValueTask EnqueueFrameAsync(AudioFrameDto frame, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask StopAsync(CancellationToken cancellationToken) => ValueTask.CompletedTask;
    }
}
