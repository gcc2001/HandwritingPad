using Avalonia;
using HandwritingPad.Models;

namespace HandwritingPad.Services;

public sealed class PunctuationGeometryRecognizer
{
    public IReadOnlyList<RecognitionCandidate> Recognize(
        IReadOnlyList<InkStroke> strokes,
        Size canvasSize,
        RecognitionInputMode inputMode,
        InkLayoutInfo layout)
    {
        if (strokes.Count == 0)
        {
            return Array.Empty<RecognitionCandidate>();
        }

        var boxes = strokes
            .Where(x => x.Points.Count > 0)
            .Select(GetBox)
            .OrderBy(x => x.CenterY)
            .ToArray();

        if (boxes.Length == 0)
        {
            return Array.Empty<RecognitionCandidate>();
        }

        var result = new List<RecognitionCandidate>();
        var preferChinese = inputMode is RecognitionInputMode.ChineseText or RecognitionInputMode.General or RecognitionInputMode.Punctuation or RecognitionInputMode.Symbol;

        if (boxes.Length == 1)
        {
            AddSingleStrokeCandidates(result, boxes[0], canvasSize, preferChinese);
        }
        else if (boxes.Length == 2)
        {
            AddTwoStrokeCandidates(result, boxes, canvasSize, preferChinese);
        }
        else if (boxes.Length == 3)
        {
            AddThreeStrokeCandidates(result, boxes, preferChinese);
        }

        if (inputMode is RecognitionInputMode.Punctuation or RecognitionInputMode.Symbol)
        {
            return result.OrderByDescending(x => x.Confidence).Take(8).ToArray();
        }

        if (!layout.IsLikelyPunctuation)
        {
            return result.Where(x => x.Confidence >= 0.68).OrderByDescending(x => x.Confidence).Take(4).ToArray();
        }

        return result.OrderByDescending(x => x.Confidence).Take(6).ToArray();
    }

    private static void AddSingleStrokeCandidates(List<RecognitionCandidate> result, StrokeBox box, Size canvasSize, bool preferChinese)
    {
        var canvasWidth = Math.Max(1.0, canvasSize.Width);
        var canvasHeight = Math.Max(1.0, canvasSize.Height);
        var relativeWidth = box.Width / canvasWidth;
        var relativeHeight = box.Height / canvasHeight;
        var aspect = box.Width / Math.Max(1.0, box.Height);

        var isSmallDot = relativeWidth <= 0.08 && relativeHeight <= 0.08 && aspect >= 0.45 && aspect <= 2.2;
        if (isSmallDot)
        {
            result.Add(new RecognitionCandidate(preferChinese ? "。" : ".", 0.90));
            result.Add(new RecognitionCandidate(".", 0.82));
            result.Add(new RecognitionCandidate("·", 0.70));
            return;
        }

        var isCommaLike = relativeWidth <= 0.12 && relativeHeight <= 0.22 && aspect <= 0.85;
        if (isCommaLike)
        {
            result.Add(new RecognitionCandidate(preferChinese ? "，" : ",", 0.84));
            result.Add(new RecognitionCandidate("、", 0.76));
            result.Add(new RecognitionCandidate(",", 0.72));
            return;
        }

        var isDashLike = aspect >= 3.0 && relativeHeight <= 0.08;
        if (isDashLike)
        {
            result.Add(new RecognitionCandidate("—", 0.82));
            result.Add(new RecognitionCandidate("-", 0.78));
            return;
        }

        var isVerticalLine = box.Height >= box.Width * 2.8 && relativeHeight <= 0.42;
        if (isVerticalLine)
        {
            result.Add(new RecognitionCandidate("丨", 0.55));
            result.Add(new RecognitionCandidate("|", 0.50));
        }
    }

    private static void AddTwoStrokeCandidates(List<RecognitionCandidate> result, IReadOnlyList<StrokeBox> boxes, Size canvasSize, bool preferChinese)
    {
        var upper = boxes[0];
        var lower = boxes[1];
        var isUpperDot = IsDot(upper, canvasSize);
        var isLowerDot = IsDot(lower, canvasSize);
        var verticallyAligned = Math.Abs(upper.CenterX - lower.CenterX) <= Math.Max(upper.Width, lower.Width) * 1.5 + 8;

        if (isUpperDot && isLowerDot && verticallyAligned)
        {
            result.Add(new RecognitionCandidate(preferChinese ? "：" : ":", 0.88));
            result.Add(new RecognitionCandidate(":", 0.82));
            return;
        }

        var upperIsVertical = upper.Height >= upper.Width * 2.4;
        if (upperIsVertical && isLowerDot && verticallyAligned)
        {
            result.Add(new RecognitionCandidate(preferChinese ? "！" : "!", 0.90));
            result.Add(new RecognitionCandidate("!", 0.84));
            return;
        }

        if (!isUpperDot && isLowerDot)
        {
            result.Add(new RecognitionCandidate(preferChinese ? "？" : "?", 0.72));
            result.Add(new RecognitionCandidate("?", 0.66));
            return;
        }

        if (isUpperDot && !isLowerDot)
        {
            result.Add(new RecognitionCandidate(preferChinese ? "；" : ";", 0.72));
            result.Add(new RecognitionCandidate(";", 0.66));
        }
    }

    private static void AddThreeStrokeCandidates(List<RecognitionCandidate> result, IReadOnlyList<StrokeBox> boxes, bool preferChinese)
    {
        var allSmall = boxes.All(x => x.Width <= 28 && x.Height <= 28);
        if (allSmall)
        {
            result.Add(new RecognitionCandidate("…", 0.72));
            result.Add(new RecognitionCandidate("...", 0.68));
        }

        var totalWidth = boxes.Max(x => x.MaxX) - boxes.Min(x => x.MinX);
        var totalHeight = boxes.Max(x => x.MaxY) - boxes.Min(x => x.MinY);
        if (totalHeight > totalWidth * 1.4)
        {
            result.Add(new RecognitionCandidate(preferChinese ? "！" : "!", 0.60));
        }
    }

    private static bool IsDot(StrokeBox box, Size canvasSize)
    {
        var canvasWidth = Math.Max(1.0, canvasSize.Width);
        var canvasHeight = Math.Max(1.0, canvasSize.Height);
        return box.Width / canvasWidth <= 0.09 && box.Height / canvasHeight <= 0.09 && box.Width / Math.Max(1.0, box.Height) <= 2.4 && box.Height / Math.Max(1.0, box.Width) <= 2.4;
    }

    private static StrokeBox GetBox(InkStroke stroke)
    {
        var minX = stroke.Points.Min(x => x.X);
        var minY = stroke.Points.Min(x => x.Y);
        var maxX = stroke.Points.Max(x => x.X);
        var maxY = stroke.Points.Max(x => x.Y);
        return new StrokeBox(minX, minY, maxX, maxY);
    }

    private sealed record StrokeBox(double MinX, double MinY, double MaxX, double MaxY)
    {
        public double Width => Math.Max(1.0, MaxX - MinX);
        public double Height => Math.Max(1.0, MaxY - MinY);
        public double CenterX => (MinX + MaxX) / 2.0;
        public double CenterY => (MinY + MaxY) / 2.0;
    }
}
