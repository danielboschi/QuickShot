using System.IO;

namespace QuickShot;

internal static class SafeFile
{
    /// <summary>Grava em um arquivo temporário e só então substitui o destino, preservando-o se a gravação falhar.</summary>
    public static void Write(string path, Action<Stream> write)
    {
        var fullPath = Path.GetFullPath(path);
        var temporary = Path.Combine(Path.GetDirectoryName(fullPath)!, $".quickshot-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporary); } catch { /* Preserve o erro original da gravação. */ }
            throw;
        }
    }
}
