using System.Collections.Generic;
using System.Text.RegularExpressions;
using AskyBackend.Contracts;
using AskyBackend.Options;
using AskyBackend.Services.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AskyBackend.Services.Summarization;

/// <summary>
/// Lightweight summarizer that derives a rolling summary from the latest transcripts.
/// This is a placeholder until an LLM-backed implementation is introduced.
/// </summary>
public sealed class HeuristicRollingSummaryService : IRollingSummaryService
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly IConversationContextStore _contextStore;
    private readonly RollingSummaryOptions _options;
    private readonly ILogger<HeuristicRollingSummaryService> _logger;

    public HeuristicRollingSummaryService(
        IConversationContextStore contextStore,
        IOptions<RollingSummaryOptions> options,
        ILogger<HeuristicRollingSummaryService> logger)
    {
        _contextStore = contextStore;
        _options = options.Value;
        _logger = logger;
    }

    public async Task UpdateAsync(string connectionId, FinalTranscript final, CancellationToken cancellationToken)
    {
        var snapshot = await _contextStore.GetSnapshotAsync(connectionId, cancellationToken);

        var pieces = new List<string>(capacity: 3);
        if (!string.IsNullOrWhiteSpace(snapshot.RollingSummary))
        {
            pieces.Add(snapshot.RollingSummary);
        }

        if (!string.IsNullOrWhiteSpace(final.Text))
        {
            pieces.Add(final.Text);
        }

        if (pieces.Count == 0 && !string.IsNullOrWhiteSpace(snapshot.LastWindow))
        {
            pieces.Add(snapshot.LastWindow);
        }

        if (pieces.Count == 0)
        {
            _logger.LogDebug("No content to summarize for {ConnectionId}", connectionId);
            return;
        }

        var normalized = Normalize(string.Join(' ', pieces));
        var trimmed = TrimToLimit(normalized, _options.MaxSummaryCharacters);
        await _contextStore.UpdateRollingSummaryAsync(connectionId, trimmed, cancellationToken);
    }

    private static string Normalize(string text) =>
        WhitespaceRegex.Replace(text.Trim(), " ");

    private static string TrimToLimit(string text, int limit)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= limit)
        {
            return text;
        }

        var start = text.Length - limit;
        var candidate = text.Substring(start);
        var firstSpace = candidate.IndexOf(' ');
        if (firstSpace > 0)
        {
            candidate = candidate.Substring(firstSpace + 1);
        }

        return candidate.TrimStart();
    }
}
