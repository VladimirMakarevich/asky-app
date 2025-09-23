using AskyBackend.Contracts;
using AskyBackend.Services.Context;

namespace AskyBackend.Services.Questions;

public interface IQuestionFallbackGenerator
{
    IReadOnlyList<QuestionItem> Generate(
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options);
}
