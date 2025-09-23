using AskyBackend.Contracts;
using AskyBackend.Services.Context;

namespace AskyBackend.Services.Questions.Llm;

public sealed class NullLlmQuestionClient : ILlmQuestionClient
{
    public Task<IReadOnlyList<QuestionItem>> GenerateAsync(
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<QuestionItem>>(Array.Empty<QuestionItem>());
}
