namespace AskyBackend.Telemetry;

public interface ITelemetryRecorder
{
    void RecordAsrLatency(TimeSpan latency);

    void RecordLlmLatency(TimeSpan latency, bool usedFallback);

    void RecordError(string component, string reason);
}
