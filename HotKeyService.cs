using System.Windows.Interop;

namespace QuickShot;

internal sealed class HotKeyService : IDisposable
{
    private const int HotKeyId = 9001;
    private readonly HwndSource _source;
    private bool _registered;
    public event Action? Pressed;

    public HotKeyService()
    {
        _source = new HwndSource(new HwndSourceParameters("QuickShotHotKey") { Width = 0, Height = 0, WindowStyle = 0 });
        _source.AddHook(WndProc);
    }
    public bool Register(uint modifiers, uint key)
    {
        if (_registered) NativeMethods.UnregisterHotKey(_source.Handle, HotKeyId);
        _registered = NativeMethods.RegisterHotKey(_source.Handle, HotKeyId, modifiers | NativeMethods.MOD_NOREPEAT, key);
        return _registered;
    }
    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotKeyId) { Pressed?.Invoke(); handled = true; }
        return 0;
    }
    public void Dispose() { if (_registered) NativeMethods.UnregisterHotKey(_source.Handle, HotKeyId); _source.Dispose(); }
}
