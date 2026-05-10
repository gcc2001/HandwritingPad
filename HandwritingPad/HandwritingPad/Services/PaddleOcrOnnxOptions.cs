namespace HandwritingPad.Services;

public sealed record PaddleOcrOnnxOptions
{
    public OcrModelProfile ModelProfile { get; init; } = OcrModelProfile.高精度;

    public OcrExecutionProvider ExecutionProvider { get; init; } =
        OnnxRuntimeBuildInfo.DefaultExecutionProvider;

    public int DeviceId { get; init; } = 0;

    public string ModelPath { get; init; } = GetDefaultModelPath(OcrModelProfile.高精度);

    // Server 和 Mobile 共用同一套 PP-OCRv5 字典，覆盖简中、繁中、英文等字符集。
    public string DictionaryPath { get; init; } = GetSharedDictionaryPath();

    public int DefaultInputHeight { get; init; } = 48;
    public int DefaultInputWidth { get; init; } = 320;
    public int DynamicMaxInputWidth { get; init; } = 960;
    public int ThreadCount { get; init; } = 2;
    public bool EnableDebugImages { get; init; } = false;
    public bool VerifyModelSha256 { get; init; } = true;
    public bool DisableThreadSpinning { get; init; } = false;
    public OcrEnhancementOptions Enhancement { get; init; } =
        OcrEnhancementOptions.LowComputeDefault;

    public static PaddleOcrOnnxOptions Create(
        OcrModelProfile modelProfile,
        OcrExecutionProvider executionProvider,
        int threadCount = 2,
        int dynamicMaxInputWidth = 960,
        bool enableDebugImages = false
    )
    {
        return new PaddleOcrOnnxOptions
        {
            ModelProfile = modelProfile,
            ExecutionProvider = executionProvider,
            ModelPath = GetDefaultModelPath(modelProfile),
            DictionaryPath = GetSharedDictionaryPath(),
            ThreadCount = threadCount,
            DynamicMaxInputWidth = dynamicMaxInputWidth,
            EnableDebugImages = enableDebugImages,
            Enhancement = OcrEnhancementOptions.LowComputeDefault,
        };
    }

    public static PaddleOcrOnnxOptions CreateDefault()
    {
        return Create(OcrModelProfile.高精度, OnnxRuntimeBuildInfo.DefaultExecutionProvider);
    }

    public static string GetDefaultModelPath(OcrModelProfile profile)
    {
        return Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "Models",
            profile == OcrModelProfile.高精度 ? "ppocrv5_server_rec" : "ppocrv5_mobile_rec",
            "inference.onnx"
        );
    }

    public static string GetSharedDictionaryPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Models", "ppocrv5_dict.txt");
    }

    public static string GetExpectedSha256(OcrModelProfile profile)
    {
        return profile == OcrModelProfile.高精度
            ? "e09385400eaaaef34ceff54aeb7c4f0f1fe014c27fa8b9905d4709b65746562a"
            : "5825fc7ebf84ae7a412be049820b4d86d77620f204a041697b0494669b1742c5";
    }
}
