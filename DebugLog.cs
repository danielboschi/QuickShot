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
                // Limite o uso de disco, preservando também o arquivo anterior.
                if (File.Exists(Path) && new FileInfo(Path).Length >= 1024 * 1024)
                    File.Move(Path, Path + ".old", overwrite: true);
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                if (exception != null) entry += exception + Environment.NewLine;
                File.AppendAllText(Path, entry);
            }
        }
        catch { /* O log jamais deve afetar a captura. */ }
    }
}
