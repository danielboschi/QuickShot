using System.Globalization;
using System.IO;
using System.Text;

namespace QuickShot;

/// <summary>Preferências locais, em um arquivo de texto simples. Valores inválidos voltam ao padrão.</summary>
internal sealed class AppSettings
{
    public const double MinimumThickness = 1, MaximumThickness = 24;
    public static readonly (string Name, string Value)[] Palette =
    {
        ("Vermelho", "#E53935"), ("Amarelo", "#FDD835"), ("Verde", "#43A047"), ("Azul", "#1E88E5"), ("Preto", "#111111"), ("Branco", "#FFFFFF")
    };

    private static AppSettings? _current;
    private string _saved = "";
    public static AppSettings Current { get => _current ??= Load(); internal set => _current = value; }
    public static string Path { get; internal set; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickShot", "settings.ini");

    public EditorTool Tool { get; set; } = EditorTool.Pen;
    public string Color { get; set; } = "#E53935";
    public double Thickness { get; set; } = 3;
    /// <summary>O marca-texto tem cor e espessura próprias.</summary>
    public string HighlightColor { get; set; } = "#FDD835";
    public double HighlightThickness { get; set; } = 18;
    /// <summary>Pede ao Windows que a captura copiada não entre no histórico nem na nuvem da área de transferência.</summary>
    public bool PrivateClipboard { get; set; }
    /// <summary>Última região copiada ou salva, em pixels da área de trabalho.</summary>
    public System.Drawing.Rectangle? LastRegion { get; set; }

    /// <summary>Borracha e mover são momentâneas: a próxima captura volta à última ferramenta de desenho.</summary>
    public static bool IsRemembered(EditorTool tool) => Enum.IsDefined(tool) && tool is not (EditorTool.Erase or EditorTool.Move);

    internal static AppSettings Load()
    {
        var settings = new AppSettings();
        try
        {
            if (File.Exists(Path))
                foreach (var line in File.ReadLines(Path).Take(50))
                {
                    var separator = line.IndexOf('=');
                    if (separator > 0) settings.Apply(line[..separator].Trim(), line[(separator + 1)..].Trim());
                }
        }
        catch (Exception exception) { DebugLog.Write($"Preferências não lidas: {exception.GetType().Name} 0x{exception.HResult:X8}."); }
        settings._saved = settings.Serialize();
        return settings;
    }

    private void Apply(string key, string value)
    {
        switch (key)
        {
            case "tool": if (Enum.TryParse<EditorTool>(value, out var tool) && IsRemembered(tool)) Tool = tool; break;
            case "color": if (IsColor(value)) Color = value; break;
            case "thickness": if (TryThickness(value, out var thickness)) Thickness = thickness; break;
            case "highlightColor": if (IsColor(value)) HighlightColor = value; break;
            case "highlightThickness": if (TryThickness(value, out var highlight)) HighlightThickness = highlight; break;
            case "privateClipboard": PrivateClipboard = value == "true"; break;
            case "lastRegion":
                var parts = value.Split(',');
                var numbers = new int[4];
                if (parts.Length == 4 && parts.Select((part, index) => int.TryParse(part, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out numbers[index])).ToArray().All(valid => valid)
                    && numbers[2] is > 1 and < 100000 && numbers[3] is > 1 and < 100000 && Math.Abs((long)numbers[0]) < 100000 && Math.Abs((long)numbers[1]) < 100000)
                    LastRegion = new System.Drawing.Rectangle(numbers[0], numbers[1], numbers[2], numbers[3]);
                break;
        }
    }
    private static bool IsColor(string value) => Palette.Any(color => color.Value == value);
    private static bool TryThickness(string value, out double thickness) =>
        double.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out thickness) && thickness is >= MinimumThickness and <= MaximumThickness;

    private string Serialize()
    {
        var text = new StringBuilder();
        void Line(string key, string value) => text.Append(key).Append('=').Append(value).Append('\n');
        Line("tool", Tool.ToString()); Line("color", Color); Line("thickness", Thickness.ToString("0.##", CultureInfo.InvariantCulture));
        Line("highlightColor", HighlightColor); Line("highlightThickness", HighlightThickness.ToString("0.##", CultureInfo.InvariantCulture));
        Line("privateClipboard", PrivateClipboard ? "true" : "false");
        if (LastRegion is { } region) Line("lastRegion", string.Create(CultureInfo.InvariantCulture, $"{region.X},{region.Y},{region.Width},{region.Height}"));
        return text.ToString();
    }

    /// <summary>Grava somente quando algo mudou.</summary>
    public void Save()
    {
        try
        {
            var text = Serialize();
            if (text == _saved) return;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            SafeFile.Write(Path, stream => stream.Write(Encoding.UTF8.GetBytes(text)));
            _saved = text;
        }
        catch (Exception exception) { DebugLog.Write($"Preferências não gravadas: {exception.GetType().Name} 0x{exception.HResult:X8}."); }
    }
}
