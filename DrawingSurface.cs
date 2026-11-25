using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace QuickShot;

public enum EditorTool { Move, Pen, Line, Arrow, Rectangle, Ellipse, Text, Highlight, Redact, Erase }

public sealed class DrawingSurface : Canvas
{
    private readonly Stack<EditAction> _undo = new();
    private readonly Stack<EditAction> _redo = new();
    private Point _start;
    private UIElement? _preview;
    private Polyline? _freehand;
    private UIElement? _moving;
    private Point _moveOrigin;
    private bool _movingRegion;
    private TextBox? _textEditor;
    private TextBlock? _editedText;
    private EditorTool _tool = EditorTool.Pen;
    public EditorTool Tool
    {
        get => _tool;
        set { _tool = value; Cursor = value switch { EditorTool.Move => Cursors.SizeAll, EditorTool.Text => Cursors.IBeam, _ => Cursors.Cross }; }
    }
    public Brush DrawBrush { get; set; } = Brushes.Red;
    public double DrawThickness { get; set; } = 3;
    /// <summary>Escala do monitor: traços e textos novos têm o mesmo tamanho aparente em qualquer DPI.</summary>
    public double Scale { get; set; } = 1;
    private double ScaledThickness => DrawThickness * Scale;

    /// <summary>Com a ferramenta de mover, arrastar fora das anotações move a seleção.</summary>
    public event Action? RegionDragStarted;
    public event Action<Vector>? RegionDragged;
    public event Action? RegionDragCompleted;

    private Rect _region;
    /// <summary>
    /// Parte da imagem que é exibida, editável e exportada. As anotações usam as coordenadas da imagem
    /// inteira: mudar a região não as desloca, e as que ficam de fora reaparecem quando ela cresce.
    /// </summary>
    public Rect Region
    {
        get => _region;
        set { _region = value; Clip = new RectangleGeometry(value); }
    }

    public DrawingSurface()
    {
        Background = Brushes.Transparent;
        MouseLeftButtonDown += Begin; MouseMove += Move; MouseLeftButtonUp += End; LostMouseCapture += OnLostCapture;
    }
    public void SetImage(BitmapSource image)
    {
        ClearImage();
        Width = image.Width; Height = image.Height;
        Region = new Rect(0, 0, Width, Height);
        Children.Add(new Image { Source = image, Width = Width, Height = Height, IsHitTestVisible = false });
    }
    public void ClearImage()
    {
        if (IsMouseCaptured) ReleaseMouseCapture();
        Children.Clear(); _undo.Clear(); _redo.Clear();
        _preview = null; _freehand = null; _moving = null; _movingRegion = false;
        _textEditor = null; _editedText = null;
    }
    public BitmapSource RenderImage()
    {
        if (Children.Count == 0) throw new InvalidOperationException("A imagem de fundo não foi definida.");
        CommitText();
        UpdateLayout();
        var background = (BitmapSource)((Image)Children[0]).Source;
        var crop = new Int32Rect((int)Region.X, (int)Region.Y, (int)Region.Width, (int)Region.Height);
        var width = crop.Width;
        var height = crop.Height;
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(new CroppedBitmap(background, crop), new Rect(0, 0, width, height));
            context.PushTransform(new TranslateTransform(-crop.X, -crop.Y));
            foreach (UIElement child in Children)
            {
                if (ReferenceEquals(child, Children[0]) || child.Visibility != Visibility.Visible) continue;
                if (IsRedaction(child))
                {
                    // Desenhada diretamente, sem pincel intermediário: preto opaco em todos os pixels.
                    var bar = (Rectangle)child;
                    context.DrawRectangle(RedactionBrush, null, new Rect(Canvas.GetLeft(bar), Canvas.GetTop(bar), bar.Width, bar.Height));
                    continue;
                }
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

    // --- Mouse

    private void Begin(object sender, MouseButtonEventArgs e)
    {
        _start = e.GetPosition(this);
        if (_textEditor != null)
        {
            // Clicar fora da caixa confirma o texto; clicar dentro apenas posiciona o cursor.
            if (!_textEditor.IsMouseOver) CommitText();
            e.Handled = true;
            return;
        }
        if (Tool == EditorTool.Text) { BeginTextEdit(_start, FindAnnotation(_start) as TextBlock); e.Handled = true; return; }
        if (Tool == EditorTool.Erase) { if (FindAnnotation(_start) is { } target) RemoveAnnotation(target); return; }
        CaptureMouse();
        if (Tool == EditorTool.Move) { BeginMove(FindAnnotation(_start)); return; }
        if (Tool is EditorTool.Pen or EditorTool.Highlight)
        {
            _freehand = new Polyline { Stroke = DrawBrush, StrokeThickness = ScaledThickness, StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, Opacity = Tool == EditorTool.Highlight ? .45 : 1 };
            _freehand.Points.Add(_start); Children.Add(_freehand);
        }
        else { _preview = CreateShape(); if (_preview != null) Children.Add(_preview); }
    }
    private static bool IsConstrained => (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
    private void Move(object sender, MouseEventArgs e)
    {
        if (!IsMouseCaptured) return;
        var point = e.GetPosition(this);
        if (_freehand != null) ExtendFreehand(point, IsConstrained);
        else if (_preview != null) UpdateShape(_preview, IsConstrained ? Constrain(_preview, point) : point);
        else if (_moving != null) MoveTo(_moving, _moveOrigin + Round(point - _start));
        else if (_movingRegion) RegionDragged?.Invoke(point - _start);
    }
    private void End(object sender, MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured) return;
        var point = e.GetPosition(this);
        var constrained = IsConstrained;
        // Encerre o traço antes de liberar o mouse, que dispara LostMouseCapture.
        if (_freehand != null) FinishFreehand(point, constrained);
        else if (_preview != null) FinishShape(constrained ? Constrain(_preview, point) : point);
        else if (_moving != null) FinishMove(point);
        else if (_movingRegion) { RegionDragged?.Invoke(point - _start); EndRegionMove(); }
        ReleaseMouseCapture();
    }
    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        // O Windows tomou o mouse no meio do gesto: descarte o que não foi concluído.
        if (_freehand != null) { Children.Remove(_freehand); _freehand = null; }
        if (_preview != null) { Children.Remove(_preview); _preview = null; }
        if (_moving != null) { MoveTo(_moving, _moveOrigin); _moving = null; }
        if (_movingRegion) EndRegionMove();
    }

    // --- Traços e formas

    private void ExtendFreehand(Point point, bool straight)
    {
        if (_freehand == null) return;
        if (straight) _freehand.Points = new PointCollection { _start, SnapAngle(_start, point) };
        else _freehand.Points.Add(point);
    }
    private void FinishFreehand(Point point, bool straight)
    {
        if (_freehand == null) return;
        ExtendFreehand(point, straight);
        RecordAdded(_freehand);
        _freehand = null;
    }
    private const double MinimumDrag = 3;
    private void FinishShape(Point point)
    {
        if (_preview == null) return;
        UpdateShape(_preview, point);
        // Um clique sem arrastar deixaria uma forma invisível ocupando um passo do desfazer.
        if ((point - _start).Length >= MinimumDrag) RecordAdded(_preview); else Children.Remove(_preview);
        _preview = null;
    }
    private UIElement? CreateShape() => Tool switch
    {
        EditorTool.Line => new Line { Stroke = DrawBrush, StrokeThickness = ScaledThickness, X1 = _start.X, Y1 = _start.Y, X2 = _start.X, Y2 = _start.Y },
        EditorTool.Arrow => new Path { Stroke = DrawBrush, StrokeThickness = ScaledThickness, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round },
        EditorTool.Rectangle => new Rectangle { Stroke = DrawBrush, StrokeThickness = ScaledThickness, Fill = Brushes.Transparent },
        EditorTool.Ellipse => new Ellipse { Stroke = DrawBrush, StrokeThickness = ScaledThickness, Fill = Brushes.Transparent },
        EditorTool.Redact => CreateRedaction(),
        _ => null
    };
    // A tarja é sempre preta e opaca: desfoque ou transparência permitiriam recuperar o conteúdo.
    private static readonly Brush RedactionBrush = CreateRedactionBrush();
    private static Brush CreateRedactionBrush() { var brush = new SolidColorBrush(Colors.Black); brush.Freeze(); return brush; }
    private static Rectangle CreateRedaction()
    {
        var bar = new Rectangle { Fill = RedactionBrush };
        RenderOptions.SetEdgeMode(bar, EdgeMode.Aliased);
        return bar;
    }
    private static bool IsRedaction(UIElement element) => element is Rectangle bar && ReferenceEquals(bar.Fill, RedactionBrush);
    private void UpdateShape(UIElement shape, Point end)
    {
        if (shape is Line line) { line.X2 = end.X; line.Y2 = end.Y; return; }
        if (shape is Path arrow) { arrow.Data = ArrowGeometry(_start, end, Scale); return; }
        if (shape is Shape box)
        {
            var bounds = new Rect(_start, end);
            // Pixels inteiros, para fora: nenhuma borda parcialmente coberta.
            if (IsRedaction(shape)) bounds = new Rect(new Point(Math.Floor(bounds.Left), Math.Floor(bounds.Top)), new Point(Math.Ceiling(bounds.Right), Math.Ceiling(bounds.Bottom)));
            Canvas.SetLeft(box, bounds.X); Canvas.SetTop(box, bounds.Y);
            box.Width = bounds.Width; box.Height = bounds.Height;
        }
    }
    private Point Constrain(UIElement shape, Point point) => shape is Line or Path ? SnapAngle(_start, point) : SnapSquare(_start, point);
    /// <summary>Direção mais próxima entre as oito principais, mantendo o alcance do mouse.</summary>
    internal static Point SnapAngle(Point from, Point to)
    {
        var delta = to - from;
        if (delta.Length < 1) return to;
        var angle = Math.Round(Math.Atan2(delta.Y, delta.X) / (Math.PI / 4)) * (Math.PI / 4);
        var direction = new Vector(Math.Round(Math.Cos(angle), 12), Math.Round(Math.Sin(angle), 12));
        return from + direction * (delta * direction);
    }
    internal static Point SnapSquare(Point from, Point to)
    {
        var delta = to - from; var side = Math.Max(Math.Abs(delta.X), Math.Abs(delta.Y));
        return new Point(from.X + (delta.X < 0 ? -side : side), from.Y + (delta.Y < 0 ? -side : side));
    }
    private static Geometry ArrowGeometry(Point from, Point to, double scale)
    {
        var dx = to.X - from.X; var dy = to.Y - from.Y; var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 1) return Geometry.Empty;
        var ux = dx / length; var uy = dy / length; var head = Math.Min(22 * scale, Math.Max(10 * scale, length * .22));
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

    // --- Mover

    private void BeginMove(UIElement? target)
    {
        if (target == null) { _movingRegion = true; RegionDragStarted?.Invoke(); return; }
        _moving = target; _moveOrigin = Position(target);
    }
    private void FinishMove(Point point)
    {
        if (_moving is not { } element) return;
        var from = _moveOrigin; var to = from + Round(point - _start);
        _moving = null;
        MoveTo(element, to);
        if (to != from) Record(new EditAction(() => MoveTo(element, from), () => MoveTo(element, to)));
    }
    private void EndRegionMove() { _movingRegion = false; RegionDragCompleted?.Invoke(); }
    // Linhas e traços nascem sem posição no Canvas, o que equivale a zero.
    private static Point Position(UIElement element)
    {
        var left = Canvas.GetLeft(element); var top = Canvas.GetTop(element);
        return new Point(double.IsNaN(left) ? 0 : left, double.IsNaN(top) ? 0 : top);
    }
    private static void MoveTo(UIElement element, Point position) { Canvas.SetLeft(element, position.X); Canvas.SetTop(element, position.Y); }
    private static Vector Round(Vector vector) => new(Math.Round(vector.X), Math.Round(vector.Y));

    // --- Texto

    /// <summary>Indica se há um texto sendo digitado, ainda não incorporado à imagem.</summary>
    public bool IsEditingText => _textEditor != null;
    public bool HasSelectedText => _textEditor is { SelectionLength: > 0 };

    // A caixa de edição é temporária: ao confirmar, só o texto, sem moldura nem cursor, vira anotação.
    private void BeginTextEdit(Point at, TextBlock? existing)
    {
        var editor = new TextBox
        {
            Foreground = existing?.Foreground ?? DrawBrush, BorderBrush = existing?.Foreground ?? DrawBrush,
            FontSize = existing?.FontSize ?? Math.Max(14, DrawThickness * 5) * Scale, Text = existing?.Text ?? "",
            Background = Brushes.Transparent, MinWidth = 120, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true
        };
        _textEditor = editor; _editedText = existing;
        if (existing != null) existing.Visibility = Visibility.Hidden;
        Children.Add(editor);
        Canvas.SetLeft(editor, at.X); Canvas.SetTop(editor, at.Y);
        if (existing != null)
        {
            // Mantém as letras na mesma posição enquanto o texto é reeditado.
            var offset = TextOffset(editor);
            Canvas.SetLeft(editor, Canvas.GetLeft(existing) - offset.X); Canvas.SetTop(editor, Canvas.GetTop(existing) - offset.Y);
            editor.CaretIndex = editor.Text.Length;
        }
        // Foco lógico: trocar de janela ou abrir o menu de contexto não encerra a digitação.
        editor.LostFocus += (_, _) => { if (ReferenceEquals(_textEditor, editor)) CommitText(); };
        editor.Focus();
    }
    private static Vector TextOffset(TextBox editor)
    {
        editor.UpdateLayout();
        var first = editor.GetRectFromCharacterIndex(0);
        return first.IsEmpty ? new Vector() : new Vector(first.X, first.Y);
    }
    public void CommitText()
    {
        if (_textEditor is not { } editor) return;
        var existing = _editedText;
        var offset = TextOffset(editor);
        var position = new Point(Canvas.GetLeft(editor) + offset.X, Canvas.GetTop(editor) + offset.Y);
        var text = editor.Text.TrimEnd();
        CloseTextEditor(editor);
        if (existing != null)
        {
            var previous = existing.Text;
            if (text == previous) return;
            if (text.Length == 0) { RemoveAnnotation(existing); return; }
            existing.Text = text;
            Record(new EditAction(() => existing.Text = previous, () => existing.Text = text));
        }
        else if (text.Length > 0)
        {
            // O fundo transparente torna a área inteira do texto clicável para reeditar, mover ou apagar.
            var block = new TextBlock { Text = text, Foreground = editor.Foreground, FontSize = editor.FontSize, FontFamily = editor.FontFamily, Background = Brushes.Transparent };
            MoveTo(block, position);
            Children.Add(block); RecordAdded(block);
        }
    }
    public void CancelText() { if (_textEditor is { } editor) CloseTextEditor(editor); }
    private void CloseTextEditor(TextBox editor)
    {
        var hadFocus = editor.IsKeyboardFocusWithin;
        // Limpe o estado antes de mover o foco, que dispara LostFocus.
        _textEditor = null;
        if (_editedText != null) _editedText.Visibility = Visibility.Visible;
        _editedText = null;
        if (hadFocus) Focus();
        Children.Remove(editor);
    }

    // --- Anotações e histórico

    private const double HitTolerance = 5;
    /// <summary>Anotação sob o ponto ou, se não houver, a que passa bem perto dele: linhas finas são difíceis de acertar.</summary>
    private UIElement? FindAnnotation(Point point)
    {
        if (AnnotationOf(InputHitTest(point) as DependencyObject) is { } exact) return exact;
        UIElement? near = null;
        VisualTreeHelper.HitTest(this,
            item => item is UIElement { IsVisible: false } or UIElement { IsHitTestVisible: false } ? HitTestFilterBehavior.ContinueSkipSelfAndChildren : HitTestFilterBehavior.Continue,
            result => (near = AnnotationOf(result.VisualHit)) == null ? HitTestResultBehavior.Continue : HitTestResultBehavior.Stop,
            new GeometryHitTestParameters(new EllipseGeometry(point, HitTolerance * Scale, HitTolerance * Scale)));
        return near;
    }
    private UIElement? AnnotationOf(DependencyObject? hit)
    {
        while (hit is Visual && hit != this && VisualTreeHelper.GetParent(hit) != this) hit = VisualTreeHelper.GetParent(hit);
        return hit is UIElement element && Children.IndexOf(element) > 0 ? element : null;
    }
    private void RemoveAnnotation(UIElement element)
    {
        var index = Children.IndexOf(element);
        Children.Remove(element);
        Record(new EditAction(() => InsertElement(index, element), () => RemoveElement(element)));
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
        CommitText();
        if (!_undo.TryPop(out var action)) return;
        try { action.Undo(); _redo.Push(action); }
        catch (Exception exception) { _undo.Push(action); DebugLog.Write("Falha ao desfazer anotação.", exception); }
    }
    public void Redo()
    {
        CommitText();
        if (!_redo.TryPop(out var action)) return;
        try { action.Redo(); _undo.Push(action); }
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
