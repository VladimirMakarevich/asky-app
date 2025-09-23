using AskyBackend.Contracts;

namespace AskyBackend.Services.Questions;

public interface IQuestionGenerationService
{
    Task<IReadOnlyList<QuestionItem>> GenerateAsync(
        string connectionId,
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken);
}
