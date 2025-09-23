using AskyBackend.Contracts;

namespace AskyBackend.Services.Context;

public interface IConversationContextStore
{
    ValueTask CreateOrResetAsync(string connectionId, CancellationToken cancellationToken);

    ValueTask RegisterFinalTranscriptAsync(string connectionId, FinalTranscript transcript, CancellationToken cancellationToken);

    ValueTask UpdateRollingSummaryAsync(string connectionId, string summary, CancellationToken cancellationToken);

    ValueTask RegisterAskedQuestionAsync(string connectionId, string question, CancellationToken cancellationToken);

    ValueTask UpsertKnownFactsAsync(string connectionId, IReadOnlyDictionary<string, string> facts, CancellationToken cancellationToken);

    ValueTask<ConversationContextSnapshot> GetSnapshotAsync(string connectionId, CancellationToken cancellationToken);

    ValueTask RemoveAsync(string connectionId, CancellationToken cancellationToken);
}

public sealed record ConversationContextSnapshot(
    string RollingSummary,
    string LastWindow,
    IReadOnlyDictionary<string, string> KnownFacts,
    IReadOnlyCollection<string> AskedRecently);
