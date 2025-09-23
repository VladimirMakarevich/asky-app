using AskyBackend.Contracts;

namespace AskyBackend.Services;

public interface IConversationSessionManager
{
    Task RegisterConnectionAsync(string connectionId, CancellationToken cancellationToken);

    Task HandleAudioFrameAsync(string connectionId, AudioFrameDto frame, CancellationToken cancellationToken);

    Task StopStreamAsync(string connectionId, CancellationToken cancellationToken);

    Task<IReadOnlyList<QuestionItem>> GenerateQuestionsAsync(
        string connectionId,
        GenerateQuestionsOptions options,
        CancellationToken cancellationToken);

    Task CleanupConnectionAsync(string connectionId);
}
