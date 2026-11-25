using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using static QuickShot.Tests.Program;
using Edges = QuickShot.SelectionOverlay.ResizeEdges;
using Forms = System.Windows.Forms;

namespace QuickShot.Tests;

/// <summary>
/// Teste na tela real, com os monitores e as escalas do computador. Exibe o seletor por alguns segundos
/// sobre um fundo sintético e move o mouse. Não lê o conteúdo da área de trabalho: só captura a própria janela.
/// </summary>
internal static class ScreenTest
{
    private static System.Drawing.Rectangle _bounds;
    private static BitmapSource _pattern = null!;

    public static void Run(string? imageFolder)
    {
        _bounds = Forms.SystemInformation.VirtualScreen;
        _pattern = Gradient(_bounds.Width, _bounds.Height);
        var screens = Forms.Screen.AllScreens.OrderBy(screen => screen.Bounds.X).ToArray();
        foreach (var screen in screens) Console.WriteLine($"{screen.DeviceName} {screen.Bounds} escala {Scale(screen):0%}");
        var cursor = Forms.Cursor.Position;
        try
        {
            Console.WriteLine("\nSeletor");
            ShowDimmed(screens);
            foreach (var screen in screens) ClickWindow(screen);
            Console.WriteLine("\nEscalas simuladas, na tela");
            Dpi(visible: true);
            foreach (var screen in screens) Select($"dentro de {screen.DeviceName}", Inner(screen.Bounds, .25, .3, .7, .75), imageFolder);
            foreach (var screen in screens) Select($"canto inferior direito de {screen.DeviceName}", Inner(screen.Bounds, .85, .85, .995, .995), imageFolder);
            if (screens.Length > 1)
            {
                var (left, right) = (screens[0].Bounds, screens[1].Bounds);
                var top = Math.Max(left.Top, right.Top) + 150; var bottom = Math.Min(left.Bottom, right.Bottom) - 150;
                Select("entre monitores, maior parte no primeiro", new System.Drawing.Rectangle(left.Right - 600, top, 900, bottom - top), imageFolder);
                Select("entre monitores, maior parte no segundo", new System.Drawing.Rectangle(left.Right - 300, top, 1000, bottom - top), imageFolder);
            }
        }
        finally { SetCursorPos(cursor.X, cursor.Y); }
    }

    /// <summary>Memória e fluidez com o seletor realmente exibido.</summary>
    public static void MeasureMemory(bool software, int captures = 10)
    {
        void Report(string name) { using var process = Process.GetCurrentProcess(); Console.WriteLine($"{name}: privada={process.PrivateMemorySize64 >> 20} MiB, física={process.WorkingSet64 >> 20} MiB, identificadores={process.HandleCount}"); }
        if (software) RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        _bounds = Forms.SystemInformation.VirtualScreen;
        Report("antes"); SelectionOverlay.WarmUp(); Pump(); Report("preparado");
        for (var count = 1; count <= captures; count++)
        {
            _pattern = Gradient(_bounds.Width, _bounds.Height);
            var watch = Stopwatch.StartNew(); var window = Open(); var opened = watch.ElapsedMilliseconds;
            if (count == 1) Report("janela exibida");
            var start = new Point(100, 250); window.GetType().GetField("_start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(window, start);
            var move = window.GetType().GetMethod("DrawSelection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            using var process = Process.GetCurrentProcess(); var cpu = process.TotalProcessorTime; var total = Stopwatch.StartNew(); var delays = new System.Collections.Generic.List<double>();
            // Alterna entre uma seleção pequena e uma grande; o pixel só clareia quando o quadro chega à tela.
            for (var step = 0; step < (count is 1 or 5 ? 30 : 0); step++)
            {
                var large = step % 2 == 0; var probe = new Point(1500, 850); var watchFrame = Stopwatch.StartNew();
                move.Invoke(window, new object[] { large ? new Point(1700 + step, 1000) : new Point(400 + step, 500) });
                var index = ((int)probe.Y * _bounds.Width + (int)probe.X) * 4; var pattern = Pixels(_pattern);
                while (watchFrame.ElapsedMilliseconds < 1000)
                {
                    Pump(DispatcherPriority.Background);
                    var pixel = Pixels(ScreenCapture.Capture(new Rect(_bounds.X + probe.X, _bounds.Y + probe.Y, 1, 1)));
                    if (pixel[0] == pattern[index] == large) break;
                }
                delays.Add(watchFrame.Elapsed.TotalMilliseconds);
            }
            process.Refresh(); delays.Sort(); if (count % 10 == 0) MemoryCleaner.Clean();
            if (count is 1 or 5) Console.WriteLine($"  abertura={opened} ms; seleção na tela: mediana={delays[15]:0} ms, pior={delays[^1]:0} ms, CPU={(process.TotalProcessorTime - cpu).TotalMilliseconds / total.ElapsedMilliseconds:0%}");
            window.GetType().GetMethod("StartEditing", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(window, new object[] { new Rect(100, 250, 1400, 700) }); Settle();
            ClipboardService.CreatePngData(((DrawingSurface)Field(window, "_editor")!).RenderImage(), keepPrivate: false);
            window.Close(); _pattern = null!; Settle();
            if (count is 1 or 2 or 5 || count % 10 == 0) Report($"{count} capturas");
        }
        System.Threading.Thread.Sleep(3000); Pump(); Report("após 3 s");
        var cleaning = Stopwatch.StartNew(); MemoryCleaner.Clean(); Console.WriteLine($"  limpeza em {cleaning.ElapsedMilliseconds} ms"); System.Threading.Thread.Sleep(500); Pump(); Report("após limpeza");
    }

    private static void ShowDimmed(Forms.Screen[] screens)
    {
        SetCursorPos(screens[^1].Bounds.Left + screens[^1].Bounds.Width / 2, screens[^1].Bounds.Top + screens[^1].Bounds.Height / 2);
        var window = Open(); var handle = new WindowInteropHelper(window).Handle;
        NativeMethods.GetWindowRect(handle, out var rect);
        Check("janela cobre todos os monitores, em pixels", rect.Left == _bounds.Left && rect.Top == _bounds.Top && rect.Right == _bounds.Right && rect.Bottom == _bounds.Bottom, $"{rect.Left},{rect.Top},{rect.Right},{rect.Bottom}");
        Console.WriteLine($"  DPI usado pelo WPF: {VisualTreeHelper.GetDpi(window).DpiScaleX:0%}");
        var shown = Pixels(ScreenCapture.Capture(ToRect(_bounds))); var expected = Pixels(_pattern);
        foreach (var screen in screens)
        {
            var worst = 0; var wrong = 0;
            // Sob o cursor, o monitor aparece claro; nos outros, escurecido na proporção da camada #88000000.
            // A dica e as medidas ficam no alto, e a moldura, na borda.
            var bright = ReferenceEquals(screen, screens[^1]); var factor = bright ? 1 : (255 - 0x88) / 255d;
            var area = screen.Bounds; area.Inflate(-10, -10); area.Y += 120; area.Height -= 120;
            for (var y = area.Top; y < area.Bottom; y++) for (var x = area.Left; x < area.Right; x++)
            {
                var index = ((y - _bounds.Top) * _bounds.Width + x - _bounds.Left) * 4; var bad = false;
                for (var channel = 0; channel < 3; channel++)
                {
                    var difference = Math.Abs(shown[index + channel] - expected[index + channel] * factor);
                    worst = Math.Max(worst, (int)difference); bad |= difference > 2;
                }
                if (bad) wrong++;
            }
            Check($"tela {(bright ? "realçada" : "escurecida")} coincide pixel a pixel em {screen.DeviceName}", wrong == 0, $"{wrong} pixels, diferença máxima {worst}");
        }
        var hint = (FrameworkElement)window.FindName("Hint"); var at = hint.PointToScreen(new Point(0, 0));
        Check("dica no monitor do cursor", screens[^1].Bounds.Contains((int)at.X, (int)at.Y), $"{at}");
        window.Close(); Pump();
    }

    private static void Select(string name, System.Drawing.Rectangle target, string? imageFolder)
    {
        Console.WriteLine($"\nSeleção {name}: {target}");
        var window = Open(); var handle = new WindowInteropHelper(window).Handle;
        try
        {
            if (!Drag(handle, target.Left, target.Top, target.Right, target.Bottom)) { Check("o seletor recebe o mouse", false); return; }
            Settle();
            if (Field(window, "_editor") is not DrawingSurface surface) { Check("o editor abre", false); return; }
            var region = new Rect(target.X - _bounds.X, target.Y - _bounds.Y, target.Width, target.Height);
            Check("região igual ao arraste do mouse", surface.Region == region, surface.Region.ToString());
            Check("recorte exportado idêntico", MatchesSource(surface, _pattern));

            var monitor = Forms.Screen.FromRectangle(target); var toolbar = (Border)Field(window, "_toolbar")!;
            var corner = toolbar.PointToScreen(new Point(0, 0)); var end = toolbar.PointToScreen(new Point(toolbar.ActualWidth, toolbar.ActualHeight));
            Check("barra inteira no monitor da seleção", monitor.Bounds.Contains((int)corner.X, (int)corner.Y) && monitor.Bounds.Contains((int)end.X - 1, (int)end.Y - 1), $"{corner} a {end}");
            Check("barra no tamanho da escala", Math.Abs(end.Y - corner.Y - 46 * Scale(monitor)) <= 2 * Scale(monitor), $"{end.Y - corner.Y:0.#} px");

            // Interior da seleção, longe da moldura, das alças e da barra.
            var inner = region; inner.Inflate(-24, -24);
            if (inner.IsEmpty) inner = Rect.Empty;
            var bar = new Rect(corner.X - _bounds.X, corner.Y - _bounds.Y, end.X - corner.X, end.Y - corner.Y);
            var shot = ScreenCapture.Capture(ToRect(_bounds)); var shown = Pixels(shot); var expected = Pixels(_pattern); var wrong = 0;
            if (!inner.IsEmpty) for (var y = (int)inner.Top; y < inner.Bottom; y++) for (var x = (int)inner.Left; x < inner.Right; x++)
            {
                if (bar.Contains(x, y) || !OnScreen(x, y)) continue;
                var index = (y * _bounds.Width + x) * 4;
                if (shown[index] != expected[index] || shown[index + 1] != expected[index + 1] || shown[index + 2] != expected[index + 2]) wrong++;
            }
            Check("seleção exibida idêntica à captura", wrong == 0, $"{wrong} pixels");
            if (imageFolder != null) Save(shot, region, bar, Path.Combine(imageFolder, $"tela-{string.Concat(name.Where(char.IsLetterOrDigit))}.png"));

            // Borda esquerda, pelo mouse real.
            var y0 = target.Top + target.Height * 2 / 3;
            if (Drag(handle, target.Left, y0, target.Left - 40, y0)) { Settle(); region = new Rect(region.X - 40, region.Y, region.Width + 40, region.Height); Check("ajuste da borda com o mouse", surface.Region == region, surface.Region.ToString()); }
            else Check("a alça recebe o mouse", false);

            var moved = surface.Region;
            // Uma linha, escolhida na barra e desenhada com o mouse.
            var tool = Find<RadioButton>(toolbar).First(button => button.GroupName == "Tool" && (string)button.ToolTip == "Linha");
            var button = tool.PointToScreen(new Point(tool.ActualWidth / 2, tool.ActualHeight / 2));
            Check("botão da barra recebe o clique", Click(handle, (int)button.X, (int)button.Y) && surface.Tool == EditorTool.Line);
            var from = new Point(target.Left + target.Width / 2 - 30, target.Top + target.Height / 2); var to = new Point(from.X + 60, from.Y + 20);
            if (Drag(handle, (int)from.X, (int)from.Y, (int)to.X, (int)to.Y))
            {
                var line = surface.Children.OfType<System.Windows.Shapes.Line>().SingleOrDefault();
                Check("linha sob o cursor, do início ao fim", line != null && (new Vector(line.X1 + _bounds.X, line.Y1 + _bounds.Y) - (Vector)from).Length < .01 && (new Vector(line.X2 + _bounds.X, line.Y2 + _bounds.Y) - (Vector)to).Length < .01, line == null ? "ausente" : $"{line.X1},{line.Y1} a {line.X2},{line.Y2}");
                Check("traço no tamanho da escala", line != null && Math.Abs(line.StrokeThickness - 3 * Scale(monitor)) < .01);
            }
            else Check("o editor recebe o mouse", false);

            // Com Shift, a mesma linha fica horizontal.
            surface.Undo(); Pump();
            keybd_event(0x10, 0, 0, 0); try { Drag(handle, (int)from.X, (int)from.Y, (int)to.X, (int)from.Y + 12); } finally { keybd_event(0x10, 0, 2, 0); Step(); }
            var straight = surface.Children.OfType<System.Windows.Shapes.Line>().SingleOrDefault();
            Check("Shift deixa a linha reta", straight != null && Math.Abs(straight.Y1 - straight.Y2) < .01 && Math.Abs(straight.X2 - straight.X1 - 60) < 1.5, straight == null ? "ausente" : $"{straight.X1},{straight.Y1} a {straight.X2},{straight.Y2}");

            // Mover a anotação e, fora dela, a seleção inteira.
            ClickTool(handle, toolbar, "Mover anotação ou seleção");
            Drag(handle, (int)from.X + 30, (int)from.Y, (int)from.X + 40, (int)from.Y - 20);
            Check("anotação movida com o mouse", straight != null && Math.Abs(Canvas.GetLeft(straight) - 10) < .01 && Math.Abs(Canvas.GetTop(straight) + 20) < .01);
            var grab = new Point(target.Left + target.Width / 4, target.Top + target.Height / 4);
            Drag(handle, (int)grab.X, (int)grab.Y, (int)grab.X - 25, (int)grab.Y - 15);
            Check("seleção movida com o mouse", surface.Region == new Rect(moved.X - 25, moved.Y - 15, moved.Width, moved.Height), surface.Region.ToString());

            // Tarja sobre a imagem, conferida na tela e na exportação.
            ClickTool(handle, toolbar, "Tarja para ocultar");
            var cover = new System.Drawing.Rectangle(target.Left + target.Width / 5, target.Top + target.Height / 2 + 10, 50, 24);
            Drag(handle, cover.Left, cover.Top, cover.Right, cover.Bottom); Settle();
            var image = Pixels(surface.RenderImage()); var screenPixels = Pixels(ScreenCapture.Capture(ToRect(cover)));
            var hidden = true; for (var y = 0; y < cover.Height; y++) for (var x = 0; x < cover.Width; x++)
            {
                var index = ((cover.Y - _bounds.Y - (int)surface.Region.Y + y) * (int)surface.Region.Width + cover.X - _bounds.X - (int)surface.Region.X + x) * 4;
                for (var channel = 0; channel < 3; channel++) hidden &= image[index + channel] == 0 && screenPixels[(y * cover.Width + x) * 4 + channel] == 0;
            }
            Check("tarja preta na tela e na imagem final", hidden);
            if (imageFolder != null) Save(ScreenCapture.Capture(ToRect(_bounds)), surface.Region, new Rect(Canvas.GetLeft(toolbar), Canvas.GetTop(toolbar), toolbar.DesiredSize.Width, toolbar.DesiredSize.Height), Path.Combine(imageFolder, $"editado-{string.Concat(name.Where(char.IsLetterOrDigit))}.png"));
        }
        finally { window.Close(); Pump(); }
    }

    private static void ClickWindow(Forms.Screen screen)
    {
        Console.WriteLine($"\nClique em janela, em {screen.DeviceName}");
        var front = Inner(screen.Bounds, .3, .35, .6, .7); var back = Inner(screen.Bounds, .2, .3, .8, .8);
        var window = Open(new[] { front, back }); var handle = new WindowInteropHelper(window).Handle;
        try
        {
            var expected = new Rect(back.X - _bounds.X, back.Y - _bounds.Y, back.Width, back.Height);
            MoveTo(back.Left + 12, back.Top + 12); Settle();
            Check("realce da janela sob o cursor", ((UIElement)window.FindName("Bright")).Clip.Bounds == expected, ((UIElement)window.FindName("Bright")).Clip?.Bounds.ToString() ?? "");
            Click(handle, front.Left + 20, front.Top + 20); Settle();
            Check("clique escolhe a janela da frente", Field(window, "_editor") is DrawingSurface surface && surface.Region == new Rect(front.X - _bounds.X, front.Y - _bounds.Y, front.Width, front.Height));
        }
        finally { window.Close(); Pump(); }
        window = Open(new[] { front }); handle = new WindowInteropHelper(window).Handle;
        try
        {
            Click(handle, screen.Bounds.Right - 30, screen.Bounds.Bottom - 30); Settle();
            Check("fora das janelas, o monitor inteiro", Field(window, "_editor") is DrawingSurface surface && surface.Region == new Rect(screen.Bounds.X - _bounds.X, screen.Bounds.Y - _bounds.Y, screen.Bounds.Width, screen.Bounds.Height));
        }
        finally { window.Close(); Pump(); }
    }
    private static void ClickTool(nint handle, Border toolbar, string name)
    {
        var tool = Find<RadioButton>(toolbar).First(button => button.GroupName == "Tool" && (string)button.ToolTip == name);
        var at = tool.PointToScreen(new Point(tool.ActualWidth / 2, tool.ActualHeight / 2)); Click(handle, (int)at.X, (int)at.Y);
    }

    private static SelectionOverlay Open(System.Collections.Generic.IReadOnlyList<System.Drawing.Rectangle>? windows = null)
    {
        var window = new SelectionOverlay(_pattern, _bounds, windows); var rendered = false;
        window.ContentRendered += (_, _) => rendered = true;
        window.Show(); window.Activate();
        var watch = Stopwatch.StartNew();
        while (!rendered && watch.ElapsedMilliseconds < 3000) Pump(DispatcherPriority.Background);
        Settle(); return window;
    }
    private static void Settle() { for (var count = 0; count < 4; count++) { System.Threading.Thread.Sleep(80); Pump(); } }
    private static double Scale(Forms.Screen screen) => NativeMethods.GetMonitorScale(screen.Bounds.X + screen.Bounds.Width / 2, screen.Bounds.Y + screen.Bounds.Height / 2);
    private static Rect ToRect(System.Drawing.Rectangle area) => new(area.X, area.Y, area.Width, area.Height);
    private static bool OnScreen(int x, int y) => Forms.Screen.AllScreens.Any(screen => screen.Bounds.Contains(x + _bounds.X, y + _bounds.Y));
    private static System.Drawing.Rectangle Inner(System.Drawing.Rectangle area, double left, double top, double right, double bottom) => System.Drawing.Rectangle.FromLTRB(
        area.X + (int)(area.Width * left), area.Y + (int)(area.Height * top), area.X + (int)(area.Width * right), area.Y + (int)(area.Height * bottom));
    private static void Save(BitmapSource shot, Rect region, Rect bar, string path)
    {
        var area = Rect.Union(region, bar); area.Inflate(30, 30); area.Intersect(new Rect(0, 0, shot.PixelWidth, shot.PixelHeight));
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(new CroppedBitmap(shot, new Int32Rect((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height))));
        using var file = File.Create(path); encoder.Save(file);
    }

    // --- Mouse: só age se a janela sob o cursor for o próprio seletor.

    private static bool Owns(nint handle, int x, int y) => WindowFromPoint(new POINT { X = x, Y = y }) == handle;
    private static bool Drag(nint handle, int x1, int y1, int x2, int y2)
    {
        if (!Owns(handle, x1, y1)) return false;
        MoveTo(x1, y1); mouse_event(0x0002, 0, 0, 0, 0); Step();
        for (var step = 1; step <= 5; step++) MoveTo(x1 + (x2 - x1) * step / 5, y1 + (y2 - y1) * step / 5);
        mouse_event(0x0004, 0, 0, 0, 0); Step(); return true;
    }
    private static bool Click(nint handle, int x, int y)
    {
        if (!Owns(handle, x, y)) return false;
        MoveTo(x, y); mouse_event(0x0002, 0, 0, 0, 0); Step(); mouse_event(0x0004, 0, 0, 0, 0); Step(); return true;
    }
    private static void MoveTo(int x, int y) { SetCursorPos(x, y); Step(); }
    private static void Step() { System.Threading.Thread.Sleep(25); Pump(); }

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, int x, int y, uint data, nint extra);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, nint extra);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(POINT point);
}
