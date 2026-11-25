using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace QuickShot;

internal static class ClipboardService
{
    // Formatos documentados pelo Windows para quem copia dados sensíveis.
    internal const string ExcludeFromMonitors = "ExcludeClipboardContentFromMonitorProcessing";
    internal const string AllowHistory = "CanIncludeInClipboardHistory";
    internal const string AllowCloud = "CanUploadToCloudClipboard";

    public static void SetPng(BitmapSource bitmap, bool keepPrivate) => Clipboard.SetDataObject(CreatePngData(bitmap, keepPrivate), true);

    internal static DataObject CreatePngData(BitmapSource bitmap, bool keepPrivate)
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
        var stream = new MemoryStream();
        encoder.Save(stream);
        var data = new DataObject();
        data.SetData(DataFormats.Bitmap, opaque);
        stream.Position = 0;
        data.SetData("PNG", stream);
        // Cada valor é um DWORD zero: "não permitido". A colagem normal não muda.
        if (keepPrivate)
            foreach (var format in new[] { ExcludeFromMonitors, AllowHistory, AllowCloud }) data.SetData(format, new MemoryStream(new byte[sizeof(int)]));
        return data;
    }
}
