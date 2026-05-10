using HandwritingPad.Models;
using SkiaSharp;

namespace HandwritingPad.Services;

public sealed class InkOcrImageRenderer
{
    public SKBitmap RenderCroppedLineBitmap(
        IReadOnlyList<InkStroke> strokes,
        InkOcrRenderOptions options)
    {
        var points = strokes.SelectMany(x => x.Points).ToArray();
        if (points.Length == 0)
        {
            return CreateBlankBitmap(options.MinTargetWidth, options.TargetHeight);
        }

        var minX = points.Min(x => x.X);
        var minY = points.Min(x => x.Y);
        var maxX = points.Max(x => x.X);
        var maxY = points.Max(x => x.Y);
        var rawWidth = Math.Max(1.0, maxX - minX);
        var rawHeight = Math.Max(1.0, maxY - minY);
        var padding = ResolvePadding(options);
        var expandedMinX = minX - padding;
        var expandedMinY = minY - padding;
        var expandedMaxX = maxX + padding;
        var expandedMaxY = maxY + padding;
        var expandedWidth = Math.Max(1.0, expandedMaxX - expandedMinX);
        var expandedHeight = Math.Max(1.0, expandedMaxY - expandedMinY);

        int targetWidth;
        if (options.ForceSquareCanvas)
        {
            targetWidth = options.TargetHeight;
        }
        else
        {
            var aspect = expandedWidth / expandedHeight;
            targetWidth = (int)Math.Round(options.TargetHeight * aspect);
            targetWidth = Math.Clamp(targetWidth, options.MinTargetWidth, options.MaxTargetWidth);
        }

        var bitmap = new SKBitmap(targetWidth, options.TargetHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);

        var scaleX = targetWidth / expandedWidth;
        var scaleY = options.TargetHeight / expandedHeight;
        var scale = (float)Math.Min(scaleX, scaleY);
        var renderedWidth = (float)(expandedWidth * scale);
        var renderedHeight = (float)(expandedHeight * scale);
        var offsetX = (targetWidth - renderedWidth) / 2f;
        var offsetY = (options.TargetHeight - renderedHeight) / 2f;
        var strokeWidth = ResolveStrokeWidth(rawWidth, rawHeight, options);

        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = options.Mode != InkRenderMode.Binary,
            StrokeWidth = strokeWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            Style = SKPaintStyle.Stroke
        };

        foreach (var stroke in strokes)
        {
            DrawStroke(canvas, paint, stroke, expandedMinX, expandedMinY, scale, offsetX, offsetY, options);
        }

        if (options.BinaryOutput || options.Mode == InkRenderMode.Binary)
        {
            using var binary = Binarize(bitmap);
            bitmap.Dispose();
            var cloned = binary.Copy();
            if (options.SaveDebugImage)
            {
                SaveDebugBitmap(cloned, options.DebugDirectory, options.Mode);
            }
            return cloned;
        }

        if (options.SaveDebugImage)
        {
            SaveDebugBitmap(bitmap, options.DebugDirectory, options.Mode);
        }

        return bitmap;
    }

    public static IReadOnlyList<InkOcrRenderOptions> CreateVariants(InkLayoutInfo layout, bool saveDebugImage)
    {
        var variants = new List<InkOcrRenderOptions>
        {
            new()
            {
                Mode = InkRenderMode.Normal,
                TargetHeight = 192,
                Padding = 56f,
                StrokeWidthScale = 0.075f,
                SmoothStroke = false,
                BinaryOutput = false,
                SaveDebugImage = saveDebugImage
            },
            new()
            {
                Mode = InkRenderMode.Smooth,
                TargetHeight = 192,
                Padding = 56f,
                StrokeWidthScale = 0.075f,
                SmoothStroke = true,
                BinaryOutput = false,
                SaveDebugImage = saveDebugImage
            },
            new()
            {
                Mode = InkRenderMode.Thin,
                TargetHeight = 192,
                Padding = 60f,
                StrokeWidthScale = 0.058f,
                SmoothStroke = true,
                BinaryOutput = false,
                SaveDebugImage = saveDebugImage
            },
            new()
            {
                Mode = InkRenderMode.Bold,
                TargetHeight = 192,
                Padding = 64f,
                StrokeWidthScale = 0.095f,
                SmoothStroke = true,
                BinaryOutput = false,
                SaveDebugImage = saveDebugImage
            },
            new()
            {
                Mode = InkRenderMode.Binary,
                TargetHeight = 192,
                Padding = 60f,
                StrokeWidthScale = 0.075f,
                SmoothStroke = true,
                BinaryOutput = true,
                SaveDebugImage = saveDebugImage
            }
        };

        if (layout.IsLikelySingleCjkCharacter)
        {
            variants.Insert(0, new InkOcrRenderOptions
            {
                Mode = InkRenderMode.SquareCjk,
                TargetHeight = 224,
                MinTargetWidth = 224,
                MaxTargetWidth = 224,
                ForceSquareCanvas = true,
                Padding = 72f,
                StrokeWidthScale = 0.070f,
                SmoothStroke = true,
                BinaryOutput = false,
                SaveDebugImage = saveDebugImage
            });

            variants.Insert(1, new InkOcrRenderOptions
            {
                Mode = InkRenderMode.CompactCjk,
                TargetHeight = 192,
                MinTargetWidth = 192,
                MaxTargetWidth = 320,
                ForceSquareCanvas = false,
                Padding = 64f,
                StrokeWidthScale = 0.068f,
                SmoothStroke = true,
                BinaryOutput = false,
                SaveDebugImage = saveDebugImage
            });
        }
        else
        {
            variants.Add(new InkOcrRenderOptions
            {
                Mode = InkRenderMode.LooseCrop,
                TargetHeight = 224,
                Padding = 86f,
                StrokeWidthScale = 0.070f,
                SmoothStroke = true,
                BinaryOutput = false,
                MaxTargetWidth = 1800,
                SaveDebugImage = saveDebugImage
            });
        }

        return variants;
    }

    private static float ResolvePadding(InkOcrRenderOptions options)
    {
        return options.Mode switch
        {
            InkRenderMode.SquareCjk => Math.Max(options.Padding, 72f),
            InkRenderMode.CompactCjk => Math.Max(options.Padding, 64f),
            InkRenderMode.LooseCrop => Math.Max(options.Padding, 84f),
            InkRenderMode.Thin => Math.Max(options.Padding, 60f),
            InkRenderMode.Bold => Math.Max(options.Padding, 64f),
            _ => options.Padding
        };
    }

    private static float ResolveStrokeWidth(double sourceWidth, double sourceHeight, InkOcrRenderOptions options)
    {
        var baseWidth = options.TargetHeight * options.StrokeWidthScale;
        var aspect = sourceWidth / Math.Max(1.0, sourceHeight);
        if (aspect > 4.0) baseWidth *= 0.88f;
        if (aspect < 0.75) baseWidth *= 1.08f;

        var width = options.Mode switch
        {
            InkRenderMode.Thin => baseWidth * 0.75f,
            InkRenderMode.Bold => baseWidth * 1.25f,
            InkRenderMode.Binary => baseWidth * 1.05f,
            InkRenderMode.SquareCjk => baseWidth * 0.95f,
            InkRenderMode.CompactCjk => baseWidth * 0.92f,
            _ => baseWidth
        };

        return Math.Clamp(width, 8f, options.TargetHeight * 0.13f);
    }

    private static void DrawStroke(
        SKCanvas canvas,
        SKPaint paint,
        InkStroke stroke,
        double minX,
        double minY,
        float scale,
        float offsetX,
        float offsetY,
        InkOcrRenderOptions options)
    {
        if (stroke.Points.Count == 0) return;
        var points = options.SmoothStroke ? StrokePreprocessor.SimplifyPoints(stroke.Points) : stroke.Points;

        if (points.Count == 1)
        {
            var p = points[0];
            canvas.DrawCircle(offsetX + (float)((p.X - minX) * scale), offsetY + (float)((p.Y - minY) * scale), paint.StrokeWidth / 2f, paint);
            return;
        }

        if (options.SmoothStroke && points.Count >= 3)
        {
            DrawSmoothedStroke(canvas, paint, points, minX, minY, scale, offsetX, offsetY);
            return;
        }

        using var path = new SKPath();
        var first = points[0];
        path.MoveTo(offsetX + (float)((first.X - minX) * scale), offsetY + (float)((first.Y - minY) * scale));
        for (var i = 1; i < points.Count; i++)
        {
            var p = points[i];
            path.LineTo(offsetX + (float)((p.X - minX) * scale), offsetY + (float)((p.Y - minY) * scale));
        }
        canvas.DrawPath(path, paint);
    }

    private static void DrawSmoothedStroke(SKCanvas canvas, SKPaint paint, IReadOnlyList<InkPoint> points, double minX, double minY, float scale, float offsetX, float offsetY)
    {
        using var path = new SKPath();
        var first = ToPoint(points[0], minX, minY, scale, offsetX, offsetY);
        path.MoveTo(first);
        for (var i = 1; i < points.Count - 1; i++)
        {
            var current = ToPoint(points[i], minX, minY, scale, offsetX, offsetY);
            var next = ToPoint(points[i + 1], minX, minY, scale, offsetX, offsetY);
            var mid = new SKPoint((current.X + next.X) / 2f, (current.Y + next.Y) / 2f);
            path.QuadTo(current, mid);
        }
        var last = ToPoint(points[^1], minX, minY, scale, offsetX, offsetY);
        path.LineTo(last);
        canvas.DrawPath(path, paint);
    }

    private static SKPoint ToPoint(InkPoint p, double minX, double minY, float scale, float offsetX, float offsetY)
    {
        return new SKPoint(offsetX + (float)((p.X - minX) * scale), offsetY + (float)((p.Y - minY) * scale));
    }

    private static SKBitmap Binarize(SKBitmap source)
    {
        var output = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                var gray = (int)(c.Red * 0.299 + c.Green * 0.587 + c.Blue * 0.114);
                var value = gray < 210 ? (byte)0 : (byte)255;
                output.SetPixel(x, y, new SKColor(value, value, value, 255));
            }
        }
        return output;
    }

    private static SKBitmap CreateBlankBitmap(int width, int height)
    {
        var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.White);
        return bitmap;
    }

    private static void SaveDebugBitmap(SKBitmap bitmap, string directory, InkRenderMode mode)
    {
        Directory.CreateDirectory(directory);
        var fileName = $"ocr_{mode}_{DateTimeOffset.Now:yyyyMMdd_HHmmss_fff}.png";
        var path = Path.Combine(directory, fileName);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.OpenWrite(path);
        data.SaveTo(stream);
    }
}
