using AskyBackend.Contracts;
using AskyBackend.Services.Context;

namespace AskyBackend.Services.Questions;

/// <summary>
/// Temporary placeholder that returns an empty list of questions.
/// Will be replaced by the LLM-backed implementation.
/// </summary>
public sealed class NoopQuestionGenerationService : IQuestionGenerationService
{
    public Task<IReadOnlyList<QuestionItem>> GenerateAsync(
        string connectionId,
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuestionItem>>(Array.Empty<QuestionItem>());
}
