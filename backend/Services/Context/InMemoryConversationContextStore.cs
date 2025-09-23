using System.Collections.Concurrent;
using System.Text;
using System.Linq;
using AskyBackend.Contracts;
using AskyBackend.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AskyBackend.Services.Context;

public sealed class InMemoryConversationContextStore : IConversationContextStore
{
    private readonly ConcurrentDictionary<string, ConversationContextState> _state = new();
    private readonly ConversationContextOptions _options;
    private readonly ILogger<InMemoryConversationContextStore> _logger;

    public InMemoryConversationContextStore(IOptions<ConversationContextOptions> options, ILogger<InMemoryConversationContextStore> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public ValueTask CreateOrResetAsync(string connectionId, CancellationToken cancellationToken)
    {
        _state[connectionId] = new ConversationContextState();
        _logger.LogDebug("Initialized context for {ConnectionId}", connectionId);
        return ValueTask.CompletedTask;
    }

    public ValueTask RegisterFinalTranscriptAsync(string connectionId, FinalTranscript transcript, CancellationToken cancellationToken)
    {
        if (!_state.TryGetValue(connectionId, out var ctx))
        {
            return ValueTask.CompletedTask;
        }

        lock (ctx.SyncRoot)
        {
            ctx.Transcripts.Add(new TranscriptRecord(transcript.Text, DateTimeOffset.UtcNow, transcript.Offset, transcript.Duration));
            TrimWindow(ctx);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateRollingSummaryAsync(string connectionId, string summary, CancellationToken cancellationToken)
    {
        if (!_state.TryGetValue(connectionId, out var ctx))
        {
            return ValueTask.CompletedTask;
        }

        lock (ctx.SyncRoot)
        {
            ctx.RollingSummary = summary;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask RegisterAskedQuestionAsync(string connectionId, string question, CancellationToken cancellationToken)
    {
        if (!_state.TryGetValue(connectionId, out var ctx))
        {
            return ValueTask.CompletedTask;
        }

        lock (ctx.SyncRoot)
        {
            if (ctx.AskedRecently.Contains(question))
            {
                // Move to the end to reflect recent usage.
                ctx.AskedRecently.Remove(question);
            }

            ctx.AskedRecently.AddLast(question);

            while (ctx.AskedRecently.Count > _options.MaxAskedRecently)
            {
                ctx.AskedRecently.RemoveFirst();
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask UpsertKnownFactsAsync(string connectionId, IReadOnlyDictionary<string, string> facts, CancellationToken cancellationToken)
    {
        if (!_state.TryGetValue(connectionId, out var ctx) || facts.Count == 0)
        {
            return ValueTask.CompletedTask;
        }

        lock (ctx.SyncRoot)
        {
            foreach (var (key, value) in facts)
            {
                if (ctx.KnownFacts.ContainsKey(key))
                {
                    ctx.KnownFacts[key] = value;
                    continue;
                }

                ctx.KnownFacts[key] = value;
                ctx.FactInsertionOrder.Enqueue(key);
            }

            while (ctx.KnownFacts.Count > _options.MaxKnownFacts && ctx.FactInsertionOrder.TryDequeue(out var oldest))
            {
                ctx.KnownFacts.Remove(oldest);
            }
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<ConversationContextSnapshot> GetSnapshotAsync(string connectionId, CancellationToken cancellationToken)
    {
        if (!_state.TryGetValue(connectionId, out var ctx))
        {
            return ValueTask.FromResult(new ConversationContextSnapshot(string.Empty, string.Empty, new Dictionary<string, string>(), Array.Empty<string>()));
        }

        lock (ctx.SyncRoot)
        {
            TrimWindow(ctx);
            var windowText = BuildWindowText(ctx.Transcripts, _options.MaxWindowCharacters);
            var asked = ctx.AskedRecently.ToArray();
            var facts = new Dictionary<string, string>(ctx.KnownFacts, StringComparer.OrdinalIgnoreCase);
            return ValueTask.FromResult(new ConversationContextSnapshot(ctx.RollingSummary, windowText, facts, asked));
        }
    }

    public ValueTask RemoveAsync(string connectionId, CancellationToken cancellationToken)
    {
        _state.TryRemove(connectionId, out _);
        _logger.LogDebug("Removed context for {ConnectionId}", connectionId);
        return ValueTask.CompletedTask;
    }

    private void TrimWindow(ConversationContextState ctx)
    {
        var cutoff = DateTimeOffset.UtcNow - _options.SlidingWindow;
        ctx.Transcripts.RemoveAll(t => t.ReceivedAt < cutoff);

        var totalChars = ctx.Transcripts.Sum(t => t.Text.Length);
        while (totalChars > _options.MaxWindowCharacters && ctx.Transcripts.Count > 0)
        {
            totalChars -= ctx.Transcripts[0].Text.Length;
            ctx.Transcripts.RemoveAt(0);
        }
    }

    private static string BuildWindowText(List<TranscriptRecord> transcripts, int maxChars)
    {
        if (transcripts.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var record in transcripts)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(record.Text);
            if (builder.Length >= maxChars)
            {
                break;
            }
        }

        if (builder.Length > maxChars)
        {
            return builder.ToString(0, maxChars);
        }

        return builder.ToString();
    }

    private sealed class ConversationContextState
    {
        public List<TranscriptRecord> Transcripts { get; } = new();

        public string RollingSummary { get; set; } = string.Empty;

        public LinkedList<string> AskedRecently { get; } = new();

        public Dictionary<string, string> KnownFacts { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Queue<string> FactInsertionOrder { get; } = new();

        public object SyncRoot { get; } = new();
    }

    private sealed record TranscriptRecord(string Text, DateTimeOffset ReceivedAt, long Offset, long Duration);
}
