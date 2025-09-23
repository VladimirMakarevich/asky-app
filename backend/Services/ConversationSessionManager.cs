using System.Collections.Concurrent;
using AskyBackend.Contracts;
using AskyBackend.Hubs;
using AskyBackend.Services.Context;
using AskyBackend.Services.Questions;
using AskyBackend.Services.Summarization;
using AskyBackend.Speech;
using AskyBackend.Telemetry;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AskyBackend.Services;

public sealed class ConversationSessionManager : IConversationSessionManager, ISpeechObserver
{
    private readonly ConcurrentDictionary<string, ConversationSession> _sessions = new();
    private readonly ISpeechSessionFactory _speechSessionFactory;
    private readonly IQuestionGenerationService _questionGenerationService;
    private readonly IConversationContextStore _contextStore;
    private readonly IRollingSummaryService _rollingSummaryService;
    private readonly IHubContext<AsrHub> _hubContext;
    private readonly ILogger<ConversationSessionManager> _logger;
    private readonly IThrottlingService _throttlingService;
    private readonly ITelemetryRecorder _telemetry;

    public ConversationSessionManager(
        ISpeechSessionFactory speechSessionFactory,
        IQuestionGenerationService questionGenerationService,
        IConversationContextStore contextStore,
        IRollingSummaryService rollingSummaryService,
        IThrottlingService throttlingService,
        ITelemetryRecorder telemetry,
        IHubContext<AsrHub> hubContext,
        ILogger<ConversationSessionManager> logger)
    {
        _speechSessionFactory = speechSessionFactory;
        _questionGenerationService = questionGenerationService;
        _contextStore = contextStore;
        _rollingSummaryService = rollingSummaryService;
        _throttlingService = throttlingService;
        _telemetry = telemetry;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task RegisterConnectionAsync(string connectionId, CancellationToken cancellationToken)
    {
        if (_sessions.ContainsKey(connectionId))
        {
            _logger.LogWarning("Connection {ConnectionId} is already registered", connectionId);
            return;
        }

        var speechSession = await _speechSessionFactory.CreateAsync(connectionId, this, cancellationToken);
        var session = new ConversationSession(speechSession);

        if (!_sessions.TryAdd(connectionId, session))
        {
            await speechSession.DisposeAsync();
            throw new InvalidOperationException($"Session for connection '{connectionId}' already exists.");
        }

        await _contextStore.CreateOrResetAsync(connectionId, cancellationToken);
        _logger.LogInformation("Registered SignalR connection {ConnectionId}", connectionId);
    }

    public async Task HandleAudioFrameAsync(string connectionId, AudioFrameDto frame, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(connectionId, out var session))
        {
            throw new HubException("SessionNotInitialized");
        }

        _throttlingService.CheckAudioFrame(connectionId, frame.Payload.Length);
        await session.SpeechSession.EnqueueFrameAsync(frame, cancellationToken);
        session.TrackActivity(frame.Sequence);
    }

    public async Task StopStreamAsync(string connectionId, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(connectionId, out var session))
        {
            return;
        }

        await session.SpeechSession.StopAsync(cancellationToken);
        _logger.LogInformation("StopStream requested for connection {ConnectionId}", connectionId);
    }

    public async Task<IReadOnlyList<QuestionItem>> GenerateQuestionsAsync(
        string connectionId,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken)
    {
        if (!_sessions.ContainsKey(connectionId))
        {
            throw new HubException("SessionNotInitialized");
        }

        _throttlingService.CheckGenerateQuestions(connectionId);
        var snapshot = await _contextStore.GetSnapshotAsync(connectionId, cancellationToken);
        return await _questionGenerationService.GenerateAsync(connectionId, snapshot, options, cancellationToken);
    }

    public async Task CleanupConnectionAsync(string connectionId)
    {
        if (_sessions.TryRemove(connectionId, out var session))
        {
            await session.DisposeAsync();
            _logger.LogInformation("Cleaned up SignalR connection {ConnectionId}", connectionId);
        }

        await _contextStore.RemoveAsync(connectionId, CancellationToken.None);
        _throttlingService.Reset(connectionId);
    }

    public Task OnPartialAsync(string connectionId, PartialTranscript partial, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Partial transcript for {ConnectionId}: {Text}", connectionId, partial.Text);
        return _hubContext.Clients.Client(connectionId).SendAsync("Partial", new
        {
            text = partial.Text,
            offset = partial.Offset,
            duration = partial.Duration
        }, cancellationToken);
    }

    public async Task OnFinalAsync(string connectionId, FinalTranscript final, CancellationToken cancellationToken)
    {
        await _contextStore.RegisterFinalTranscriptAsync(connectionId, final, cancellationToken);
        await _rollingSummaryService.UpdateAsync(connectionId, final, cancellationToken);
        _logger.LogDebug("Final transcript for {ConnectionId}: {Text}", connectionId, final.Text);
        await _hubContext.Clients.Client(connectionId).SendAsync("Final", new
        {
            text = final.Text,
            offset = final.Offset,
            duration = final.Duration,
            facts = final.Facts
        }, cancellationToken);

        if (_sessions.TryGetValue(connectionId, out var session))
        {
            var latency = DateTimeOffset.UtcNow - session.LastActivityUtc;
            if (latency > TimeSpan.Zero)
            {
                _telemetry.RecordAsrLatency(latency);
            }
        }
    }

    public async Task OnErrorAsync(string connectionId, string reason, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Speech pipeline error for {ConnectionId}: {Reason}", connectionId, reason);
        _telemetry.RecordError("speech", reason);
        await _hubContext.Clients.Client(connectionId).SendAsync("Error", new
        {
            reason,
            details = exception.Message
        }, cancellationToken);
    }

    private sealed class ConversationSession : IAsyncDisposable
    {
        public ConversationSession(ISpeechSession speechSession)
        {
            SpeechSession = speechSession;
        }

        public ISpeechSession SpeechSession { get; }

        public DateTimeOffset LastActivityUtc { get; private set; } = DateTimeOffset.UtcNow;

        public int LastSequence { get; private set; }

        public void TrackActivity(int sequence)
        {
            LastActivityUtc = DateTimeOffset.UtcNow;
            LastSequence = sequence;
        }

        public ValueTask DisposeAsync() => SpeechSession.DisposeAsync();
    }
}
