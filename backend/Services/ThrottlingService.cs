using System.Collections.Concurrent;
using AskyBackend.Options;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace AskyBackend.Services;

public sealed class ThrottlingService : IThrottlingService
{
    private readonly ThrottlingOptions _options;
    private readonly ConcurrentDictionary<string, AudioRateState> _audioStates = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _questionsState = new();

    public ThrottlingService(IOptions<ThrottlingOptions> options)
    {
        _options = options.Value;
    }

    public void CheckAudioFrame(string connectionId, int payloadSize)
    {
        if (payloadSize > _options.MaxAudioPayloadBytes)
        {
            throw new HubException("PayloadTooLarge");
        }

        var now = DateTimeOffset.UtcNow;
        var state = _audioStates.GetOrAdd(connectionId, _ => new AudioRateState(now));
        lock (state)
        {
            if (now - state.WindowStart >= TimeSpan.FromSeconds(1))
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            if (state.Count >= _options.MaxAudioFramesPerSecond)
            {
                throw new HubException("AudioRateExceeded");
            }

            state.Count++;
        }
    }

    public void CheckGenerateQuestions(string connectionId)
    {
        var now = DateTimeOffset.UtcNow;
        var last = _questionsState.GetOrAdd(connectionId, DateTimeOffset.MinValue);
        if (last != DateTimeOffset.MinValue && now - last < _options.GenerateQuestionsCooldown)
        {
            throw new HubException("QuestionsRateExceeded");
        }

        _questionsState[connectionId] = now;
    }

    public void Reset(string connectionId)
    {
        _audioStates.TryRemove(connectionId, out _);
        _questionsState.TryRemove(connectionId, out _);
    }

    private sealed class AudioRateState
    {
        public AudioRateState(DateTimeOffset windowStart)
        {
            WindowStart = windowStart;
        }

        public DateTimeOffset WindowStart { get; set; }

        public int Count { get; set; }
    }
}
