using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;

namespace QuickShot;

internal sealed class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;
    private PrintScreenGuideForm? _guide;
    public event Action? CaptureRequested, ExitRequested;
    public TrayService()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Capturar tela", null, (_, _) => CaptureRequested?.Invoke());
        menu.Items.Add("Configurar Print Screen no Windows…", null, OpenPrintScreenSettings);
        menu.Items.Add("Sair", null, (_, _) => ExitRequested?.Invoke());
        _icon = new NotifyIcon { Icon = AppIcon.CreateIcon(), Text = "QuickShot", ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => CaptureRequested?.Invoke();
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
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show($"Não foi possível reiniciar o QuickShot.\n\n{exception.Message}", "QuickShot", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
    public void Dispose() { _guide?.Close(); _icon.Visible = false; _icon.Dispose(); }
}
