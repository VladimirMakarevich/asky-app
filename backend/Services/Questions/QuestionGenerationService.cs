using System.Diagnostics;
using AskyBackend.Contracts;
using AskyBackend.Services.Context;
using AskyBackend.Services.Questions.Llm;
using AskyBackend.Telemetry;
using Microsoft.Extensions.Logging;

namespace AskyBackend.Services.Questions;

public sealed class QuestionGenerationService : IQuestionGenerationService
{
    private readonly ILlmQuestionClient _llmQuestionClient;
    private readonly IQuestionFallbackGenerator _fallbackGenerator;
    private readonly IPiiRedactor _piiRedactor;
    private readonly ITelemetryRecorder _telemetry;
    private readonly ILogger<QuestionGenerationService> _logger;

    public QuestionGenerationService(
        ILlmQuestionClient llmQuestionClient,
        IQuestionFallbackGenerator fallbackGenerator,
        IPiiRedactor piiRedactor,
        ITelemetryRecorder telemetry,
        ILogger<QuestionGenerationService> logger)
    {
        _llmQuestionClient = llmQuestionClient;
        _fallbackGenerator = fallbackGenerator;
        _piiRedactor = piiRedactor;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<IReadOnlyList<QuestionItem>> GenerateAsync(
        string connectionId,
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken)
    {
        var (sanitizedSnapshot, sanitizedOptions) = _piiRedactor.Redact(snapshot, options);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _llmQuestionClient.GenerateAsync(sanitizedSnapshot, sanitizedOptions, cancellationToken).ConfigureAwait(false);
            if (result.Count > 0)
            {
                stopwatch.Stop();
                _telemetry.RecordLlmLatency(stopwatch.Elapsed, usedFallback: false);
                _logger.LogDebug("LLM returned {Count} candidates for {ConnectionId}", result.Count, connectionId);
                return result;
            }

            _logger.LogInformation("LLM returned no candidates. Falling back to 4W1H template for {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM question generation failed for {ConnectionId}", connectionId);
            _telemetry.RecordError("llm", ex.GetType().Name);
        }

        stopwatch.Stop();
        _telemetry.RecordLlmLatency(stopwatch.Elapsed, usedFallback: true);
        var fallback = _fallbackGenerator.Generate(snapshot, options);
        _logger.LogDebug("Generated {Count} fallback questions for {ConnectionId}", fallback.Count, connectionId);
        return fallback;
    }
}
