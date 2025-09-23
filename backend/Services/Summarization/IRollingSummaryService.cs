using AskyBackend.Contracts;

namespace AskyBackend.Services.Summarization;

public interface IRollingSummaryService
{
    Task UpdateAsync(string connectionId, FinalTranscript final, CancellationToken cancellationToken);
}
