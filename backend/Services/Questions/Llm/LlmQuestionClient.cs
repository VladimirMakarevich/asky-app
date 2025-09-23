using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AskyBackend.Contracts;
using AskyBackend.Options;
using AskyBackend.Services.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AskyBackend.Services.Questions.Llm;

public sealed class LlmQuestionClient : ILlmQuestionClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;
    private readonly LlmServiceOptions _options;
    private readonly ILogger<LlmQuestionClient> _logger;

    public LlmQuestionClient(HttpClient httpClient, IOptions<LlmServiceOptions> options, ILogger<LlmQuestionClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    public async Task<IReadOnlyList<QuestionItem>> GenerateAsync(
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken)
    {
        var payload = new LlmQuestionRequest
        {
            Topic = options.Topic,
            RollingSummary = string.IsNullOrWhiteSpace(snapshot.RollingSummary) ? null : snapshot.RollingSummary,
            LastWindow = string.IsNullOrWhiteSpace(snapshot.LastWindow) ? null : snapshot.LastWindow,
            KnownFacts = snapshot.KnownFacts.Count == 0 ? null : snapshot.KnownFacts,
            AskedRecently = snapshot.AskedRecently.Count == 0 ? null : snapshot.AskedRecently,
            PreferredStyle = options.PreferredStyle,
            MaxCandidates = _options.MaxCandidates
        };

        var retries = Math.Max(1, _options.MaxRetries);
        for (var attempt = 1; attempt <= retries; attempt++)
        {
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(_options.Timeout);

                using var request = new HttpRequestMessage(HttpMethod.Post, "llm/questions")
                {
                    Content = JsonContent.Create(payload, options: SerializerOptions)
                };

                if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                }

                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    var error = await response.Content.ReadAsStringAsync(linkedCts.Token).ConfigureAwait(false);
                    _logger.LogWarning("LLM request failed with status {Status} on attempt {Attempt}. Body: {Body}", response.StatusCode, attempt, error);
                    ThrowIfFinalAttempt(attempt, retries, response.StatusCode, error);
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var content = await response.Content.ReadFromJsonAsync<LlmQuestionResponse>(SerializerOptions, linkedCts.Token).ConfigureAwait(false);
                if (content?.Candidates is null)
                {
                    _logger.LogWarning("LLM response did not contain candidates");
                    return Array.Empty<QuestionItem>();
                }

                return content.Candidates.Select(candidate => new QuestionItem(
                    candidate.Text ?? string.Empty,
                    candidate.Tags,
                    candidate.Confidence,
                    candidate.Novelty)).ToArray();
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "LLM request timed out on attempt {Attempt}", attempt);
                if (attempt == retries)
                {
                    throw;
                }
            }
            catch (Exception ex) when (attempt < retries)
            {
                _logger.LogWarning(ex, "LLM request failed on attempt {Attempt}", attempt);
            }
        }

        return Array.Empty<QuestionItem>();
    }

    private static void ThrowIfFinalAttempt(int attempt, int retries, System.Net.HttpStatusCode statusCode, string? body)
    {
        if (attempt == retries)
        {
            throw new HttpRequestException($"LLM request failed with status {(int)statusCode}: {body}");
        }
    }

    private sealed class LlmQuestionRequest
    {
        public string? Topic { get; set; }

        public string? RollingSummary { get; set; }

        public string? LastWindow { get; set; }

        public IReadOnlyDictionary<string, string>? KnownFacts { get; set; }

        public IReadOnlyCollection<string>? AskedRecently { get; set; }

        public string? PreferredStyle { get; set; }

        public int MaxCandidates { get; set; }
    }

    private sealed class LlmQuestionResponse
    {
        public IReadOnlyList<Candidate>? Candidates { get; set; }

        public sealed class Candidate
        {
            public string? Text { get; set; }

            public IReadOnlyCollection<string>? Tags { get; set; }

            public double? Confidence { get; set; }

            public double? Novelty { get; set; }
        }
    }
}
