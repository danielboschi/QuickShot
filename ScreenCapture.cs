using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace QuickShot;

internal static class ScreenCapture
{
    public static BitmapSource Capture(Rect area)
    {
        using var bitmap = new Bitmap((int)area.Width, (int)area.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen((int)area.X, (int)area.Y, 0, 0, bitmap.Size, CopyPixelOperation.SourceCopy);
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, nint.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally { DeleteObject(hBitmap); }
    }
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteObject(nint hObject);
}
