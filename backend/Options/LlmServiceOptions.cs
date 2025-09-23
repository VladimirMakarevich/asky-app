namespace AskyBackend.Options;

public sealed class LlmServiceOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public int MaxCandidates { get; set; } = 20;

    public int MaxRetries { get; set; } = 3;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(12);
}
