using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

namespace QuickShot;

internal sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly Icon _trayIcon;
    private readonly ContextMenuStrip _menu;
    private PrintScreenGuideForm? _guide;
    private bool _balloonOpensSettings;
    // O menu leva um instante para sumir da tela e não deve aparecer na captura.
    private readonly Timer _captureDelay = new() { Interval = 300 };
    private Action? _delayedCapture;
    public event Action? CaptureRequested, RepeatRequested, ExitRequested;
    public TrayService()
    {
        var menu = _menu = new ContextMenuStrip();
        menu.Items.Add("Capturar tela", null, (_, _) => CaptureAfterMenu(() => CaptureRequested?.Invoke()));
        var repeat = new ToolStripMenuItem("Repetir última região", null, (_, _) => CaptureAfterMenu(() => RepeatRequested?.Invoke()));
        menu.Items.Add(repeat);
        menu.Items.Add(new ToolStripSeparator());
        var keepPrivate = new ToolStripMenuItem("Modo privado") { CheckOnClick = true, ToolTipText = "As capturas copiadas não entram no histórico nem na nuvem da área de transferência do Windows." };
        keepPrivate.Click += (_, _) => { AppSettings.Current.PrivateClipboard = keepPrivate.Checked; AppSettings.Current.Save(); };
        menu.Items.Add(keepPrivate);
        menu.Opening += (_, _) => { repeat.Enabled = AppSettings.Current.LastRegion != null; keepPrivate.Checked = AppSettings.Current.PrivateClipboard; };
        _captureDelay.Tick += (_, _) => { _captureDelay.Stop(); _delayedCapture?.Invoke(); };
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Configurar Print Screen no Windows…", null, OpenPrintScreenSettings);
        menu.Items.Add("Sair", null, (_, _) => ExitRequested?.Invoke());
        _trayIcon = AppIcon.CreateIcon();
        _icon = new NotifyIcon { Icon = _trayIcon, Text = "QuickShot", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => CaptureRequested?.Invoke();
        _icon.BalloonTipClicked += (sender, e) => { if (_balloonOpensSettings) OpenPrintScreenSettings(sender, e); };
    }
    private void CaptureAfterMenu(Action capture) { _delayedCapture = capture; _captureDelay.Start(); }
    public void NotifyShortcutUnavailable()
    {
        _balloonOpensSettings = true;
        // O texto do ícone permanece mesmo se o Windows suprimir a notificação.
        _icon.Text = "QuickShot — Print Screen indisponível";
        _icon.ShowBalloonTip(10000, "Print Screen indisponível", "Outro programa ou o Windows está usando esta tecla. Clique aqui para configurar. Você ainda pode capturar com dois cliques no ícone do QuickShot.", ToolTipIcon.Warning);
    }
    public void NotifyCaptureFailed(string details)
    {
        _balloonOpensSettings = false;
        _icon.ShowBalloonTip(5000, "Não foi possível capturar a tela", details, ToolTipIcon.Error);
    }
    private void OpenPrintScreenSettings(object? sender, EventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo("ms-settings:easeofaccess-keyboard") { UseShellExecute = true });
            // O guia pisca o rótulo a procurar, sem alterar a configuração do Windows.
            ShowGuide();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Não foi possível abrir as configurações de teclado do Windows.\n\n{exception.Message}", "QuickShot", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
    private void ShowGuide()
    {
        if (_guide == null || _guide.IsDisposed)
        {
            _guide = new PrintScreenGuideForm();
            _guide.RestartRequested += RestartQuickShot;
        }
        _guide.Show();
        _guide.Activate();
    }
    private void RestartQuickShot()
    {
        try
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable)) throw new InvalidOperationException("Não foi possível localizar o executável do QuickShot.");
            // Libera Print Screen antes de abrir a nova instância para ela registrar o atalho sem conflito.
            CaptureController.Dispose();
            try { Process.Start(new ProcessStartInfo(executable, App.RestartArgument) { UseShellExecute = true }); }
            catch
            {
                // Sem a nova instância, esta precisa recuperar o atalho.
                CaptureController.Initialize();
                throw;
            }
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show($"Não foi possível reiniciar o QuickShot.\n\n{exception.Message}", "QuickShot", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
    public void Dispose() { _captureDelay.Dispose(); _guide?.Dispose(); _icon.Visible = false; _icon.Dispose(); _menu.Dispose(); _trayIcon.Dispose(); }
}
