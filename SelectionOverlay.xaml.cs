using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace QuickShot;

public partial class SelectionOverlay : Window
{
    private bool _editing;
    private DrawingSurface? _editor;
    private Border? _toolbar;
    private BitmapSource? _desktopSnapshot;
    private readonly System.Drawing.Rectangle _bounds;
    internal SelectionOverlay(BitmapSource desktopSnapshot, System.Drawing.Rectangle bounds, IReadOnlyList<System.Drawing.Rectangle>? windows = null)
    {
        _desktopSnapshot = desktopSnapshot;
        _bounds = bounds;
        _windows = windows ?? Array.Empty<System.Drawing.Rectangle>();
        InitializeComponent();
        // Valores iniciais em unidades do WPF; o tamanho exato, em pixels, vem de FitToPhysicalBounds.
        Left = bounds.Left; Top = bounds.Top; Width = bounds.Width; Height = bounds.Height;
        // A seleção e o recorte usam a mesma imagem; o desktop ao vivo fica coberto.
        Background = new ImageBrush(_desktopSnapshot) { Stretch = Stretch.Fill };
        Bright.Source = _desktopSnapshot; Bright.Width = bounds.Width; Bright.Height = bounds.Height;
        Panel.SetZIndex(Hint, 8); Panel.SetZIndex(SizeLabel, 7);
        PlaceHint();
    }

    /// <summary>Percorre o caminho de uma captura sem exibir janela, gravar arquivo ou tocar na área de transferência.</summary>
    internal static void WarmUp()
    {
        var bounds = new System.Drawing.Rectangle(0, 0, 64, 64);
        var snapshot = ScreenCapture.Capture(new Rect(0, 0, bounds.Width, bounds.Height));
        WindowFinder.VisibleWindows();
        var overlay = new SelectionOverlay(snapshot, bounds);
        try
        {
            new System.Windows.Interop.WindowInteropHelper(overlay).EnsureHandle();
            overlay.StartEditing(new Rect(0, 0, bounds.Width, bounds.Height));
            overlay.Surface.Measure(new Size(bounds.Width, bounds.Height));
            overlay.Surface.Arrange(new Rect(0, 0, bounds.Width, bounds.Height));
            ClipboardService.CreatePngData(overlay.CreateFinalImage(), keepPrivate: false);
        }
        finally { overlay.Close(); }
    }
    protected override void OnClosed(EventArgs e)
    {
        AppSettings.Current.Save();
        // A primeira Window pode continuar referenciada por Application.MainWindow.
        // Libere imagens, anotações e histórico mesmo antes da próxima coleta de lixo.
        Background = null; Bright.Source = null;
        _desktopSnapshot = null;
        _editor?.ClearImage();
        Surface.Children.Clear();
        _editor = null; _toolbar = null; _grips.Clear();
        if (ReferenceEquals(Application.Current.MainWindow, this)) Application.Current.MainWindow = null;
        base.OnClosed(e);
    }

    private void ShowSelection(Rect region)
    {
        Canvas.SetLeft(SelectionShadow, region.X); Canvas.SetTop(SelectionShadow, region.Y); SelectionShadow.Width = region.Width; SelectionShadow.Height = region.Height;
        Canvas.SetLeft(Selection, region.X); Canvas.SetTop(Selection, region.Y); Selection.Width = region.Width; Selection.Height = region.Height;
        Selection.Visibility = Visibility.Visible; SelectionShadow.Visibility = Visibility.Visible;
    }
    private void StartEditing(Rect region)
    {
        // O editor cobre a captura inteira e mostra só a região, para ela poder ser ajustada depois.
        _editor = new DrawingSurface { Focusable = true };
        _editor.SetImage(_desktopSnapshot ?? throw new InvalidOperationException("Não há captura ativa."));
        Canvas.SetLeft(_editor, 0); Canvas.SetTop(_editor, 0);
        Panel.SetZIndex(_editor, 2); Surface.Children.Add(_editor);
        // A moldura fica visível sobre a captura. A exportação compõe apenas a imagem e as anotações.
        Panel.SetZIndex(SelectionShadow, 3);
        Panel.SetZIndex(Selection, 4);
        _toolbar = CreateToolbar();
        Panel.SetZIndex(_toolbar, 6); Surface.Children.Add(_toolbar);
        CreateGrips();
        _editor.RegionDragStarted += BeginRegionMove;
        _editor.RegionDragged += MoveRegion;
        _editor.RegionDragCompleted += HideSize;
        ApplyRegion(region);
        Bright.Visibility = Visibility.Collapsed; Hint.Visibility = Visibility.Collapsed;
        HideSize();
        _editing = true;
        _editor.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (_editor is { IsEditingText: true })
        {
            if (e.Key == Key.Escape) { _editor.CancelText(); e.Handled = true; return; }
            if (modifiers == ModifierKeys.Control && e.Key == Key.Enter) { _editor.CommitText(); e.Handled = true; return; }
            // Sem texto selecionado, Ctrl+C continua copiando a captura; o resto pertence à caixa de texto.
            var copiesCapture = modifiers == ModifierKeys.Control && e.Key == Key.C && !_editor.HasSelectedText;
            var savesCapture = modifiers == ModifierKeys.Control && e.Key == Key.S;
            if (!copiesCapture && !savesCapture) return;
        }
        e.Handled = HandleShortcut(e.Key, modifiers);
    }

    /// <summary>Atalhos fora da digitação de texto. Retorna false para as teclas que não usa.</summary>
    internal bool HandleShortcut(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Escape) { Close(); return true; }
        if (!_editing || _editor == null)
        {
            if (modifiers != ModifierKeys.None || key != Key.R) return false;
            RepeatLastRegion();
            return true;
        }
        switch (key, modifiers)
        {
            case (Key.C, ModifierKeys.Control): case (Key.Enter, ModifierKeys.None): CopyCapture(); return true;
            case (Key.S, ModifierKeys.Control): SaveCapture(); return true;
            case (Key.Z, ModifierKeys.Control): _editor.Undo(); return true;
            case (Key.Y, ModifierKeys.Control): case (Key.Z, ModifierKeys.Control | ModifierKeys.Shift): _editor.Redo(); return true;
            default: return false;
        }
    }

    private BitmapSource CreateFinalImage()
    {
        if (_editor == null) throw new InvalidOperationException("Não há captura ativa.");
        return _editor.RenderImage();
    }

    private void CopyCapture()
    {
        try
        {
            ClipboardService.SetPng(CreateFinalImage(), AppSettings.Current.PrivateClipboard);
            DebugLog.Write("Captura copiada.");
            RememberRegion();
            Close();
        }
        catch (Exception exception)
        {
            DebugLog.Write("Falha ao copiar composição.", exception);
            MessageBox.Show($"Não foi possível copiar a captura.\n\n{exception.Message}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            SafeFile.Write(dialog.FileName, encoder.Save);
            DebugLog.Write("Captura salva.");
            RememberRegion();
            Close();
        }
        catch (Exception exception)
        {
            // A mensagem de erros de arquivo costuma trazer o caminho; o log guarda só o tipo e o código.
            DebugLog.Write($"Falha ao salvar captura: {exception.GetType().Name} 0x{exception.HResult:X8}.");
            MessageBox.Show($"Não foi possível salvar a captura.\n\n{exception.Message}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
