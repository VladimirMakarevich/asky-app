namespace AskyBackend.Services;

public interface IThrottlingService
{
    void CheckAudioFrame(string connectionId, int payloadSize);

    void CheckGenerateQuestions(string connectionId);

    void Reset(string connectionId);
}
