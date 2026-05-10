namespace HandwritingPad.Services;

public sealed record OcrEnhancementOptions
{
    public bool EnableMultiRenderTta { get; init; } = true;
    public bool EnableBeamSearch { get; init; } = true;
    public bool EnableInputModeRerank { get; init; } = true;
    public int BeamWidth { get; init; } = 8;
    public int BeamTopK { get; init; } = 5;
    public int MaxCandidateCount { get; init; } = 8;
    public double MinCandidateConfidence { get; init; } = 0.01;

    public static OcrEnhancementOptions LowComputeDefault => new()
    {
        EnableMultiRenderTta = true,
        EnableBeamSearch = true,
        EnableInputModeRerank = true,
        BeamWidth = 8,
        BeamTopK = 5,
        MaxCandidateCount = 8
    };

    public static OcrEnhancementOptions LowestCompute => new()
    {
        EnableMultiRenderTta = true,
        EnableBeamSearch = false,
        EnableInputModeRerank = true,
        BeamWidth = 4,
        BeamTopK = 3,
        MaxCandidateCount = 6
    };
}
