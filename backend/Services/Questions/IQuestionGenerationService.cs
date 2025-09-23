using AskyBackend.Contracts;
using AskyBackend.Services.Context;

namespace AskyBackend.Services.Questions;

public interface IQuestionGenerationService
{
    Task<IReadOnlyList<QuestionItem>> GenerateAsync(
        string connectionId,
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken);
}
