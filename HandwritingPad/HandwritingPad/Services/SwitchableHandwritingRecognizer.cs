using Avalonia;
using HandwritingPad.Models;

namespace HandwritingPad.Services;

public sealed class SwitchableHandwritingRecognizer : IDisposable
{
    private readonly Dictionary<RecognizerKey, Lazy<PaddleOcrOnnxRecognizer>> _cache = new();
    private readonly object _lock = new();

    public Task<OcrRecognitionResult> RecognizeAsync(
        OcrModelProfile modelProfile,
        IReadOnlyList<InkStroke> strokes,
        Size canvasSize,
        CancellationToken cancellationToken)
    {
        var executionProvider = OnnxRuntimeBuildInfo.DefaultExecutionProvider;
        var recognizer = GetRecognizer(modelProfile, executionProvider);

        return RecognizeCoreAsync(
            recognizer,
            strokes,
            canvasSize,
            cancellationToken);
    }

    private static async Task<OcrRecognitionResult> RecognizeCoreAsync(
        PaddleOcrOnnxRecognizer recognizer,
        IReadOnlyList<InkStroke> strokes,
        Size canvasSize,
        CancellationToken cancellationToken)
    {
        var candidates = await recognizer.RecognizeAsync(
            strokes,
            canvasSize,
            RecognitionInputMode.General,
            cancellationToken);

        return new OcrRecognitionResult(
            candidates,
            recognizer.ModelProfile,
            recognizer.RequestedProvider,
            recognizer.EffectiveProvider);
    }

    private PaddleOcrOnnxRecognizer GetRecognizer(
        OcrModelProfile modelProfile,
        OcrExecutionProvider executionProvider)
    {
        var key = new RecognizerKey(modelProfile, executionProvider);

        lock (_lock)
        {
            if (!_cache.TryGetValue(key, out var lazy))
            {
                lazy = new Lazy<PaddleOcrOnnxRecognizer>(() =>
                    new PaddleOcrOnnxRecognizer(
                        PaddleOcrOnnxOptions.Create(
                            modelProfile,
                            executionProvider)));

                _cache[key] = lazy;
            }

            try
            {
                return lazy.Value;
            }
            catch
            {
                _cache.Remove(key);
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var item in _cache.Values)
            {
                if (item.IsValueCreated)
                {
                    item.Value.Dispose();
                }
            }

            _cache.Clear();
        }
    }

    private readonly record struct RecognizerKey(
        OcrModelProfile ModelProfile,
        OcrExecutionProvider ExecutionProvider);
}
