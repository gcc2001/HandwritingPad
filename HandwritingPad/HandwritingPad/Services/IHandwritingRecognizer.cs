using Avalonia;
using HandwritingPad.Models;

namespace HandwritingPad.Services;

public interface IHandwritingRecognizer
{
    Task<IReadOnlyList<RecognitionCandidate>> RecognizeAsync(
        IReadOnlyList<InkStroke> strokes,
        Size canvasSize,
        RecognitionInputMode inputMode,
        CancellationToken cancellationToken);
}
