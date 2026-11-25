using System.Runtime.InteropServices;
using System.Text;

namespace QuickShot;

/// <summary>Localiza as janelas visíveis, da frente para o fundo. Lê apenas posição e tipo, nunca títulos ou conteúdo.</summary>
internal static class WindowFinder
{
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9, DWMWA_CLOAKED = 14, GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20;
    private const int MinimumSide = 8;

    public static IReadOnlyList<System.Drawing.Rectangle> VisibleWindows()
    {
        var windows = new List<System.Drawing.Rectangle>();
        try
        {
            EnumWindows((handle, _) =>
            {
                if (TryGetBounds(handle, out var bounds)) windows.Add(bounds);
                return true;
            }, 0);
        }
        catch (Exception exception) { DebugLog.Write("Falha ao localizar janelas; a seleção por arraste continua disponível.", exception); }
        return windows;
    }

    private static bool TryGetBounds(nint handle, out System.Drawing.Rectangle bounds)
    {
        bounds = default;
        if (!IsWindowVisible(handle) || IsIconic(handle)) return false;
        GetWindowThreadProcessId(handle, out var process);
        if (process == Environment.ProcessId) return false;
        // Sobreposições que deixam o mouse passar não são alvos, assim como janelas ocultas pelo sistema.
        if (((long)GetWindowLongPtr(handle, GWL_EXSTYLE) & WS_EX_TRANSPARENT) != 0) return false;
        if (DwmGetWindowAttribute(handle, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return false;
        var name = new StringBuilder(64);
        GetClassName(handle, name, name.Capacity);
        // A área de trabalho não é uma janela a capturar: nela, vale o monitor inteiro.
        if (name.ToString() is "Progman" or "WorkerW") return false;
        // Sem as bordas invisíveis de redimensionamento, incluídas por GetWindowRect.
        if (DwmGetWindowAttribute(handle, DWMWA_EXTENDED_FRAME_BOUNDS, out NativeMethods.RECT rect, Marshal.SizeOf<NativeMethods.RECT>()) != 0 && !NativeMethods.GetWindowRect(handle, out rect)) return false;
        bounds = System.Drawing.Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return bounds.Width >= MinimumSide && bounds.Height >= MinimumSide;
    }

    private delegate bool EnumWindowsCallback(nint handle, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint handle);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out int process);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint handle, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint handle, StringBuilder name, int length);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint handle, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint handle, int attribute, out NativeMethods.RECT value, int size);
}
