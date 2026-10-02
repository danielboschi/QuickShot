using System.Drawing;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingBrush = System.Drawing.SolidBrush;
using DrawingPen = System.Drawing.Pen;
using DrawingColor = System.Drawing.Color;

namespace QuickShot;

internal static class AppIcon
{
    public static Icon CreateIcon()
    {
        using var bitmap = DrawBitmap(32);
        var handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally { DestroyIcon(handle); }
    }

    public static ImageSource CreateImageSource()
    {
        using var bitmap = DrawBitmap(64);
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(handle, nint.Zero, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally { DeleteObject(handle); }
    }

    private static Bitmap DrawBitmap(int size)
    {
        var bitmap = new Bitmap(size, size);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(DrawingColor.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var background = new DrawingBrush(DrawingColor.FromArgb(255, 31, 115, 203));
        using var white = new DrawingBrush(DrawingColor.White);
        using var outline = new DrawingPen(DrawingColor.White, Math.Max(1, size / 12f));
        graphics.FillEllipse(background, 1, 1, size - 2, size - 2);
        var margin = size * .24f;
        graphics.DrawRectangle(outline, margin, margin, size - margin * 2, size - margin * 2);
        graphics.FillRectangle(white, size * .43f, size * .10f, size * .14f, size * .80f);
        graphics.FillRectangle(white, size * .10f, size * .43f, size * .80f, size * .14f);
        return bitmap;
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(nint hIcon);
}
