using HandwritingPad.Models;

namespace HandwritingPad.Services;

public sealed record OcrRecognitionResult(
    IReadOnlyList<RecognitionCandidate> Candidates,
    OcrModelProfile ModelProfile,
    OcrExecutionProvider RequestedProvider,
    string EffectiveProvider);
