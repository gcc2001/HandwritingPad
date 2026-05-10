namespace HandwritingPad.Models;

public sealed record RecognitionCandidate(string Text, double Confidence)
{
    public string ConfidenceText => $"置信度 {Confidence:P0}";
}
