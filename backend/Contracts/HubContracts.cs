namespace AskyBackend.Contracts;

/// <summary>
/// PCM16 audio frame payload delivered from the client.
/// </summary>
/// <param name="Sequence">Sequential number supplied by the client.</param>
/// <param name="Timestamp">Client-side timestamp (in milliseconds since session start).</param>
/// <param name="Payload">Raw PCM16 mono bytes (20-40ms).</param>
public sealed record AudioFrameDto(int Sequence, double Timestamp, byte[] Payload);

/// <summary>
/// Options supplied when the client requests question generation.
/// </summary>
public sealed record GenerateQuestionsOptions(
    string? Topic = null,
    string? PreferredStyle = null,
    bool ForceRefresh = false);

/// <summary>
/// Question item returned to the client.
/// </summary>
public sealed record QuestionItem(
    string Text,
    IReadOnlyCollection<string>? Tags = null,
    double? Confidence = null,
    double? Novelty = null);

/// <summary>
/// Partial transcript chunk emitted while the user is speaking.
/// </summary>
public sealed record PartialTranscript(
    string Text,
    long Offset,
    long Duration);

/// <summary>
/// Finalized transcript chunk emitted once ASR confirms the utterance.
/// </summary>
public sealed record FinalTranscript(
    string Text,
    long Offset,
    long Duration,
    IReadOnlyCollection<string>? Facts = null);
