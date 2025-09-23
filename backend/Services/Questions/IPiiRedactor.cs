using AskyBackend.Contracts;
using AskyBackend.Services.Context;

namespace AskyBackend.Services.Questions;

public interface IPiiRedactor
{
    (ConversationContextSnapshot Snapshot, GenerateQuestionsOptions Options) Redact(
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options);
}
