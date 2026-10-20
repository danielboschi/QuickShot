using System.Drawing;
using System.Windows.Forms;

namespace QuickShot;

/// <summary>Pequena dica visual que destaca o texto exato a procurar nas Configurações.</summary>
internal sealed class PrintScreenGuideForm : Form
{
    private readonly Panel _settingCard;
    private readonly System.Windows.Forms.Timer _pulseTimer;
    private bool _highlighted;
    public event Action? RestartRequested;

    public PrintScreenGuideForm()
    {
        Text = "QuickShot — tecla Print Screen";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        TopMost = true;
        ClientSize = new Size(430, 205);
        var workArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 800);
        Location = new Point(workArea.Right - Width - 18, workArea.Bottom - Height - 18);

        var instructions = new Label
        {
            Text = "Nas Configurações do Windows, procure esta opção.\nDesative-a para usar Print Screen no QuickShot.\nDepois, reinicie o QuickShot para aplicar a mudança.",
            AutoSize = false,
            Location = new Point(14, 12),
            Size = new Size(400, 54)
        };
        Controls.Add(instructions);

        _settingCard = new Panel
        {
            Location = new Point(14, 72),
            Size = new Size(400, 54),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(255, 244, 204)
        };
        var settingName = new Label
        {
            Text = "Usar a tecla Print Screen para abrir a captura de tela",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(8, 4),
            Size = new Size(380, 44)
        };
        _settingCard.Controls.Add(settingName);
        Controls.Add(_settingCard);

        var restart = new Button { Text = "Reiniciar QuickShot", Location = new Point(200, 158), Size = new Size(145, 30) };
        restart.Click += (_, _) => RestartRequested?.Invoke();
        Controls.Add(restart);

        var close = new Button { Text = "Entendi", Location = new Point(352, 158), Size = new Size(62, 30) };
        close.Click += (_, _) => Close();
        Controls.Add(close);

        _pulseTimer = new System.Windows.Forms.Timer { Interval = 450 };
        _pulseTimer.Tick += (_, _) =>
        {
            _highlighted = !_highlighted;
            _settingCard.BackColor = _highlighted ? Color.FromArgb(255, 215, 110) : Color.FromArgb(255, 244, 204);
        };
        _pulseTimer.Start();
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) _pulseTimer?.Dispose();
        base.Dispose(disposing);
    }
}
