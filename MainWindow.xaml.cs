using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QuickShot;

public partial class MainWindow : Window
{
    private static MainWindow? _instance;
    public MainWindow()
    {
        DebugLog.Write("Criando janela do editor.");
        InitializeComponent();
        Icon = AppIcon.CreateImageSource();
        Closing += (_, e) => { e.Cancel = true; Hide(); };
        _instance = this;
        ColorChanged(null!, null!);
        ThicknessChanged(null!, null!);
    }
    public static void ShowCapture(BitmapSource image)
    {
        DebugLog.Write("Enviando captura para o editor.");
        if (_instance == null) _instance = new MainWindow();
        _instance.Editor.SetImage(image);
        _instance.Show();
        _instance.Activate();
    }
    public static void ShowEmptyEditor()
    {
        if (_instance == null) _instance = new MainWindow();
        _instance.Show(); _instance.Activate();
    }
    private void ToolClick(object sender, RoutedEventArgs e) => Editor.Tool = Enum.Parse<EditorTool>((string)((Button)sender).Tag);
    private void ColorChanged(object sender, SelectionChangedEventArgs e)
    {
        // Este evento é disparado durante InitializeComponent, antes de Editor existir.
        if (Editor == null || ColorBox?.SelectedItem is not ComboBoxItem item || item.Tag is not string color) return;
        Editor.DrawBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)!);
    }
    private void ThicknessChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { if (Editor != null && Thickness != null) Editor.DrawThickness = Thickness.Value; }
    private void UndoClick(object sender, RoutedEventArgs e) => Editor.Undo();
    private void RedoClick(object sender, RoutedEventArgs e) => Editor.Redo();
    private void ShortcutChanged(object sender, RoutedEventArgs e) => CaptureController.SetShortcut(CtrlPrint.IsChecked == true);
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C) { CopyToClipboard(); e.Handled = true; }
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) Editor.Undo();
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z) Editor.Redo();
    }
    private void CopyClick(object sender, RoutedEventArgs e) => CopyToClipboard();
    private void CopyToClipboard()
    {
        if (Editor.ActualWidth < 1 || Editor.ActualHeight < 1) return;
        Editor.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(Editor.ActualWidth), (int)Math.Ceiling(Editor.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(Editor); bitmap.Freeze();
        ClipboardService.SetPng(bitmap);
    }
}
