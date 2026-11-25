using System.IO;

namespace QuickShot;

internal static class DebugLog
{
    private static readonly object Sync = new();
    /// <summary>Fora da pasta do programa, para funcionar também quando ela não permite gravação.</summary>
    public static string Path { get; internal set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickShot", "quickshot-debug.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
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
