using System.Drawing;

namespace QuickShot;

/// <summary>Ícone do aplicativo. O arquivo é gerado por Assets\build-icon.ps1.</summary>
internal static class AppIcon
{
    // O arquivo traz um desenho próprio para cada tamanho; o Windows recebe o da bandeja, sem redimensionar.
    public static Icon CreateIcon()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("QuickShot.ico")
            ?? throw new InvalidOperationException("O ícone do aplicativo não foi encontrado.");
        return new Icon(stream, System.Windows.Forms.SystemInformation.SmallIconSize);
    }
}
