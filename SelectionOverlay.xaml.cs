using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;

namespace QuickShot;

public partial class SelectionOverlay : Window
{
    private Point _start;
    private bool _selecting;
    private bool _editing;
    private DrawingSurface? _editor;
    private Border? _toolbar;
    private readonly BitmapSource _desktopSnapshot;
    private BitmapSource? _selectedImage;
    public SelectionOverlay()
    {
        InitializeComponent();
        var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
        Left = bounds.Left; Top = bounds.Top; Width = bounds.Width; Height = bounds.Height;
        // A captura acontece antes de a camada translúcida existir na tela.
        _desktopSnapshot = ScreenCapture.Capture(new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
        DebugLog.Write("Snapshot do desktop criado antes da seleção.");
    }
    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (_editing) return;
        _start = e.GetPosition(Surface); _selecting = true; Selection.Visibility = Visibility.Visible; SelectionShadow.Visibility = Visibility.Visible; Hint.Visibility = Visibility.Collapsed; CaptureMouse();
    }
    private void OnMouseMove(object sender, MouseEventArgs e) { if (_selecting && !_editing) DrawSelection(e.GetPosition(Surface)); }
    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_selecting || _editing) return;
        _selecting = false; ReleaseMouseCapture();
        var end = e.GetPosition(Surface); var x = Math.Min(_start.X, end.X); var y = Math.Min(_start.Y, end.Y);
        var rect = new Rect(Left + x, Top + y, Math.Abs(end.X - _start.X), Math.Abs(end.Y - _start.Y));
        if (rect.Width < 2 || rect.Height < 2) { Selection.Visibility = Visibility.Collapsed; SelectionShadow.Visibility = Visibility.Collapsed; Hint.Visibility = Visibility.Visible; return; }
        try
        {
            var crop = new Int32Rect((int)Math.Round(rect.X - Left), (int)Math.Round(rect.Y - Top), (int)Math.Round(rect.Width), (int)Math.Round(rect.Height));
            var image = new CroppedBitmap(_desktopSnapshot, crop);
            image.Freeze();
            _selectedImage = image;
            StartEditing(image, new Rect(x, y, rect.Width, rect.Height));
        }
        catch (Exception exception)
        {
            DebugLog.Write("Falha ao iniciar o editor sobreposto.", exception);
            MessageBox.Show($"Não foi possível capturar a tela.\n\n{exception.Message}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }
    }
    private void DrawSelection(Point point)
    {
        var x = Math.Min(_start.X, point.X); var y = Math.Min(_start.Y, point.Y);
        var width = Math.Abs(point.X - _start.X); var height = Math.Abs(point.Y - _start.Y);
        Canvas.SetLeft(SelectionShadow, x); Canvas.SetTop(SelectionShadow, y); SelectionShadow.Width = width; SelectionShadow.Height = height;
        Canvas.SetLeft(Selection, x); Canvas.SetTop(Selection, y); Selection.Width = width; Selection.Height = height;
    }
    private void StartEditing(System.Windows.Media.Imaging.BitmapSource image, Rect canvasRegion)
    {
        _editor = new DrawingSurface { Focusable = true };
        _editor.SetImage(image);
        _editor.Width = canvasRegion.Width;
        _editor.Height = canvasRegion.Height;
        Canvas.SetLeft(_editor, canvasRegion.X); Canvas.SetTop(_editor, canvasRegion.Y);
        Panel.SetZIndex(_editor, 2); Surface.Children.Add(_editor);
        // A moldura fica visível sobre a captura. A exportação compõe apenas a imagem e as anotações.
        Selection.Fill = Brushes.Transparent;
        Panel.SetZIndex(SelectionShadow, 3);
        Panel.SetZIndex(Selection, 4);
        _toolbar = CreateToolbar();
        var toolbarY = canvasRegion.Y - 44 >= 0 ? canvasRegion.Y - 44 : Math.Min(Height - 44, canvasRegion.Bottom + 6);
        Canvas.SetLeft(_toolbar, canvasRegion.X); Canvas.SetTop(_toolbar, toolbarY);
        Panel.SetZIndex(_toolbar, 5); Surface.Children.Add(_toolbar);
        _editing = true;
        _editor.Focus();
        DebugLog.Write("Editor sobreposto aberto.");
    }

    private Border CreateToolbar()
    {
        var panel = new WrapPanel { Margin = new Thickness(5) };
        foreach (var (label, name, tool) in new[]
        {
            ("✎", "Caneta", EditorTool.Pen), ("╱", "Linha", EditorTool.Line), ("➜", "Seta", EditorTool.Arrow),
            ("□", "Retângulo", EditorTool.Rectangle), ("○", "Elipse", EditorTool.Ellipse), ("T", "Texto", EditorTool.Text),
            ("▰", "Marca-texto", EditorTool.Highlight), ("⌫", "Borracha", EditorTool.Erase)
        })
        {
            var button = new Button { Content = label, Tag = tool, MinWidth = 28, Margin = new Thickness(1), ToolTip = name };
            AutomationProperties.SetName(button, name);
            button.Click += (sender, _) => { if (_editor != null) _editor.Tool = (EditorTool)((Button)sender).Tag; };
            panel.Children.Add(button);
        }
        var undo = new Button { Content = "↶", ToolTip = "Desfazer", Margin = new Thickness(3, 1, 1, 1) };
        undo.Click += (_, _) => _editor?.Undo(); panel.Children.Add(undo);
        var redo = new Button { Content = "↷", ToolTip = "Refazer", Margin = new Thickness(1) };
        redo.Click += (_, _) => _editor?.Redo(); panel.Children.Add(redo);
        var color = new ComboBox { Width = 82, SelectedIndex = 0, Margin = new Thickness(5, 1, 1, 1), ToolTip = "Cor" };
        foreach (var (name, value) in new[] { ("Vermelho", "#E53935"), ("Azul", "#1E88E5"), ("Verde", "#43A047"), ("Preto", "#111111"), ("Branco", "#FFFFFF") }) color.Items.Add(new ComboBoxItem { Content = name, Tag = value });
        color.SelectionChanged += (_, _) =>
        {
            if (_editor != null && color.SelectedItem is ComboBoxItem item && item.Tag is string value) _editor.DrawBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)!);
        };
        panel.Children.Add(color);
        var thickness = new Slider { Minimum = 1, Maximum = 24, Value = 3, Width = 70, Margin = new Thickness(5, 1, 1, 1), ToolTip = "Espessura" };
        thickness.ValueChanged += (_, _) => { if (_editor != null) _editor.DrawThickness = thickness.Value; };
        panel.Children.Add(thickness);
        var save = new Button { Content = "💾", ToolTip = "Salvar captura…", Margin = new Thickness(4, 1, 1, 1), MinWidth = 30 };
        save.Click += (_, _) => SaveCapture(); panel.Children.Add(save);
        panel.Children.Add(new TextBlock { Text = "  Ctrl+C: copiar", Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 2, 0) });
        return new Border { Background = new SolidColorBrush(Color.FromArgb(235, 35, 35, 35)), BorderBrush = Brushes.White, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(4), Child = panel };
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { Close(); return; }
        if (!_editing || _editor == null) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            try
            {
                ClipboardService.SetPng(CreateFinalImage());
                DebugLog.Write("Anotação sobreposta copiada para o Clipboard.");
                Close();
            }
            catch (Exception exception) { DebugLog.Write("Falha ao copiar composição.", exception); MessageBox.Show($"Não foi possível copiar a captura.\n\n{exception.Message}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error); }
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { _editor.Undo(); e.Handled = true; }
        else if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Z) { _editor.Redo(); e.Handled = true; }
    }

    private BitmapSource CreateFinalImage()
    {
        if (_editor == null || _selectedImage == null) throw new InvalidOperationException("Não há captura ativa.");
        _editor.UpdateLayout();
        var width = (int)Math.Ceiling(_editor.ActualWidth);
        var height = (int)Math.Ceiling(_editor.ActualHeight);
        var annotations = _editor.RenderAnnotations();
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.DrawImage(_selectedImage, new Rect(0, 0, width, height));
            context.DrawImage(annotations, new Rect(0, 0, width, height));
        }
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private void SaveCapture()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Salvar captura",
            Filter = "Imagem PNG (*.png)|*.png",
            DefaultExt = ".png",
            AddExtension = true,
            FileName = $"QuickShot-{DateTime.Now:yyyyMMdd-HHmmss}.png"
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var image = CreateFinalImage();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var file = File.Create(dialog.FileName);
            encoder.Save(file);
            DebugLog.Write($"Captura salva: {dialog.FileName}");
            Close();
        }
        catch (Exception exception)
        {
            DebugLog.Write("Falha ao salvar captura.", exception);
            MessageBox.Show($"Não foi possível salvar a captura.\n\n{exception.Message}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
