namespace HandwritingPad.Models;

public sealed record InkStroke(IReadOnlyList<InkPoint> Points);
