using System.IO;

namespace QuickShot;

internal static class DebugLog
{
    private static readonly object Sync = new();
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "quickshot-debug.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                File.AppendAllText(Path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
                if (exception != null) File.AppendAllText(Path, exception + Environment.NewLine);
            }
        }
        catch { /* O log jamais deve afetar a captura. */ }
    }
}
