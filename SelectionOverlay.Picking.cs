using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickShot;

/// <summary>Escolha da região: arraste, clique em uma janela ou repetição da última.</summary>
public partial class SelectionOverlay
{
    private const double DragThreshold = 4;
    private readonly IReadOnlyList<System.Drawing.Rectangle> _windows;
    private Point _start;
    private bool _pressed;
    private bool _selecting;
    private Rect _hovered = Rect.Empty;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (!_editing && !_pressed) Hover(Mouse.GetPosition(Surface));
    }
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_editing) return;
        _start = e.GetPosition(Surface); _pressed = true; _selecting = false;
        CaptureMouse();
    }
    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_editing) return;
        var point = e.GetPosition(Surface);
        if (!_pressed) { Hover(point); return; }
        if (!_selecting && (point - _start).Length >= DragThreshold) { _selecting = true; Hint.Visibility = Visibility.Collapsed; }
        if (_selecting) DrawSelection(point);
    }
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed || _editing) return;
        var end = e.GetPosition(Surface);
        var region = _selecting ? ToRegion(_start, end) : FindTarget(_start);
        // Encerre o gesto antes de liberar o mouse, que dispara LostMouseCapture.
        _pressed = false; _selecting = false;
        ReleaseMouseCapture();
        if (region.IsEmpty || region.Width < 2 || region.Height < 2) { ResetPicking(); return; }
        TryStartEditing(region);
    }
    private void OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_pressed || IsMouseCaptured) return;
        _pressed = false; _selecting = false;
        ResetPicking();
    }
    private void ResetPicking() { _hovered = Rect.Empty; HideTarget(); Hint.Visibility = Visibility.Visible; }

    private void TryStartEditing(Rect region)
    {
        try
        {
            StartEditing(region);
            DebugLog.Write("Editor sobreposto aberto.");
        }
        catch (Exception exception)
        {
            DebugLog.Write("Falha ao iniciar o editor sobreposto.", exception);
            MessageBox.Show($"Não foi possível capturar a tela.\n\n{exception.Message}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }

    private void DrawSelection(Point point) => ShowTarget(ToRegion(_start, point));
    private void Hover(Point point)
    {
        var target = FindTarget(point);
        if (target == _hovered) return;
        _hovered = target;
        if (target.IsEmpty) HideTarget(); else ShowTarget(target);
    }
    private void ShowTarget(Rect region)
    {
        if (region.IsEmpty) { HideTarget(); return; }
        ShowSelection(region);
        // A parte escolhida aparece sem o escurecimento.
        Bright.Clip = new RectangleGeometry(region); Bright.Visibility = Visibility.Visible;
        ShowSize(region);
    }
    private void HideTarget()
    {
        Selection.Visibility = Visibility.Collapsed; SelectionShadow.Visibility = Visibility.Collapsed; Bright.Visibility = Visibility.Collapsed;
        HideSize();
    }

    /// <summary>Região entre dois pontos, em pixels inteiros e dentro da imagem, condição para recortar a captura.</summary>
    private Rect ToRegion(Point from, Point to)
    {
        var region = new Rect(Math.Round(Math.Min(from.X, to.X)), Math.Round(Math.Min(from.Y, to.Y)), Math.Round(Math.Abs(to.X - from.X)), Math.Round(Math.Abs(to.Y - from.Y)));
        region.Intersect(new Rect(0, 0, _bounds.Width, _bounds.Height));
        return region;
    }

    /// <summary>Janela da frente sob o ponto ou, fora de qualquer janela, o monitor inteiro.</summary>
    internal Rect FindTarget(Point point)
    {
        var x = _bounds.Left + (int)Math.Floor(point.X); var y = _bounds.Top + (int)Math.Floor(point.Y);
        var target = _windows.FirstOrDefault(window => window.Contains(x, y));
        if (target.IsEmpty) target = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x, y)).Bounds;
        return ToCapture(target);
    }
    private Rect ToCapture(System.Drawing.Rectangle area)
    {
        var visible = System.Drawing.Rectangle.Intersect(area, _bounds);
        return visible.IsEmpty ? Rect.Empty : new Rect(visible.X - _bounds.X, visible.Y - _bounds.Y, visible.Width, visible.Height);
    }

    // --- Última região

    private void RememberRegion()
    {
        if (_editor?.Region is not { IsEmpty: false } region) return;
        AppSettings.Current.LastRegion = new System.Drawing.Rectangle(_bounds.X + (int)region.X, _bounds.Y + (int)region.Y, (int)region.Width, (int)region.Height);
    }
    /// <summary>Repete a última região, limitada à área de trabalho atual. Sem ela, mantém a escolha livre.</summary>
    internal bool RepeatLastRegion()
    {
        if (_editing || _pressed || AppSettings.Current.LastRegion is not { } last) return false;
        var region = ToCapture(last);
        if (region.IsEmpty || region.Width < 2 || region.Height < 2) return false;
        TryStartEditing(region);
        return true;
    }

    // --- Medidas

    private void ShowSize(Rect region)
    {
        var scale = FindMonitor(region).Scale;
        SizeText.Text = $"{region.Width:0} × {region.Height:0}";
        SizeLabel.LayoutTransform = new ScaleTransform(scale, scale);
        SizeLabel.Visibility = Visibility.Visible;
        SizeLabel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var label = SizeLabel.DesiredSize; var gap = 6 * scale;
        // Dentro da seleção, junto ao canto; nas pequenas, logo abaixo, sem sair da tela.
        var inside = region.Width >= label.Width + gap * 2 && region.Height >= label.Height + gap * 2;
        var x = inside ? region.Left + gap : region.Left;
        var y = inside ? region.Top + gap : region.Bottom + gap;
        Canvas.SetLeft(SizeLabel, Math.Clamp(x, 0, Math.Max(0, _bounds.Width - label.Width)));
        Canvas.SetTop(SizeLabel, Math.Clamp(y, 0, Math.Max(0, _bounds.Height - label.Height)));
    }
    private void HideSize() => SizeLabel.Visibility = Visibility.Collapsed;
}
