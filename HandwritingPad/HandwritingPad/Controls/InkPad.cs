using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HandwritingPad.Models;

namespace HandwritingPad.Controls;

public sealed class InkPad : Control
{
    private readonly List<InkStroke> _strokes = new();
    private List<InkPoint>? _currentStroke;

    private static readonly Pen StrokePen = new(
        Brushes.Black,
        thickness: 4,
        lineCap: PenLineCap.Round,
        lineJoin: PenLineJoin.Round);

    public event EventHandler? StrokesChanged;

    public IReadOnlyList<InkStroke> GetSnapshot()
    {
        return _strokes
            .Select(x => new InkStroke(x.Points.ToArray()))
            .ToArray();
    }

    public void Clear()
    {
        _strokes.Clear();
        _currentStroke = null;
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _currentStroke = new List<InkPoint>();
        AddPoint(e);
        _strokes.Add(new InkStroke(_currentStroke));
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_currentStroke is null)
        {
            return;
        }

        AddPoint(e);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_currentStroke is not null)
        {
            AddPoint(e);
        }

        _currentStroke = null;
        e.Pointer.Capture(null);
        StrokesChanged?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var rect = new Rect(0, 0, Bounds.Width, Bounds.Height);
        context.FillRectangle(Brushes.White, rect);

        foreach (var stroke in _strokes)
        {
            DrawStroke(context, stroke);
        }
    }

    private void AddPoint(PointerEventArgs e)
    {
        if (_currentStroke is null)
        {
            return;
        }

        var position = e.GetCurrentPoint(this).Position;
        _currentStroke.Add(new InkPoint(position.X, position.Y, Environment.TickCount64));
        InvalidateVisual();
        StrokesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static void DrawStroke(DrawingContext context, InkStroke stroke)
    {
        if (stroke.Points.Count == 0)
        {
            return;
        }

        if (stroke.Points.Count == 1)
        {
            var p = stroke.Points[0];
            context.DrawEllipse(Brushes.Black, null, new Point(p.X, p.Y), 2, 2);
            return;
        }

        for (var i = 1; i < stroke.Points.Count; i++)
        {
            var previous = stroke.Points[i - 1];
            var current = stroke.Points[i];
            context.DrawLine(StrokePen, new Point(previous.X, previous.Y), new Point(current.X, current.Y));
        }
    }
}
