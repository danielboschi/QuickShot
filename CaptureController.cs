using System.Windows;
using System.Windows.Threading;

namespace QuickShot;

internal static class CaptureController
{
    private static HotKeyService? _hotKey;
    private static SelectionOverlay? _overlay;
    /// <summary>Substituível nos testes, para simular falhas da captura.</summary>
    internal static Func<Rect, System.Windows.Media.Imaging.BitmapSource> CaptureScreen { get; set; } = ScreenCapture.Capture;
    public static event Action<string>? CaptureFailed;

    /// <summary>Registra o atalho global. Retorna false quando a tecla já pertence a outro programa.</summary>
    public static bool Initialize()
    {
        _hotKey = new HotKeyService();
        _hotKey.Pressed += Start;
        return _hotKey.Register(0, NativeMethods.VK_SNAPSHOT);
    }

    /// <summary>Carrega antecipadamente o que a primeira captura usaria, quando a interface estiver ociosa.</summary>
    public static void WarmUpWhenIdle() => Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
    {
        if (_overlay != null) return;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            SelectionOverlay.WarmUp();
            DebugLog.Write($"Preparação da primeira captura concluída em {watch.ElapsedMilliseconds} ms.");
        }
        catch (Exception exception) { DebugLog.Write("Falha ao preparar a primeira captura; o aplicativo continua normalmente.", exception); }
        MemoryCleaner.Schedule();
    });

    public static void Start() => Start(repeatLastRegion: false);
    public static void StartWithLastRegion() => Start(repeatLastRegion: true);
    private static void Start(bool repeatLastRegion)
    {
        DebugLog.Write("Início de captura solicitado.");
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_overlay != null) { DebugLog.Write("Captura ignorada: seletor já aberto."); return; }
            SelectionOverlay? overlay = null;
            MemoryCleaner.Cancel();
            try
            {
                var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
                // Preserve inclusive os tooltips antes de criar ou ativar a janela de seleção.
                // A captura fica fora do construtor: uma Window que falha ao ser construída
                // permanece em Application.Windows.
                var snapshot = CaptureScreen(new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
                overlay = new SelectionOverlay(snapshot, bounds, WindowFinder.VisibleWindows());
                DebugLog.Write("Snapshot do desktop criado antes da seleção.");
                var opened = overlay;
                opened.Closed += (_, _) =>
                {
                    if (!ReferenceEquals(_overlay, opened)) return;
                    _overlay = null;
                    MemoryCleaner.Schedule();
                };
                _overlay = opened;
                opened.Show();
                if (repeatLastRegion) opened.RepeatLastRegion();
            }
            catch (Exception exception)
            {
                _overlay = null;
                DebugLog.Write("Falha ao iniciar a captura.", exception);
                try { overlay?.Close(); }
                catch (Exception closeException) { DebugLog.Write("Falha ao fechar o seletor após erro.", closeException); }
                CaptureFailed?.Invoke(exception.Message);
            }
        });
    }
    public static void Dispose()
    {
        _hotKey?.Dispose();
        _hotKey = null;
    }
}
