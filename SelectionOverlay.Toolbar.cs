using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QuickShot;

/// <summary>Barra de ferramentas: criação, posição e preferências lembradas entre capturas.</summary>
public partial class SelectionOverlay
{
    private const double ToolbarMargin = 6;
    private readonly Dictionary<string, RadioButton> _swatches = new();
    private Slider? _thickness;
    private bool _loadingStyle;

    private static void PositionToolbar(Border toolbar, Rect region, MonitorArea monitor)
    {
        toolbar.LayoutTransform = new ScaleTransform(monitor.Scale, monitor.Scale);
        // MaxWidth é aplicado antes da ampliação; DesiredSize já a inclui.
        toolbar.MaxWidth = Math.Max(1, (monitor.Bounds.Width - ToolbarMargin * 2) / monitor.Scale);
        toolbar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var position = PlaceToolbar(region, toolbar.DesiredSize, monitor.Bounds);
        Canvas.SetLeft(toolbar, position.X); Canvas.SetTop(toolbar, position.Y);
    }

    /// <summary>Acima da seleção, abaixo dela ou, sem espaço, dentro dela; sempre dentro do monitor.</summary>
    internal static Point PlaceToolbar(Rect region, Size toolbar, Rect monitor)
    {
        var minX = monitor.Left + ToolbarMargin; var maxX = Math.Max(minX, monitor.Right - toolbar.Width - ToolbarMargin);
        var minY = monitor.Top + ToolbarMargin; var maxY = Math.Max(minY, monitor.Bottom - toolbar.Height - ToolbarMargin);
        var above = region.Top - toolbar.Height - ToolbarMargin;
        var below = region.Bottom + ToolbarMargin;
        var y = above >= minY ? above : below <= maxY ? below : region.Bottom - toolbar.Height - ToolbarMargin;
        return new Point(Math.Clamp(region.Left, minX, maxX), Math.Clamp(y, minY, maxY));
    }

    private Border CreateToolbar()
    {
        var settings = AppSettings.Current;
        var panel = new WrapPanel { Margin = new Thickness(5, 4, 5, 4), VerticalAlignment = VerticalAlignment.Center };
        var tools = new List<RadioButton>();
        foreach (var (icon, name, tool) in new[]
        {
            (ToolbarIcons.Move, "Mover anotação ou seleção", EditorTool.Move),
            (ToolbarIcons.Pen, "Caneta", EditorTool.Pen), (ToolbarIcons.Line, "Linha", EditorTool.Line), (ToolbarIcons.Arrow, "Seta", EditorTool.Arrow),
            (ToolbarIcons.Rectangle, "Retângulo", EditorTool.Rectangle), (ToolbarIcons.Ellipse, "Elipse", EditorTool.Ellipse), (ToolbarIcons.Text, "Texto", EditorTool.Text),
            (ToolbarIcons.Highlight, "Marca-texto", EditorTool.Highlight), (ToolbarIcons.Redact, "Tarja para ocultar", EditorTool.Redact), (ToolbarIcons.Erase, "Borracha", EditorTool.Erase)
        })
        {
            var button = new RadioButton { Style = (Style)Resources["ToolbarTool"], GroupName = "Tool", Content = ToolbarIcons.Create(icon), Tag = tool };
            button.Checked += (_, _) => ChooseTool(tool);
            tools.Add(button);
            AddToToolbar(panel, button, name);
        }
        AddSeparator(panel);
        AddAction(panel, ToolbarIcons.Undo, "Desfazer (Ctrl+Z)", () => _editor?.Undo());
        AddAction(panel, ToolbarIcons.Redo, "Refazer (Ctrl+Y)", () => _editor?.Redo());
        AddSeparator(panel);
        _swatches.Clear();
        foreach (var (name, value) in AppSettings.Palette)
        {
            var swatch = new RadioButton { Style = (Style)Resources["ToolbarColor"], GroupName = "Color", Background = CreateBrush(value) };
            swatch.Checked += (_, _) => ChooseColor(value);
            _swatches[value] = swatch;
            AddToToolbar(panel, swatch, name);
        }
        _thickness = new Slider { Style = (Style)Resources["ToolbarSlider"], Minimum = AppSettings.MinimumThickness, Maximum = AppSettings.MaximumThickness, Width = 80, Margin = new Thickness(8, 1, 4, 1) };
        _thickness.ValueChanged += (_, _) => ChooseThickness(_thickness.Value);
        AddToToolbar(panel, _thickness, "Espessura");
        AddSeparator(panel);
        AddAction(panel, ToolbarIcons.Save, "Salvar (Ctrl+S)", SaveCapture);
        AddAction(panel, ToolbarIcons.Copy, "Copiar (Ctrl+C ou Enter)", CopyCapture, "ToolbarPrimary");
        AddAction(panel, ToolbarIcons.Close, "Cancelar (Esc)", Close);
        if (settings.PrivateClipboard)
            panel.Children.Add(new TextBlock { Text = "Modo privado", FontSize = 12, Foreground = (Brush)Resources["ToolbarMuted"], VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) });
        // Marcar a ferramenta lembrada também carrega a cor e a espessura dela.
        tools.First(button => (EditorTool)button.Tag == settings.Tool).IsChecked = true;
        return new Border { Background = (Brush)Resources["ToolbarBackground"], BorderBrush = (Brush)Resources["ToolbarBorder"], BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12), Cursor = Cursors.Arrow, UseLayoutRounding = true, Child = panel };
    }

    private void ChooseTool(EditorTool tool)
    {
        if (_editor == null) return;
        _editor.Tool = tool;
        var settings = AppSettings.Current;
        if (AppSettings.IsRemembered(tool)) settings.Tool = tool;
        var highlight = tool == EditorTool.Highlight;
        var color = highlight ? settings.HighlightColor : settings.Color;
        var thickness = highlight ? settings.HighlightThickness : settings.Thickness;
        _loadingStyle = true;
        try
        {
            _swatches[color].IsChecked = true;
            if (_thickness != null) _thickness.Value = thickness;
        }
        finally { _loadingStyle = false; }
        _editor.DrawBrush = CreateBrush(color); _editor.DrawThickness = thickness;
    }
    private void ChooseColor(string value)
    {
        if (_editor == null || _loadingStyle) return;
        _editor.DrawBrush = CreateBrush(value);
        if (_editor.Tool == EditorTool.Highlight) AppSettings.Current.HighlightColor = value; else AppSettings.Current.Color = value;
    }
    private void ChooseThickness(double value)
    {
        if (_editor == null || _loadingStyle) return;
        _editor.DrawThickness = value;
        if (_editor.Tool == EditorTool.Highlight) AppSettings.Current.HighlightThickness = value; else AppSettings.Current.Thickness = value;
    }
    private static Brush CreateBrush(string value)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)!);
        brush.Freeze();
        return brush;
    }

    private void AddAction(Panel panel, Geometry icon, string name, Action action, string style = "ToolbarButton")
    {
        var button = new Button { Style = (Style)Resources[style], Content = ToolbarIcons.Create(icon) };
        button.Click += (_, _) => action();
        AddToToolbar(panel, button, name);
    }
    private static void AddToToolbar(Panel panel, Control control, string name)
    {
        control.ToolTip = name;
        AutomationProperties.SetName(control, name);
        panel.Children.Add(control);
    }
    private void AddSeparator(Panel panel) => panel.Children.Add(new Border { Width = 1, Height = 20, Margin = new Thickness(6, 0, 6, 0), Background = (Brush)Resources["ToolbarBorder"] });
}
