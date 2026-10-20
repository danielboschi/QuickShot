using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickShot;

internal static class ClipboardService
{
    public static void SetPng(BitmapSource bitmap)
    {
        // Evita que aplicativos que não entendem alfa pré-multiplicado interpretem a imagem como preta.
        var opaque = new FormatConvertedBitmap();
        opaque.BeginInit();
        opaque.Source = bitmap;
        opaque.DestinationFormat = System.Windows.Media.PixelFormats.Bgr32;
        opaque.EndInit();
        opaque.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(opaque));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        var data = new DataObject();
        data.SetData(DataFormats.Bitmap, opaque);
        stream.Position = 0;
        data.SetData("PNG", stream);
        Clipboard.SetDataObject(data, true);
    }
}
