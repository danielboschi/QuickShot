using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace QuickShot;

/// <summary>
/// Mantém uma unidade do Canvas igual a um pixel físico da captura, em qualquer escala do Windows.
/// Uma única janela cobre todos os monitores: o WPF a desenha com um só DPI, que é compensado aqui,
/// e a barra e as alças são ampliadas conforme a escala do monitor em que aparecem.
/// </summary>
public partial class SelectionOverlay
{
    internal readonly record struct MonitorArea(Rect Bounds, double Scale);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        FitToPhysicalBounds();
    }
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // O Windows sugere um novo tamanho ao mudar o DPI; a janela precisa continuar cobrindo a captura.
        FitToPhysicalBounds();
        Dispatcher.BeginInvoke(FitToPhysicalBounds);
    }
    private void FitToPhysicalBounds()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == 0) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        Surface.LayoutTransform = dpi.DpiScaleX == 1 && dpi.DpiScaleY == 1 ? Transform.Identity : new ScaleTransform(1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY);
        if (NativeMethods.GetWindowRect(handle, out var rect) && rect.Left == _bounds.Left && rect.Top == _bounds.Top && rect.Right == _bounds.Right && rect.Bottom == _bounds.Bottom) return;
        if (!NativeMethods.SetWindowPos(handle, 0, _bounds.Left, _bounds.Top, _bounds.Width, _bounds.Height, NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE))
            DebugLog.Write($"Falha ao posicionar o seletor. Erro do Windows: {System.Runtime.InteropServices.Marshal.GetLastWin32Error()}.");
    }

    /// <summary>Monitor com a maior parte da região, em coordenadas da captura.</summary>
    private MonitorArea FindMonitor(Rect region)
    {
        var selected = new System.Drawing.Rectangle(_bounds.Left + (int)region.X, _bounds.Top + (int)region.Y, (int)region.Width, (int)region.Height);
        var screen = System.Windows.Forms.Screen.FromRectangle(selected).Bounds;
        return new MonitorArea(
            new Rect(screen.Left - _bounds.Left, screen.Top - _bounds.Top, screen.Width, screen.Height),
            NativeMethods.GetMonitorScale(screen.Left + screen.Width / 2, screen.Top + screen.Height / 2));
    }

    private void PlaceHint()
    {
        var cursor = System.Windows.Forms.Cursor.Position;
        var monitor = FindMonitor(new Rect(Math.Max(0, cursor.X - _bounds.Left), Math.Max(0, cursor.Y - _bounds.Top), 1, 1));
        Hint.LayoutTransform = new ScaleTransform(monitor.Scale, monitor.Scale);
        System.Windows.Controls.Canvas.SetLeft(Hint, monitor.Bounds.Left + 18 * monitor.Scale);
        System.Windows.Controls.Canvas.SetTop(Hint, monitor.Bounds.Top + 18 * monitor.Scale);
    }
}
