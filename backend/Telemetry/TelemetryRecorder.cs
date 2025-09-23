using System.Diagnostics.Metrics;

namespace AskyBackend.Telemetry;

public sealed class TelemetryRecorder : ITelemetryRecorder, IDisposable
{
    private readonly Meter _meter = new("asky-backend", "1.0.0");
    private readonly Histogram<double> _asrLatency;
    private readonly Histogram<double> _llmLatency;
    private readonly Counter<long> _llmFallbacks;
    private readonly Counter<long> _errorCounter;

    public TelemetryRecorder()
    {
        _asrLatency = _meter.CreateHistogram<double>("asr_latency_ms");
        _llmLatency = _meter.CreateHistogram<double>("llm_latency_ms");
        _llmFallbacks = _meter.CreateCounter<long>("llm_fallbacks");
        _errorCounter = _meter.CreateCounter<long>("backend_errors");
    }

    public void RecordAsrLatency(TimeSpan latency)
    {
        _asrLatency.Record(latency.TotalMilliseconds);
    }

    public void RecordLlmLatency(TimeSpan latency, bool usedFallback)
    {
        _llmLatency.Record(latency.TotalMilliseconds);
        if (usedFallback)
        {
            _llmFallbacks.Add(1);
        }
    }

    public void RecordError(string component, string reason)
    {
        _errorCounter.Add(1, new KeyValuePair<string, object?>("component", component), new KeyValuePair<string, object?>("reason", reason));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
