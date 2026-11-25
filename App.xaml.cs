using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace QuickShot;

public partial class App : Application
{
    internal const string RestartArgument = "--restart";
    private const string InstanceMutexName = @"Local\QuickShot-SingleInstance-C0A3E8A1";
    private Mutex? _instanceMutex;
    private TrayService? _tray;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        if (!AcquireInstance(isRestart: e.Args.Contains(RestartArgument)))
        {
            DebugLog.Write("Inicialização cancelada: já existe uma instância em execução.");
            MessageBox.Show("O QuickShot já está em execução. Procure o ícone na bandeja do Windows.", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        DebugLog.Write("QuickShot iniciado.");
        _tray = new TrayService();
        _tray.CaptureRequested += CaptureController.Start;
        _tray.RepeatRequested += CaptureController.StartWithLastRegion;
        _tray.ExitRequested += Shutdown;
        CaptureController.CaptureFailed += _tray.NotifyCaptureFailed;
        if (!CaptureController.Initialize()) _tray.NotifyShortcutUnavailable();
        CaptureController.WarmUpWhenIdle();
    }

    private bool AcquireInstance(bool isRestart)
    {
        try
        {
            var mutex = new Mutex(false, InstanceMutexName);
            bool acquired;
            // Ao reiniciar, a instância anterior pode ainda estar encerrando.
            try { acquired = mutex.WaitOne(isRestart ? TimeSpan.FromSeconds(10) : TimeSpan.Zero); }
            catch (AbandonedMutexException) { acquired = true; }
            if (acquired) _instanceMutex = mutex; else mutex.Dispose();
            return acquired;
        }
        catch (UnauthorizedAccessException)
        {
            // Há uma instância com privilégios diferentes na mesma sessão.
            return false;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        DebugLog.Write("Exceção não tratada na interface; processo mantido ativo.", e.Exception);
        e.Handled = true;
        MessageBox.Show($"O QuickShot encontrou um erro, mas continuará na bandeja.\n\n{e.Exception.Message}\n\nDetalhes: {DebugLog.Path}", "QuickShot", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_instanceMutex != null)
        {
            DebugLog.Write("QuickShot encerrado.");
            CaptureController.Dispose();
            _tray?.Dispose();
            _instanceMutex.ReleaseMutex();
            _instanceMutex.Dispose();
            _instanceMutex = null;
        }
        base.OnExit(e);
    }
}
