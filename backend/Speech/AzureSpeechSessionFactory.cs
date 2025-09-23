using System.Threading.Channels;
using AskyBackend.Contracts;
using AskyBackend.Options;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AskyBackend.Speech;

public sealed class AzureSpeechSessionFactory : ISpeechSessionFactory
{
    private readonly AzureSpeechOptions _options;
    private readonly ILogger<AzureSpeechSessionFactory> _logger;

    public AzureSpeechSessionFactory(IOptions<AzureSpeechOptions> options, ILogger<AzureSpeechSessionFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.SubscriptionKey) &&
        !string.IsNullOrWhiteSpace(_options.Region);

    public async Task<ISpeechSession> CreateAsync(string connectionId, ISpeechObserver observer, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Azure Speech is not configured. Provide SubscriptionKey and Region.");
        }

        var speechConfig = SpeechConfig.FromSubscription(_options.SubscriptionKey, _options.Region);
        speechConfig.SpeechRecognitionLanguage = _options.Language;
        if (_options.EnableDetailedOutput)
        {
            speechConfig.OutputFormat = OutputFormat.Detailed;
        }

        var audioFormat = AudioStreamFormat.GetWaveFormatPCM(
            (uint)_options.SampleRate,
            (byte)_options.BitsPerSample,
            (byte)_options.Channels);

        var pushStream = AudioInputStream.CreatePushStream(audioFormat);
        var audioConfig = AudioConfig.FromStreamInput(pushStream);
        var recognizer = new SpeechRecognizer(speechConfig, audioConfig);

        var session = new AzureSpeechSession(
            connectionId,
            observer,
            recognizer,
            pushStream,
            _options,
            _logger);

        await session.StartAsync(cancellationToken).ConfigureAwait(false);
        return session;
    }

    private sealed class AzureSpeechSession : ISpeechSession
    {
        private readonly string _connectionId;
        private readonly ISpeechObserver _observer;
        private readonly SpeechRecognizer _recognizer;
        private readonly PushAudioInputStream _pushStream;
        private readonly AzureSpeechOptions _options;
        private readonly ILogger _logger;
        private readonly Channel<AudioFrameDto> _channel;
        private readonly CancellationTokenSource _cts = new();
        private Task? _pumpTask;
        private int _isStopping;

        public AzureSpeechSession(
            string connectionId,
            ISpeechObserver observer,
            SpeechRecognizer recognizer,
            PushAudioInputStream pushStream,
            AzureSpeechOptions options,
            ILogger logger)
        {
            _connectionId = connectionId;
            _observer = observer;
            _recognizer = recognizer;
            _pushStream = pushStream;
            _options = options;
            _logger = logger;
            _channel = Channel.CreateBounded<AudioFrameDto>(new BoundedChannelOptions(Math.Max(8, options.MaxQueuedFrames))
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });

            _recognizer.Recognizing += RecognizingHandler;
            _recognizer.Recognized += RecognizedHandler;
            _recognizer.Canceled += CanceledHandler;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _pumpTask = Task.Run(PumpAudioAsync, CancellationToken.None);
            await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
            _logger.LogInformation("Azure Speech session started for {ConnectionId}", _connectionId);
        }

        public async ValueTask EnqueueFrameAsync(AudioFrameDto frame, CancellationToken cancellationToken)
        {
            if (_channel.Writer.TryWrite(frame))
            {
                return;
            }

            await _channel.Writer.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
        }

        public async ValueTask StopAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _isStopping, 1) == 1)
            {
                return;
            }

            _channel.Writer.TryComplete();
            _cts.Cancel();

            if (_pumpTask is not null)
            {
                await _pumpTask.ConfigureAwait(false);
            }

            await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            _logger.LogInformation("Azure Speech session stopped for {ConnectionId}", _connectionId);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await StopAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while stopping speech session for {ConnectionId}", _connectionId);
            }
            finally
            {
                _recognizer.Recognizing -= RecognizingHandler;
                _recognizer.Recognized -= RecognizedHandler;
                _recognizer.Canceled -= CanceledHandler;
                _recognizer.Dispose();
                _pushStream.Dispose();
                _cts.Dispose();
            }
        }

        private async Task PumpAudioAsync()
        {
            try
            {
                await foreach (var frame in _channel.Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
                {
                    _pushStream.Write(frame.Payload);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored: cancellation is expected during stop/dispose.
            }
            catch (Exception ex)
            {
                await _observer.OnErrorAsync(_connectionId, "AudioPumpFailed", ex, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _pushStream.Close();
            }
        }

        private void RecognizingHandler(object? sender, SpeechRecognitionEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(e.Result.Text))
            {
                return;
            }

            var partial = new PartialTranscript(
                e.Result.Text,
                Offset: ConvertTicksToMilliseconds(e.Result.OffsetInTicks),
                Duration: ConvertTicksToMilliseconds((long)e.Result.Duration.Ticks));

            _ = _observer.OnPartialAsync(_connectionId, partial, CancellationToken.None);
        }

        private void RecognizedHandler(object? sender, SpeechRecognitionEventArgs e)
        {
            if (e.Result.Reason != ResultReason.RecognizedSpeech || string.IsNullOrWhiteSpace(e.Result.Text))
            {
                return;
            }

            var final = new FinalTranscript(
                e.Result.Text,
                Offset: ConvertTicksToMilliseconds(e.Result.OffsetInTicks),
                Duration: ConvertTicksToMilliseconds((long)e.Result.Duration.Ticks));

            _ = _observer.OnFinalAsync(_connectionId, final, CancellationToken.None);
        }

        private void CanceledHandler(object? sender, SpeechRecognitionCanceledEventArgs e)
        {
            var exception = e.ErrorCode == CancellationErrorCode.NoError
                ? new InvalidOperationException($"Speech recognition canceled: {e.Reason}")
                : new InvalidOperationException($"Speech recognition error ({e.ErrorCode}): {e.ErrorDetails}");

            _ = _observer.OnErrorAsync(_connectionId, "SpeechCanceled", exception, CancellationToken.None);
        }

        private static long ConvertTicksToMilliseconds(long ticks) =>
            (long)TimeSpan.FromTicks(ticks).TotalMilliseconds;
    }
}
