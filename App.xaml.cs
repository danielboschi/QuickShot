using System.Windows;
using System.Windows.Threading;

namespace QuickShot;

public partial class App : Application
{
    private TrayService? _tray;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DebugLog.Write("QuickShot iniciado.");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        _tray = new TrayService();
        _tray.CaptureRequested += CaptureController.Start;
        _tray.ExitRequested += Shutdown;
        CaptureController.Initialize();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DebugLog.Write("Exceção não tratada na interface; processo mantido ativo.", e.Exception);
        e.Handled = true;
        MessageBox.Show($"O QuickShot encontrou um erro, mas continuará na bandeja.\n\n{e.Exception.Message}\n\nDetalhes: {DebugLog.Path}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DebugLog.Write("QuickShot encerrado.");
        CaptureController.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
