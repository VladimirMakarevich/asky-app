namespace AskyBackend.Options;

public sealed class ThrottlingOptions
{
    public int MaxAudioFramesPerSecond { get; set; } = 50;

    public int MaxAudioPayloadBytes { get; set; } = 4096;

    public TimeSpan GenerateQuestionsCooldown { get; set; } = TimeSpan.FromSeconds(5);
}
