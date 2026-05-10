using Avalonia;
using HandwritingPad.Models;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace HandwritingPad.Services;

public sealed class PaddleOcrOnnxRecognizer : IHandwritingRecognizer, IDisposable
{
    private readonly PaddleOcrOnnxOptions _options;
    private readonly InkOcrImageRenderer _renderer = new();
    private readonly PaddleOcrCtcDecoder _decoder;
    private readonly RecognitionCandidateReranker _reranker = new();
    private readonly PunctuationGeometryRecognizer _punctuationRecognizer = new();
    private readonly CjkComponentCorrector _cjkComponentCorrector = new();
    private readonly InferenceSession _session;

    private readonly string _inputName;
    private readonly int _modelInputHeight;
    private readonly int _modelInputWidth;
    private readonly bool _dynamicWidth;

    public OcrModelProfile ModelProfile => _options.ModelProfile;
    public OcrExecutionProvider RequestedProvider => _options.ExecutionProvider;
    public string EffectiveProvider { get; }
    public string ProviderMessage { get; }

    public PaddleOcrOnnxRecognizer(PaddleOcrOnnxOptions? options = null)
    {
        _options =
            options
            ?? PaddleOcrOnnxOptions.Create(OcrModelProfile.高精度, OcrExecutionProvider.Cpu);

        OcrRuntimeEnvironmentVerifier.VerifyOrThrow(_options);

        using var sessionOptions = OnnxSessionOptionsFactory.Create(
            _options,
            out var effectiveProvider,
            out var providerMessage
        );

        EffectiveProvider = effectiveProvider;
        ProviderMessage = providerMessage;

        _session = new InferenceSession(_options.ModelPath, sessionOptions);
        _decoder = new PaddleOcrCtcDecoder(_options.DictionaryPath);

        var input = _session.InputMetadata.First();
        _inputName = input.Key;

        var dims = input.Value.Dimensions;

        _modelInputHeight = dims.Length >= 4 && dims[2] > 0 ? dims[2] : _options.DefaultInputHeight;

        if (dims.Length >= 4 && dims[3] > 0)
        {
            _modelInputWidth = dims[3];
            _dynamicWidth = false;
        }
        else
        {
            _modelInputWidth = _options.DynamicMaxInputWidth;
            _dynamicWidth = true;
        }
    }

    public async Task<IReadOnlyList<RecognitionCandidate>> RecognizeAsync(
        IReadOnlyList<InkStroke> strokes,
        Size canvasSize,
        RecognitionInputMode inputMode,
        CancellationToken cancellationToken
    )
    {
        if (strokes.Count == 0 || strokes.Sum(x => x.Points.Count) == 0)
        {
            return Array.Empty<RecognitionCandidate>();
        }

        return await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var layout = InkLayoutAnalyzer.Analyze(strokes, canvasSize);
                var rawCandidates = new List<RecognitionCandidate>();

                rawCandidates.AddRange(
                    _punctuationRecognizer.Recognize(strokes, canvasSize, inputMode, layout)
                );

                var bestPunctuation = rawCandidates
                    .Where(x => IsPunctuationCandidate(x.Text))
                    .OrderByDescending(x => x.Confidence)
                    .FirstOrDefault();

                if (
                    inputMode is RecognitionInputMode.Punctuation or RecognitionInputMode.Symbol
                    && bestPunctuation is not null
                    && bestPunctuation.Confidence >= 0.86
                )
                {
                    return _reranker.Rerank(rawCandidates, inputMode, layout, _options.Enhancement);
                }

                var renderVariants = _options.Enhancement.EnableMultiRenderTta
                    ? InkOcrImageRenderer.CreateVariants(layout, _options.EnableDebugImages)
                    : new[]
                    {
                        new InkOcrRenderOptions
                        {
                            Mode = layout.IsLikelySingleCjkCharacter
                                ? InkRenderMode.SquareCjk
                                : InkRenderMode.Normal,
                            TargetHeight = layout.IsLikelySingleCjkCharacter ? 224 : 192,
                            Padding = layout.IsLikelySingleCjkCharacter ? 72f : 56f,
                            StrokeWidthScale = 0.072f,
                            SmoothStroke = true,
                            ForceSquareCanvas = layout.IsLikelySingleCjkCharacter,
                            SaveDebugImage = _options.EnableDebugImages,
                        },
                    };

                foreach (var variant in renderVariants)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using var bitmap = _renderer.RenderCroppedLineBitmap(strokes, variant);

                    var candidates = RecognizeBitmap(bitmap, cancellationToken);

                    rawCandidates.AddRange(candidates);
                }

                cancellationToken.ThrowIfCancellationRequested();

                rawCandidates.AddRange(
                    _cjkComponentCorrector.CreateCorrectionCandidates(
                        rawCandidates,
                        layout,
                        inputMode
                    )
                );

                return _reranker.Rerank(rawCandidates, inputMode, layout, _options.Enhancement);
            },
            cancellationToken
        );
    }

    private IReadOnlyList<RecognitionCandidate> RecognizeBitmap(
        SKBitmap source,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputWidth = ResolveInputWidth(source.Width, source.Height);

        using var normalizedBitmap = ResizeAndPadForRecognition(
            source,
            _modelInputHeight,
            inputWidth
        );

        var tensor = CreateInputTensor(normalizedBitmap);

        var input = NamedOnnxValue.CreateFromTensor(_inputName, tensor);
        using var results = _session.Run(new[] { input });

        cancellationToken.ThrowIfCancellationRequested();

        var outputTensor = results.First().AsTensor<float>();
        var outputDims = outputTensor.Dimensions.ToArray();
        var outputValues = outputTensor.ToArray();

        var (timeSteps, classCount) = ResolveOutputShape(outputDims);

        var candidates = new List<RecognitionCandidate>();

        var greedy = _decoder.DecodeGreedy(outputValues, timeSteps, classCount);

        if (!string.IsNullOrWhiteSpace(greedy.Text))
        {
            candidates.Add(greedy);
        }

        if (_options.Enhancement.EnableBeamSearch)
        {
            var beamCandidates = _decoder.DecodeBeamSearch(
                outputValues,
                timeSteps,
                classCount,
                _options.Enhancement.BeamTopK,
                _options.Enhancement.BeamWidth,
                _options.Enhancement.MaxCandidateCount
            );

            candidates.AddRange(beamCandidates);
        }

        return candidates;
    }

    private (int TimeSteps, int ClassCount) ResolveOutputShape(int[] dims)
    {
        if (dims.Length == 3)
        {
            return (dims[1], dims[2]);
        }

        if (dims.Length == 2)
        {
            return (dims[0], dims[1]);
        }

        throw new InvalidOperationException(
            $"暂不支持该 OCR 模型输出维度：{string.Join(",", dims)}"
        );
    }

    private int ResolveInputWidth(int sourceWidth, int sourceHeight)
    {
        if (!_dynamicWidth)
        {
            return _modelInputWidth;
        }

        var ratio = sourceWidth / Math.Max(1.0, sourceHeight);
        var rawWidth = (int)Math.Ceiling(_modelInputHeight * ratio);
        var alignedWidth = AlignTo(rawWidth, 32);

        return Math.Clamp(alignedWidth, _options.DefaultInputWidth, _options.DynamicMaxInputWidth);
    }

    private static int AlignTo(int value, int alignment)
    {
        return ((value + alignment - 1) / alignment) * alignment;
    }

    private static SKBitmap ResizeAndPadForRecognition(
        SKBitmap source,
        int targetHeight,
        int targetWidth
    )
    {
        var scale = Math.Min(
            targetWidth / (double)source.Width,
            targetHeight / (double)source.Height
        );

        var resizedWidth = Math.Max(1, (int)Math.Round(source.Width * scale));
        var resizedHeight = Math.Max(1, (int)Math.Round(source.Height * scale));

        using var resized = source.Resize(
            new SKImageInfo(resizedWidth, resizedHeight),
            SKFilterQuality.High
        );

        if (resized is null)
        {
            throw new InvalidOperationException("OCR 输入图 resize 失败。");
        }

        var output = new SKBitmap(
            targetWidth,
            targetHeight,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );

        using var canvas = new SKCanvas(output);
        canvas.Clear(SKColors.White);

        var x = 0;
        var y = (targetHeight - resizedHeight) / 2;

        canvas.DrawBitmap(resized, x, y);

        return output;
    }

    private static DenseTensor<float> CreateInputTensor(SKBitmap bitmap)
    {
        var height = bitmap.Height;
        var width = bitmap.Width;

        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var color = bitmap.GetPixel(x, y);

                tensor[0, 0, y, x] = NormalizeChannel(color.Red);
                tensor[0, 1, y, x] = NormalizeChannel(color.Green);
                tensor[0, 2, y, x] = NormalizeChannel(color.Blue);
            }
        }

        return tensor;
    }

    private static float NormalizeChannel(byte value)
    {
        return value / 127.5f - 1.0f;
    }

    private static bool IsPunctuationCandidate(string text)
    {
        return text.Length is >= 1 and <= 3
            && text.All(c =>
                c
                    is '，'
                        or ','
                        or '。'
                        or '.'
                        or '、'
                        or '！'
                        or '!'
                        or '？'
                        or '?'
                        or '：'
                        or ':'
                        or '；'
                        or ';'
                        or '—'
                        or '-'
                        or '…'
                        or '·'
            );
    }

    public void Dispose()
    {
        _session.Dispose();
    }
}
