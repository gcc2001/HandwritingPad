using HandwritingPad.Models;

namespace HandwritingPad.Services;

public static class StrokePreprocessor
{
    public static IReadOnlyList<InkPoint> SimplifyPoints(
        IReadOnlyList<InkPoint> points,
        double minDistance = 2.5)
    {
        if (points.Count <= 2)
        {
            return points;
        }

        var result = new List<InkPoint> { points[0] };
        var last = points[0];

        for (var i = 1; i < points.Count; i++)
        {
            var current = points[i];
            var dx = current.X - last.X;
            var dy = current.Y - last.Y;
            var distance = Math.Sqrt(dx * dx + dy * dy);

            if (distance >= minDistance)
            {
                result.Add(current);
                last = current;
            }
        }

        if (result[^1] != points[^1])
        {
            result.Add(points[^1]);
        }

        return result;
    }
}
