using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace QuickShot;

/// <summary>Ajuste da seleção pelas bordas e pelos cantos, com as anotações fixas sobre a imagem.</summary>
public partial class SelectionOverlay
{
    [Flags]
    internal enum ResizeEdges { None = 0, Left = 1, Top = 2, Right = 4, Bottom = 8 }

    private const double EdgeGrip = 10;
    private const double CornerGrip = 16;
    private const double MinimumSize = 10;
    private const double MiddleDotMinimum = 48;
    private readonly List<Thumb> _grips = new();
    private Rect _resizeOrigin;
    private Point _resizeStart;

    private void CreateGrips()
    {
        // Os cantos vêm por último para ficarem sobre as bordas.
        foreach (var (edges, name, cursor) in new[]
        {
            (ResizeEdges.Left, "Ajustar borda esquerda", Cursors.SizeWE), (ResizeEdges.Right, "Ajustar borda direita", Cursors.SizeWE),
            (ResizeEdges.Top, "Ajustar borda superior", Cursors.SizeNS), (ResizeEdges.Bottom, "Ajustar borda inferior", Cursors.SizeNS),
            (ResizeEdges.Left | ResizeEdges.Top, "Ajustar canto superior esquerdo", Cursors.SizeNWSE), (ResizeEdges.Right | ResizeEdges.Bottom, "Ajustar canto inferior direito", Cursors.SizeNWSE),
            (ResizeEdges.Right | ResizeEdges.Top, "Ajustar canto superior direito", Cursors.SizeNESW), (ResizeEdges.Left | ResizeEdges.Bottom, "Ajustar canto inferior esquerdo", Cursors.SizeNESW)
        })
        {
            var grip = new Thumb { Style = (Style)Resources["ResizeGrip"], Tag = edges, Cursor = cursor };
            AutomationProperties.SetName(grip, name);
            // A posição absoluta do mouse evita acúmulo de erro, pois a própria alça se move durante o arraste.
            grip.DragStarted += (_, _) => { _resizeOrigin = _editor?.Region ?? Rect.Empty; _resizeStart = Mouse.GetPosition(Surface); };
            grip.DragDelta += (_, _) =>
            {
                if (_editor == null || _resizeOrigin.IsEmpty) return;
                ApplyRegion(Resize(_resizeOrigin, edges, Mouse.GetPosition(Surface) - _resizeStart, new Size(_bounds.Width, _bounds.Height)));
                ShowSize(_editor.Region);
            };
            grip.DragCompleted += (_, _) => HideSize();
            Panel.SetZIndex(grip, 5);
            _grips.Add(grip); Surface.Children.Add(grip);
        }
    }

    /// <summary>Move apenas as bordas indicadas, em pixels inteiros, sem inverter a seleção nem sair da imagem.</summary>
    internal static Rect Resize(Rect origin, ResizeEdges edges, Vector delta, Size limits)
    {
        var left = origin.Left; var top = origin.Top; var right = origin.Right; var bottom = origin.Bottom;
        var minWidth = Math.Min(MinimumSize, origin.Width); var minHeight = Math.Min(MinimumSize, origin.Height);
        if (edges.HasFlag(ResizeEdges.Left)) left = Math.Clamp(Math.Round(left + delta.X), 0, right - minWidth);
        if (edges.HasFlag(ResizeEdges.Right)) right = Math.Clamp(Math.Round(right + delta.X), left + minWidth, limits.Width);
        if (edges.HasFlag(ResizeEdges.Top)) top = Math.Clamp(Math.Round(top + delta.Y), 0, bottom - minHeight);
        if (edges.HasFlag(ResizeEdges.Bottom)) bottom = Math.Clamp(Math.Round(bottom + delta.Y), top + minHeight, limits.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    private void BeginRegionMove() => _resizeOrigin = _editor?.Region ?? Rect.Empty;
    private void MoveRegion(Vector delta)
    {
        if (_resizeOrigin.IsEmpty) return;
        ApplyRegion(Shift(_resizeOrigin, delta, new Size(_bounds.Width, _bounds.Height)));
    }
    /// <summary>Desloca a seleção inteira, sem mudar o tamanho nem sair da imagem.</summary>
    internal static Rect Shift(Rect origin, Vector delta, Size limits) => new(
        Math.Clamp(Math.Round(origin.X + delta.X), 0, Math.Max(0, limits.Width - origin.Width)),
        Math.Clamp(Math.Round(origin.Y + delta.Y), 0, Math.Max(0, limits.Height - origin.Height)), origin.Width, origin.Height);

    private void ApplyRegion(Rect region)
    {
        if (_editor == null || _toolbar == null) return;
        var monitor = FindMonitor(region);
        _editor.Region = region;
        _editor.Scale = monitor.Scale;
        ShowSelection(region);
        Selection.StrokeThickness = 1.5 * monitor.Scale; SelectionShadow.StrokeThickness = 3 * monitor.Scale;
        foreach (var grip in _grips) PlaceGrip(grip, region, monitor.Scale);
        PositionToolbar(_toolbar, region, monitor);
    }

    private static void PlaceGrip(Thumb grip, Rect region, double scale)
    {
        var edges = (ResizeEdges)grip.Tag;
        var horizontal = edges.HasFlag(ResizeEdges.Left) || edges.HasFlag(ResizeEdges.Right);
        var vertical = edges.HasFlag(ResizeEdges.Top) || edges.HasFlag(ResizeEdges.Bottom);
        var corner = horizontal && vertical;
        grip.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
        // Medidas antes da ampliação; nas bordas, o comprimento acompanha a seleção.
        grip.Width = corner ? CornerGrip : horizontal ? EdgeGrip : region.Width / scale;
        grip.Height = corner ? CornerGrip : vertical ? EdgeGrip : region.Height / scale;
        var centerX = edges.HasFlag(ResizeEdges.Left) ? region.Left : edges.HasFlag(ResizeEdges.Right) ? region.Right : region.Left + region.Width / 2;
        var centerY = edges.HasFlag(ResizeEdges.Top) ? region.Top : edges.HasFlag(ResizeEdges.Bottom) ? region.Bottom : region.Top + region.Height / 2;
        Canvas.SetLeft(grip, centerX - grip.Width * scale / 2); Canvas.SetTop(grip, centerY - grip.Height * scale / 2);
        // Em seleções pequenas, o ponto do meio da borda ficaria sobre os dos cantos.
        grip.Foreground = corner || (horizontal ? region.Height : region.Width) >= MiddleDotMinimum * scale ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Transparent;
    }
}
