namespace AskyBackend.Options;

public sealed class ConversationContextOptions
{
    /// <summary>
    /// Time span that defines the sliding window for the latest transcripts.
    /// </summary>
    public TimeSpan SlidingWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum amount of text preserved in the sliding window to avoid unbounded growth.
    /// </summary>
    public int MaxWindowCharacters { get; set; } = 4_000;

    /// <summary>
    /// Maximum number of questions tracked for duplicate suppression.
    /// </summary>
    public int MaxAskedRecently { get; set; } = 50;

    /// <summary>
    /// Maximum number of known facts to keep in memory.
    /// </summary>
    public int MaxKnownFacts { get; set; } = 64;
}
