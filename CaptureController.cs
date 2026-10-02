using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickShot;

internal static class CaptureController
{
    private static HotKeyService? _hotKey;
    public static void Initialize()
    {
        _hotKey = new HotKeyService();
        _hotKey.Pressed += Start;
        _hotKey.Register(0, NativeMethods.VK_SNAPSHOT);
    }
    public static void SetShortcut(bool withCtrl) => _hotKey?.Register(withCtrl ? NativeMethods.MOD_CONTROL : 0, NativeMethods.VK_SNAPSHOT);
    public static void Start()
    {
        DebugLog.Write("Início de captura solicitado.");
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (Application.Current.Windows.OfType<SelectionOverlay>().Any()) { DebugLog.Write("Captura ignorada: seletor já aberto."); return; }
            new SelectionOverlay().Show();
        });
    }
    public static void Dispose()
    {
        _hotKey?.Dispose();
        _hotKey = null;
    }
}
