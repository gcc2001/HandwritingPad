using Avalonia;
using HandwritingPad.Models;

namespace HandwritingPad.Services;

public static class InkLayoutAnalyzer
{
    public static InkLayoutInfo Analyze(IReadOnlyList<InkStroke> strokes, Size canvasSize)
    {
        var points = strokes.SelectMany(x => x.Points).ToArray();
        if (points.Length == 0)
        {
            return new InkLayoutInfo();
        }

        var minX = points.Min(x => x.X);
        var minY = points.Min(x => x.Y);
        var maxX = points.Max(x => x.X);
        var maxY = points.Max(x => x.Y);

        var width = Math.Max(1.0, maxX - minX);
        var height = Math.Max(1.0, maxY - minY);
        var aspect = width / height;
        var canvasWidth = Math.Max(1.0, canvasSize.Width);
        var canvasHeight = Math.Max(1.0, canvasSize.Height);
        var relativeWidth = width / canvasWidth;
        var relativeHeight = height / canvasHeight;
        var strokeCount = strokes.Count;
        var pointCount = points.Length;

        var isLikelyPunctuation =
            strokeCount <= 3 &&
            pointCount <= 120 &&
            (relativeWidth <= 0.18 && relativeHeight <= 0.28 || aspect <= 0.45 || aspect >= 3.2);

        var isLikelySingleCjk =
            !isLikelyPunctuation &&
            strokeCount <= 28 &&
            pointCount <= 1400 &&
            aspect >= 0.42 &&
            aspect <= 1.85;

        return new InkLayoutInfo
        {
            StrokeCount = strokeCount,
            PointCount = pointCount,
            MinX = minX,
            MinY = minY,
            MaxX = maxX,
            MaxY = maxY,
            IsLikelySingleCjkCharacter = isLikelySingleCjk,
            IsLikelyPunctuation = isLikelyPunctuation
        };
    }
}
