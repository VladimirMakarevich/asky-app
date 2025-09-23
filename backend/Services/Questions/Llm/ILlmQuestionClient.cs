using AskyBackend.Contracts;
using AskyBackend.Services.Context;

namespace AskyBackend.Services.Questions.Llm;

public interface ILlmQuestionClient
{
    Task<IReadOnlyList<QuestionItem>> GenerateAsync(
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken);
}
