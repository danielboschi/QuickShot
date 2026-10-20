using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace QuickShot;

public enum EditorTool { Pen, Line, Arrow, Rectangle, Ellipse, Text, Highlight, Erase }

public sealed class DrawingSurface : Canvas
{
    private readonly Stack<EditAction> _undo = new();
    private readonly Stack<EditAction> _redo = new();
    private Point _start;
    private UIElement? _preview;
    private Polyline? _freehand;
    public EditorTool Tool { get; set; } = EditorTool.Pen;
    public Brush DrawBrush { get; set; } = Brushes.Red;
    public double DrawThickness { get; set; } = 3;

    public DrawingSurface()
    {
        Background = Brushes.Transparent;
        MouseLeftButtonDown += Begin; MouseMove += Move; MouseLeftButtonUp += End;
    }
    public void SetImage(ImageSource image)
    {
        ClearImage();
        Width = image.Width; Height = image.Height;
        Children.Add(new Image { Source = image, Width = Width, Height = Height, IsHitTestVisible = false });
    }
    public void ClearImage()
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        Children.Clear(); _undo.Clear(); _redo.Clear();
        _preview = null; _freehand = null;
    }
    public BitmapSource RenderImage()
    {
        if (Children.Count == 0) throw new InvalidOperationException("A imagem de fundo não foi definida.");
        UpdateLayout();
        var width = (int)Math.Ceiling(ActualWidth);
        var height = (int)Math.Ceiling(ActualHeight);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(((Image)Children[0]).Source, new Rect(0, 0, width, height));
            foreach (UIElement child in Children)
            {
                if (ReferenceEquals(child, Children[0]) || child.Visibility != Visibility.Visible) continue;
                var bounds = VisualTreeHelper.GetDescendantBounds(child);
                if (bounds.IsEmpty)
                {
                    child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    child.Arrange(new Rect(child.DesiredSize));
                    bounds = VisualTreeHelper.GetDescendantBounds(child);
                }
                if (bounds.IsEmpty) continue;

                var origin = child.TransformToAncestor(this).Transform(new Point(bounds.X, bounds.Y));
                var destination = new Rect(origin.X, origin.Y, bounds.Width, bounds.Height);
                context.DrawRectangle(new VisualBrush(child) { Stretch = Stretch.Fill }, null, destination);
            }
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }
    private void Begin(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        if (Tool == EditorTool.Text) { AddText(_start); return; }
        if (Tool == EditorTool.Erase) { EraseAt(_start); return; }
        CaptureMouse();
        if (Tool is EditorTool.Pen or EditorTool.Highlight)
        {
            _freehand = new Polyline { Stroke = DrawBrush, StrokeThickness = DrawThickness, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Opacity = Tool == EditorTool.Highlight ? .30 : 1 };
            _freehand.Points.Add(_start); Children.Add(_freehand);
        }
        else { _preview = CreateShape(); if (_preview != null) Children.Add(_preview); }
    }
    private void Move(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;
        var point = e.GetPosition(this);
        if (_freehand != null) _freehand.Points.Add(point);
        else if (_preview != null) UpdateShape(_preview, point);
    }
    private void End(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured) return;
        var point = e.GetPosition(this); ReleaseMouseCapture();
        if (_freehand != null)
        {
            _freehand.Points.Add(point);
            if (_freehand.Points.Count > 1) RecordAdded(_freehand); else Children.Remove(_freehand);
            _freehand = null;
        }
        else if (_preview != null)
        {
            UpdateShape(_preview, point); RecordAdded(_preview); _preview = null;
        }
    }
    private UIElement? CreateShape() => Tool switch
    {
        EditorTool.Line => new Line { Stroke = DrawBrush, StrokeThickness = DrawThickness, X1 = _start.X, Y1 = _start.Y, X2 = _start.X, Y2 = _start.Y },
        EditorTool.Arrow => new Path { Stroke = DrawBrush, StrokeThickness = DrawThickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round },
        EditorTool.Rectangle => new Rectangle { Stroke = DrawBrush, StrokeThickness = DrawThickness, Fill = Brushes.Transparent },
        EditorTool.Ellipse => new Ellipse { Stroke = DrawBrush, StrokeThickness = DrawThickness, Fill = Brushes.Transparent },
        _ => null
    };
    private void UpdateShape(UIElement shape, Point end)
    {
        if (shape is Line line) { line.X2 = end.X; line.Y2 = end.Y; return; }
        if (shape is Path arrow) { arrow.Data = ArrowGeometry(_start, end); return; }
        if (shape is Shape box)
        {
            Canvas.SetLeft(box, Math.Min(_start.X, end.X)); Canvas.SetTop(box, Math.Min(_start.Y, end.Y));
            box.Width = Math.Abs(end.X - _start.X); box.Height = Math.Abs(end.Y - _start.Y);
        }
    }
    private static Geometry ArrowGeometry(Point from, Point to)
    {
        var dx = to.X - from.X; var dy = to.Y - from.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1) return Geometry.Empty;
        var ux = dx / length; var uy = dy / length; var head = Math.Min(22, Math.Max(10, length * .22));
        var px = -uy; var py = ux;
        var wing1 = new Point(to.X - ux * head + px * head * .55, to.Y - uy * head + py * head * .55);
        var wing2 = new Point(to.X - ux * head - px * head * .55, to.Y - uy * head - py * head * .55);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(from, false, false); context.LineTo(to, true, false);
            context.BeginFigure(wing1, false, false); context.LineTo(to, true, false); context.LineTo(wing2, true, false);
        }
        geometry.Freeze();
        return geometry;
    }
    private void AddText(Point at)
    {
        var text = new TextBox { Foreground = DrawBrush, Background = Brushes.Transparent, BorderBrush = DrawBrush, FontSize = Math.Max(14, DrawThickness * 5), MinWidth = 120, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
        Canvas.SetLeft(text, at.X); Canvas.SetTop(text, at.Y); Children.Add(text); RecordAdded(text); text.Focus();
    }
    private void EraseAt(Point point)
    {
        var hit = InputHitTest(point) as DependencyObject;
        while (hit != null && hit != this && VisualTreeHelper.GetParent(hit) != this) hit = VisualTreeHelper.GetParent(hit);
        if (hit is UIElement element && Children.IndexOf(element) > 0)
        {
            var index = Children.IndexOf(element);
            Children.Remove(element);
            Record(new EditAction(() => InsertElement(index, element), () => RemoveElement(element)));
        }
    }
    private void RecordAdded(UIElement element)
    {
        var index = Children.IndexOf(element);
        // A criação entra na pilha de desfazer como remoção e na pilha de refazer como reinserção.
        Record(new EditAction(() => RemoveElement(element), () => InsertElement(index, element)));
    }
    private void Record(EditAction action) { _undo.Push(action); _redo.Clear(); }
    public void Undo()
    {
        if (!_undo.TryPop(out var action)) return;
        try { action.Undo(); _redo.Push(action); DebugLog.Write($"Desfazer concluído. Undo={_undo.Count}; Redo={_redo.Count}."); }
        catch (Exception exception) { _undo.Push(action); DebugLog.Write("Falha ao desfazer anotação.", exception); }
    }
    public void Redo()
    {
        if (!_redo.TryPop(out var action)) return;
        try { action.Redo(); _undo.Push(action); DebugLog.Write($"Refazer concluído. Undo={_undo.Count}; Redo={_redo.Count}."); }
        catch (Exception exception) { _redo.Push(action); DebugLog.Write("Falha ao refazer anotação.", exception); }
    }

    private void InsertElement(int index, UIElement element)
    {
        if (Children.Contains(element)) return;
        if (VisualTreeHelper.GetParent(element) is Panel oldParent) oldParent.Children.Remove(element);
        Children.Insert(Math.Clamp(index, 1, Children.Count), element);
    }

    private void RemoveElement(UIElement element)
    {
        var index = Children.IndexOf(element);
        if (index > 0) Children.RemoveAt(index);
    }
    private sealed record EditAction(Action Undo, Action Redo);
}
