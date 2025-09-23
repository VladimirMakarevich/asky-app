using AskyBackend.Contracts;

namespace AskyBackend.Speech;

public interface ISpeechObserver
{
    Task OnPartialAsync(string connectionId, PartialTranscript partial, CancellationToken cancellationToken);

    Task OnFinalAsync(string connectionId, FinalTranscript final, CancellationToken cancellationToken);

    Task OnErrorAsync(string connectionId, string reason, Exception exception, CancellationToken cancellationToken);
}

public interface ISpeechSession : IAsyncDisposable
{
    ValueTask EnqueueFrameAsync(AudioFrameDto frame, CancellationToken cancellationToken);

    ValueTask StopAsync(CancellationToken cancellationToken);
}

public interface ISpeechSessionFactory
{
    Task<ISpeechSession> CreateAsync(string connectionId, ISpeechObserver observer, CancellationToken cancellationToken);
}
