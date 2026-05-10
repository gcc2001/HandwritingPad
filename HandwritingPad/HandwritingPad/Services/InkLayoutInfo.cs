namespace HandwritingPad.Services;

public sealed record InkLayoutInfo
{
    public int StrokeCount { get; init; }
    public int PointCount { get; init; }
    public double MinX { get; init; }
    public double MinY { get; init; }
    public double MaxX { get; init; }
    public double MaxY { get; init; }
    public double Width => Math.Max(1.0, MaxX - MinX);
    public double Height => Math.Max(1.0, MaxY - MinY);
    public double AspectRatio => Width / Height;
    public bool IsLikelySingleCjkCharacter { get; init; }
    public bool IsLikelyPunctuation { get; init; }
    public string BBoxText => $"{MinX:F1},{MinY:F1},{MaxX:F1},{MaxY:F1}";
}
