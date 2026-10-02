using System.Runtime.InteropServices;

namespace QuickShot;

internal static class NativeMethods
{
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_NOREPEAT = 0x4000;
    public const uint VK_SNAPSHOT = 0x2C;
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool UnregisterHotKey(nint hWnd, int id);
}
