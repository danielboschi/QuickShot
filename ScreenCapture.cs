using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickShot;

internal static class ScreenCapture
{
    public static BitmapSource Capture(Rect area)
    {
        using var bitmap = new Bitmap((int)area.Width, (int)area.Height, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen((int)area.X, (int)area.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        // Leia o buffer existente, sem criar uma segunda imagem GDI com GetHbitmap.
        var pixels = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppRgb);
        try
        {
            var source = BitmapSource.Create(bitmap.Width, bitmap.Height, 96, 96, PixelFormats.Bgr32,
                null, pixels.Scan0, checked(pixels.Stride * bitmap.Height), pixels.Stride);
            source.Freeze();
            return source;
        }
        finally { bitmap.UnlockBits(pixels); }
    }
}
