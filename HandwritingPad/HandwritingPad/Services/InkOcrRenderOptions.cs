namespace HandwritingPad.Services;

public sealed record InkOcrRenderOptions
{
    public InkRenderMode Mode { get; init; } = InkRenderMode.Normal;
    public int TargetHeight { get; init; } = 192;
    public int MinTargetWidth { get; init; } = 320;
    public int MaxTargetWidth { get; init; } = 1600;
    public float Padding { get; init; } = 56f;
    public float StrokeWidthScale { get; init; } = 0.075f;
    public bool SmoothStroke { get; init; } = false;
    public bool BinaryOutput { get; init; } = false;
    public bool ForceSquareCanvas { get; init; } = false;
    public bool SaveDebugImage { get; init; } = false;
    public string DebugDirectory { get; init; } = "debug_ocr";
}
