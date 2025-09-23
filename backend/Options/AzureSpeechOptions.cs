namespace AskyBackend.Options;

public sealed class AzureSpeechOptions
{
    public string SubscriptionKey { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Language { get; set; } = "en-US";

    /// <summary>
    /// Maximum number of audio frames buffered before applying backpressure.
    /// Each frame contains ~20-40ms of audio.
    /// </summary>
    public int MaxQueuedFrames { get; set; } = 256;

    /// <summary>
    /// PCM sample rate expected by Azure Speech (Hz).
    /// </summary>
    public int SampleRate { get; set; } = 16_000;

    /// <summary>
    /// Bits per sample (PCM16 by default).
    /// </summary>
    public int BitsPerSample { get; set; } = 16;

    /// <summary>
    /// Number of audio channels (mono = 1).
    /// </summary>
    public int Channels { get; set; } = 1;

    /// <summary>
    /// Whether to request detailed output (includes word-level timestamps).
    /// </summary>
    public bool EnableDetailedOutput { get; set; } = false;
}
