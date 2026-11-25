using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DrawingRectangle = System.Drawing.Rectangle;
using Edges = QuickShot.SelectionOverlay.ResizeEdges;

namespace QuickShot.Tests;

/// <summary>
/// Verificações sem dependências externas. As janelas ficam fora da área visível, e nada é
/// copiado para a área de transferência. Uso: dotnet run --project tests [memory | startup | screen | windows]
/// </summary>
internal static class Program
{
    private const int Offscreen = -20000;
    private static int _failures, _passed;

    [STAThread]
    private static int Main(string[] args)
    {
        DebugLog.Path = Path.Combine(Path.GetTempPath(), $"quickshot-tests-{Environment.ProcessId}", "quickshot-debug.log");
        AppSettings.Path = Path.Combine(Path.GetDirectoryName(DebugLog.Path)!, "settings.ini");
        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        try
        {
            switch (args.FirstOrDefault())
            {
                case "memory": if (args.Contains("screen")) ScreenTest.MeasureMemory(args.Contains("software"), int.TryParse(args[^1], out var captures) ? captures : 10); else MeasureMemory(); return 0;
                case "startup": MeasureStartup(warm: args.Contains("warm")); return 0;
                case "windows": foreach (var area in WindowFinder.VisibleWindows()) Console.WriteLine(area); return 0;
                case "screen": ScreenTest.Run(args.ElementAtOrDefault(1)); Console.WriteLine($"\n{_passed} aprovadas, {_failures} reprovadas."); return _failures;
            }
            foreach (var (name, test) in new (string, Action)[]
            {
                ("Preparação e falhas", WarmUpAndFailures), ("Texto", Text), ("Seleção", Resizing), ("Região e anotações", RegionAndAnnotations),
                ("Formas", Shapes), ("Tarja", Redaction), ("Shift", Constraints), ("Mover", Moving), ("Atalhos", Shortcuts), ("Janelas e repetição", Picking), ("Preferências", Settings), ("Modo privado", PrivateClipboard), ("Barra", Toolbar), ("DPI", () => Dpi(visible: false)), ("Arquivos e log", Files), ("Ícone", Icon)
            })
            {
                Console.WriteLine($"\n{name}");
                try { test(); }
                catch (Exception exception) { Check($"{name}: {exception}", false); }
            }
            Console.WriteLine($"\n{_passed} aprovadas, {_failures} reprovadas.");
            return _failures;
        }
        finally { try { Directory.Delete(Path.GetDirectoryName(DebugLog.Path)!, true); } catch { } }
    }

    private static void WarmUpAndFailures()
    {
        SelectionOverlay.WarmUp();
        Check("preparação não deixa janela aberta", !Application.Current.Windows.OfType<SelectionOverlay>().Any());
        Check("preparação libera MainWindow", Application.Current.MainWindow == null);

        var messages = new List<string>();
        CaptureController.CaptureFailed += messages.Add;
        CaptureController.CaptureScreen = _ => throw new InvalidOperationException("simulada");
        CaptureController.Start(); CaptureController.Start();
        CaptureController.CaptureScreen = ScreenCapture.Capture;
        Check("falha de captura avisa e não bloqueia a próxima", messages.SequenceEqual(new[] { "simulada", "simulada" }));
    }

    private static void Text()
    {
        var window = Open(Solid(400, 200)); var surface = Edit(window, new Rect(0, 0, 400, 200));
        Type(surface, new Point(50, 60), "Olá mundo");
        var offset = Editor(surface).GetRectFromCharacterIndex(0);
        Check("em digitação", surface.IsEditingText);
        surface.CommitText(); Pump();
        var block = surface.Children.OfType<TextBlock>().Single();
        Check("confirmar troca a caixa pelo texto", !surface.Children.OfType<TextBox>().Any());
        Check("texto não se desloca ao confirmar", Math.Abs(Canvas.GetLeft(block) - 50 - offset.X) < .01 && Math.Abs(Canvas.GetTop(block) - 60 - offset.Y) < .01);
        var image = surface.RenderImage();
        Check("texto exportado", Pixels(image).Where((value, index) => index % 4 == 0 && value < 200).Count() > 50);
        Check("sem moldura", Pixel(image, 110, 60) == Colors.White);
        surface.Undo(); Check("desfazer", !surface.Children.OfType<TextBlock>().Any());
        surface.Redo(); Pump(); Check("refazer", surface.Children.OfType<TextBlock>().Count() == 1);

        Type(surface, new Point(60, 70), "Outro");
        Check("clicar no texto reedita", block.Visibility == Visibility.Hidden);
        surface.CommitText(); Pump();
        Check("reedição no mesmo elemento", block.Text == "Outro" && block.Visibility == Visibility.Visible);
        surface.Undo(); Check("desfazer reedição", block.Text == "Olá mundo");
        surface.Redo(); Check("refazer reedição", block.Text == "Outro");
        Type(surface, new Point(60, 70), "x"); surface.CancelText(); Pump();
        Check("cancelar mantém o original", block.Text == "Outro" && block.Visibility == Visibility.Visible && !surface.IsEditingText);
        Type(surface, new Point(60, 70), ""); surface.CommitText(); Pump();
        Check("esvaziar apaga", !surface.Children.OfType<TextBlock>().Any());
        surface.Undo(); Pump(); Check("desfazer apagamento", surface.Children.OfType<TextBlock>().Single().Text == "Outro");
        Type(surface, new Point(200, 150), ""); surface.CommitText();
        Check("texto vazio não cria anotação", surface.Children.OfType<TextBlock>().Count() == 1);
        Type(surface, new Point(200, 150), "pendente"); surface.RenderImage();
        Check("exportar inclui texto em digitação", surface.Children.OfType<TextBlock>().Count() == 2 && !surface.IsEditingText);
        surface.UpdateLayout();
        Check("borracha encontra o texto", ReferenceEquals(Call(surface, "FindAnnotation", new Point(60, 70)), block));
        Call(surface, "RemoveAnnotation", block); Check("borracha apaga", !surface.Children.Contains(block));
        surface.Undo(); Check("desfazer borracha", surface.Children.Contains(block));
        Check("fundo não é apagável", Call(surface, "FindAnnotation", new Point(390, 10)) == null);
        Type(surface, new Point(20, 20), "foco");
        Editor(surface).RaiseEvent(new RoutedEventArgs(UIElement.LostFocusEvent));
        Check("clicar na barra confirma", !surface.IsEditingText && surface.Children.OfType<TextBlock>().Any(item => item.Text == "foco"));
        Type(surface, new Point(20, 100), "a\r\nb\r\n"); surface.CommitText();
        Check("várias linhas", surface.Children.OfType<TextBlock>().Any(item => item.Text == "a\r\nb"));

        Type(surface, new Point(20, 150), "z"); Press(window, Key.Escape);
        Check("Esc na digitação não fecha", window.IsVisible && !surface.IsEditingText);
        Press(window, Key.Escape); Pump();
        Check("Esc fora da digitação fecha", !Application.Current.Windows.OfType<SelectionOverlay>().Any());
    }

    private static void Resizing()
    {
        var limits = new Size(800, 600); var origin = new Rect(200, 150, 300, 200);
        Rect Move(Edges edges, double x, double y, Rect? from = null) => SelectionOverlay.Resize(from ?? origin, edges, new Vector(x, y), limits);
        Check("esquerda", Move(Edges.Left, -50, 99) == new Rect(150, 150, 350, 200));
        Check("direita", Move(Edges.Right, 40, 99) == new Rect(200, 150, 340, 200));
        Check("topo", Move(Edges.Top, 99, 30) == new Rect(200, 180, 300, 170));
        Check("base", Move(Edges.Bottom, 99, -30) == new Rect(200, 150, 300, 170));
        Check("canto superior esquerdo", Move(Edges.Left | Edges.Top, -10, -20) == new Rect(190, 130, 310, 220));
        Check("canto inferior direito", Move(Edges.Right | Edges.Bottom, 10, 20) == new Rect(200, 150, 310, 220));
        Check("limite da imagem", Move(Edges.Left | Edges.Top, -999, -999) == new Rect(0, 0, 500, 350) && Move(Edges.Right | Edges.Bottom, 999, 999) == new Rect(200, 150, 600, 450));
        Check("não inverte", Move(Edges.Left, 999, 0) == new Rect(490, 150, 10, 200) && Move(Edges.Bottom, 0, -999) == new Rect(200, 150, 300, 10));
        Check("seleção minúscula", Move(Edges.Left | Edges.Top, 5, 5, new Rect(10, 10, 3, 3)) == new Rect(10, 10, 3, 3));
        Check("pixels inteiros", Move(Edges.Right, 10.4, 0).Width == 310);
    }

    private static void RegionAndAnnotations()
    {
        var source = Gradient(800, 600); var window = Open(source); var surface = Edit(window, new Rect(200, 150, 300, 200));
        Check("recorte inicial idêntico", MatchesSource(surface, source));
        Check("oito alças", Find<Thumb>(window).Count(thumb => thumb.Tag is Edges) == 8);
        foreach (var (name, region) in new[] { ("ampliado", new Rect(120, 100, 500, 400)), ("reduzido", new Rect(260, 200, 90, 70)), ("tela inteira", new Rect(0, 0, 800, 600)) })
        {
            Apply(window, region); Check($"recorte {name} idêntico", MatchesSource(surface, source));
        }
        Apply(window, new Rect(200, 150, 300, 200));
        var mark = new System.Windows.Shapes.Rectangle { Fill = Brushes.White, Width = 20, Height = 20 };
        Canvas.SetLeft(mark, 300); Canvas.SetTop(mark, 220); surface.Children.Add(mark); Call(surface, "RecordAdded", mark); Pump();
        bool White(int x, int y) => Pixel(surface.RenderImage(), x - (int)surface.Region.X, y - (int)surface.Region.Y) == Colors.White;
        Check("anotação no lugar", White(310, 230));
        Apply(window, new Rect(250, 180, 200, 150)); Check("ajustar esquerda e topo não a move", White(310, 230) && !White(299, 230) && !White(321, 230));
        Apply(window, new Rect(350, 180, 100, 150)); Check("fora da seleção não é exportada", MatchesSource(surface, source));
        Check("fora da seleção não recebe clique", surface.InputHitTest(new Point(310, 230)) == null && surface.InputHitTest(new Point(400, 200)) != null);
        surface.Undo(); Apply(window, new Rect(100, 100, 400, 300)); surface.Redo(); Pump(); Check("refazer após ajuste", White(310, 230));
        Type(surface, new Point(150, 150), "abc"); Apply(window, new Rect(120, 120, 300, 300));
        Check("ajustar não interrompe a digitação", surface.IsEditingText);
        surface.CommitText(); Check("texto mantém posição", Math.Abs(Canvas.GetLeft(surface.Children.OfType<TextBlock>().Single()) - 153) < .1);
        window.Close();
    }

    private static void Shapes()
    {
        var window = Open(Solid(300, 200)); var surface = Edit(window, new Rect(0, 0, 300, 200));
        foreach (var tool in new[] { EditorTool.Line, EditorTool.Arrow, EditorTool.Rectangle, EditorTool.Ellipse })
        {
            surface.Tool = tool;
            Check($"{tool}: clique sem arrastar é descartado", !Draw(surface, new Vector(1, 1)) && surface.Children.Count == 1);
            Check($"{tool}: arraste é mantido", Draw(surface, new Vector(40, 30)));
            surface.Undo(); Check($"{tool}: desfazer remove", surface.Children.Count == 1);
        }
        surface.Scale = 2; surface.Tool = EditorTool.Line; Draw(surface, new Vector(40, 30));
        Check("traço acompanha a escala", surface.Children.OfType<System.Windows.Shapes.Line>().Single().StrokeThickness == surface.DrawThickness * 2);
        Type(surface, new Point(100, 100), "x"); Check("texto acompanha a escala", Editor(surface).FontSize == Math.Max(14, surface.DrawThickness * 5) * 2); surface.CancelText();
        window.Close();
    }

    private static void Redaction()
    {
        var window = Open(Gradient(300, 200)); var surface = Edit(window, new Rect(20, 20, 200, 150)); surface.Tool = EditorTool.Redact; surface.DrawBrush = Brushes.Yellow;
        SetField(surface, "_start", new Point(60.4, 50.7)); var bar = (System.Windows.Shapes.Rectangle)Call(surface, "CreateShape")!;
        surface.Children.Add(bar); SetField(surface, "_preview", bar); Call(surface, "FinishShape", new Point(120.2, 90.3)); Pump();
        Check("ocupa pixels inteiros, para fora", Canvas.GetLeft(bar) == 60 && Canvas.GetTop(bar) == 50 && bar.Width == 61 && bar.Height == 41);
        var image = surface.RenderImage(); var pixels = Pixels(image); var black = true; var untouched = true; var expected = Pixels(Gradient(300, 200));
        for (var y = 0; y < image.PixelHeight; y++) for (var x = 0; x < image.PixelWidth; x++)
        {
            var index = (y * image.PixelWidth + x) * 4; var inside = x + 20 >= 60 && x + 20 < 121 && y + 20 >= 50 && y + 20 < 91;
            for (var channel = 0; channel < 3; channel++)
                if (inside) black &= pixels[index + channel] == 0; else untouched &= pixels[index + channel] == expected[((y + 20) * 300 + x + 20) * 4 + channel];
        }
        Check("preto opaco em todos os pixels, qualquer que seja a cor", black);
        Check("nada além dela é alterado", untouched);
        Check("nada do original resta na cópia", ClipboardService.CreatePngData(image, false).GetFormats(false).All(format => format is "PNG" or "System.Windows.Media.Imaging.BitmapSource" or "Bitmap" or "System.Drawing.Bitmap"), string.Join(", ", ClipboardService.CreatePngData(image, false).GetFormats(false)));
        surface.Undo(); Check("desfazer", surface.Children.Count == 1); surface.Redo(); Check("refazer", surface.Children.Count == 2);
        window.Close();
    }

    private static void Constraints()
    {
        var origin = new Point(100, 100);
        Check("horizontal", DrawingSurface.SnapAngle(origin, new Point(180, 109)) == new Point(180, 100));
        Check("vertical", DrawingSurface.SnapAngle(origin, new Point(95, 20)) == new Point(100, 20));
        var diagonal = DrawingSurface.SnapAngle(origin, new Point(160, 150)); Check("diagonal", Math.Abs(diagonal.X - diagonal.Y) < .001 && Math.Abs(diagonal.X - 155) < .001, diagonal.ToString());
        Check("quadrado", DrawingSurface.SnapSquare(origin, new Point(160, 130)) == new Point(160, 160) && DrawingSurface.SnapSquare(origin, new Point(60, 110)) == new Point(60, 140));
        var window = Open(Solid(300, 200)); var surface = Edit(window, new Rect(0, 0, 300, 200)); surface.Tool = EditorTool.Highlight;
        var line = new System.Windows.Shapes.Polyline(); line.Points.Add(new Point(20, 50)); surface.Children.Add(line); SetField(surface, "_start", new Point(20, 50)); SetField(surface, "_freehand", line);
        Call(surface, "ExtendFreehand", new Point(60, 58), false); Call(surface, "ExtendFreehand", new Point(120, 61), true); Call(surface, "FinishFreehand", new Point(200, 62), true);
        Check("traço livre vira reta", line.Points.Count == 2 && line.Points[1] == new Point(200, 50));
        window.Close();
    }

    private static void Moving()
    {
        var limits = new Size(800, 600);
        Check("seleção se desloca sem mudar de tamanho", SelectionOverlay.Shift(new Rect(100, 100, 200, 150), new Vector(50.4, -30), limits) == new Rect(150, 70, 200, 150));
        Check("seleção não sai da imagem", SelectionOverlay.Shift(new Rect(100, 100, 200, 150), new Vector(999, -999), limits) == new Rect(600, 0, 200, 150));
        Check("tela inteira não se move", SelectionOverlay.Shift(new Rect(0, 0, 800, 600), new Vector(50, 50), limits) == new Rect(0, 0, 800, 600));

        var window = Open(Solid(400, 300)); var surface = Edit(window, new Rect(0, 0, 400, 300));
        surface.Tool = EditorTool.Line; Draw(surface, new Vector(60, 0)); var line = surface.Children.OfType<System.Windows.Shapes.Line>().Single(); surface.UpdateLayout();
        Check("linha fina é encontrada por perto", ReferenceEquals(Call(surface, "FindAnnotation", new Point(80, 54)), line) && Call(surface, "FindAnnotation", new Point(80, 64)) == null);
        surface.Tool = EditorTool.Move; SetField(surface, "_start", new Point(80, 50)); Call(surface, "BeginMove", line); Call(surface, "FinishMove", new Point(110, 90)); Pump();
        bool Dark(int x, int y) => Pixel(surface.RenderImage(), x, y) != Colors.White;
        Check("linha movida na imagem final", Dark(100, 90) && !Dark(60, 50));
        surface.Undo(); Pump(); Check("desfazer movimento", Dark(60, 50) && !Dark(100, 90));
        surface.Redo(); Pump(); Check("refazer movimento", Dark(100, 90));
        SetField(surface, "_start", new Point(5, 5)); Call(surface, "BeginMove", line); Call(surface, "FinishMove", new Point(5, 5)); surface.Undo(); Pump();
        Check("clique sem mover não entra no histórico", Dark(60, 50));
        Vector? moved = null; surface.RegionDragged += delta => moved = delta;
        Check("fora das anotações, move a seleção", Call(surface, "FindAnnotation", new Point(300, 250)) == null);
        window.Close();

        window = Open(Gradient(400, 300)); surface = Edit(window, new Rect(50, 50, 100, 80));
        Call(window, "BeginRegionMove"); Call(window, "MoveRegion", new Vector(40, 30)); Pump();
        Check("recorte após mover a seleção", surface.Region == new Rect(90, 80, 100, 80) && MatchesSource(surface, Gradient(400, 300)));
        window.Close();
    }

    private static void Shortcuts()
    {
        var window = Open(Solid(300, 200));
        Check("antes da seleção, Enter e Ctrl+C não fazem nada", !window.HandleShortcut(Key.Enter, ModifierKeys.None) && !window.HandleShortcut(Key.C, ModifierKeys.Control));
        var surface = Edit(window, new Rect(0, 0, 300, 200)); surface.Tool = EditorTool.Line; Draw(surface, new Vector(40, 0));
        Check("Ctrl+Z", window.HandleShortcut(Key.Z, ModifierKeys.Control) && surface.Children.Count == 1);
        Check("Ctrl+Y", window.HandleShortcut(Key.Y, ModifierKeys.Control) && surface.Children.Count == 2);
        window.HandleShortcut(Key.Z, ModifierKeys.Control); Check("Ctrl+Shift+Z", window.HandleShortcut(Key.Z, ModifierKeys.Control | ModifierKeys.Shift) && surface.Children.Count == 2);
        Check("outras teclas são ignoradas", !window.HandleShortcut(Key.Q, ModifierKeys.None) && !window.HandleShortcut(Key.R, ModifierKeys.None));
        Check("Esc fecha", window.HandleShortcut(Key.Escape, ModifierKeys.None) && !window.IsVisible);
    }

    private static void Picking()
    {
        AppSettings.Current = new AppSettings();
        var image = Gradient(800, 600);
        var front = new DrawingRectangle(Offscreen + 100, Offscreen + 100, 200, 150); var back = new DrawingRectangle(Offscreen + 50, Offscreen + 50, 400, 300); var partial = new DrawingRectangle(Offscreen + 700, Offscreen + 500, 400, 400);
        var window = new SelectionOverlay(image, new DrawingRectangle(Offscreen, Offscreen, 800, 600), new[] { front, back, partial }) { ShowActivated = false }; window.Show();
        Check("janela da frente", window.FindTarget(new Point(150, 150)) == new Rect(100, 100, 200, 150));
        Check("janela de trás", window.FindTarget(new Point(60, 60)) == new Rect(50, 50, 400, 300));
        Check("janela parcialmente fora é recortada", window.FindTarget(new Point(750, 550)) == new Rect(700, 500, 100, 100));
        Call(window, "Hover", new Point(150, 150));
        Check("realce e medidas ao passar o mouse", ((UIElement)window.FindName("Bright")).IsVisible && ((TextBlock)window.FindName("SizeText")).Text == "200 × 150");
        Check("sem última região, R não faz nada", !window.RepeatLastRegion());
        var surface = Edit(window, new Rect(120, 130, 240, 160)); Call(window, "RememberRegion");
        Check("última região lembrada em pixels da tela", AppSettings.Current.LastRegion == new DrawingRectangle(Offscreen + 120, Offscreen + 130, 240, 160));
        Check("realce some ao editar", !((UIElement)window.FindName("Bright")).IsVisible && !((UIElement)window.FindName("Hint")).IsVisible && !((UIElement)window.FindName("SizeLabel")).IsVisible);
        window.Close();
        window = Open(image); Check("R repete", window.HandleShortcut(Key.R, ModifierKeys.None)); Pump();
        Check("mesma região", ((DrawingSurface)Field(window, "_editor")!).Region == new Rect(120, 130, 240, 160)); window.Close();
        AppSettings.Current.LastRegion = new DrawingRectangle(Offscreen + 700, Offscreen + 500, 400, 400);
        window = Open(image); window.RepeatLastRegion(); Check("região maior que a tela é limitada", ((DrawingSurface)Field(window, "_editor")!).Region == new Rect(700, 500, 100, 100)); window.Close();
        AppSettings.Current.LastRegion = new DrawingRectangle(5, 5, 100, 100);
        window = Open(image); Check("região fora da tela atual é ignorada", !window.RepeatLastRegion()); window.Close();
        Check("janelas do próprio aplicativo não são alvo", WindowFinder.VisibleWindows().All(area => area.X > Offscreen / 2));
    }

    private static void Settings()
    {
        File.Delete(AppSettings.Path);
        var settings = AppSettings.Load();
        Check("padrões sem arquivo", settings is { Tool: EditorTool.Pen, Color: "#E53935", Thickness: 3, HighlightColor: "#FDD835", HighlightThickness: 18, PrivateClipboard: false, LastRegion: null });
        settings.Save(); Check("nada é gravado sem mudança", !File.Exists(AppSettings.Path));
        settings.Tool = EditorTool.Arrow; settings.Color = "#1E88E5"; settings.Thickness = 7.5; settings.HighlightThickness = 22; settings.PrivateClipboard = true; settings.LastRegion = new DrawingRectangle(-1920, 10, 300, 200); settings.Save();
        var loaded = AppSettings.Load();
        Check("ida e volta", loaded is { Tool: EditorTool.Arrow, Color: "#1E88E5", Thickness: 7.5, HighlightThickness: 22, PrivateClipboard: true } && loaded.LastRegion == settings.LastRegion);
        File.WriteAllText(AppSettings.Path, "tool=Erase\ncolor=javascript\nthickness=9999\nhighlightThickness=abc\nlastRegion=1,2,-5,x\nlixo\n=\nprivateClipboard=talvez");
        loaded = AppSettings.Load();
        Check("valores inválidos voltam ao padrão", loaded is { Tool: EditorTool.Pen, Color: "#E53935", Thickness: 3, HighlightThickness: 18, PrivateClipboard: false, LastRegion: null });
        File.WriteAllBytes(AppSettings.Path, new byte[] { 0xFF, 0xFE, 0, 1, 2, 3 }); Check("arquivo corrompido não impede o uso", AppSettings.Load().Tool == EditorTool.Pen);
        var culture = System.Threading.Thread.CurrentThread.CurrentCulture; System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pt-BR");
        settings = new AppSettings { Thickness = 4.5 }; settings.Save(); Check("decimais independem do idioma", AppSettings.Load().Thickness == 4.5); System.Threading.Thread.CurrentThread.CurrentCulture = culture;

        AppSettings.Current = new AppSettings { Tool = EditorTool.Highlight, Color = "#43A047", Thickness = 6 };
        var window = Open(Solid(1100, 300)); var surface = Edit(window, new Rect(20, 120, 600, 100)); var buttons = Find<RadioButton>((Border)Field(window, "_toolbar")!).ToList();
        Color Current() => ((SolidColorBrush)surface.DrawBrush).Color;
        Check("ferramenta lembrada, com estilo próprio", surface.Tool == EditorTool.Highlight && surface.DrawThickness == 18 && Current() == (Color)ColorConverter.ConvertFromString("#FDD835"));
        buttons.First(button => button.Tag is EditorTool.Arrow).IsChecked = true;
        Check("outra ferramenta usa o estilo comum", surface.DrawThickness == 6 && Current() == (Color)ColorConverter.ConvertFromString("#43A047") && AppSettings.Current.Tool == EditorTool.Arrow);
        buttons.First(button => (string)button.ToolTip == "Azul").IsChecked = true; buttons.First(button => button.Tag is EditorTool.Highlight).IsChecked = true;
        Check("cor comum não muda o marca-texto", AppSettings.Current.Color == "#1E88E5" && AppSettings.Current.HighlightColor == "#FDD835" && Current() == (Color)ColorConverter.ConvertFromString("#FDD835"));
        buttons.First(button => button.Tag is EditorTool.Erase).IsChecked = true; Check("borracha não é lembrada", AppSettings.Current.Tool == EditorTool.Highlight);
        window.Close(); AppSettings.Current = new AppSettings();
    }

    private static void PrivateClipboard()
    {
        var formats = new[] { ClipboardService.ExcludeFromMonitors, ClipboardService.AllowHistory, ClipboardService.AllowCloud };
        var normal = ClipboardService.CreatePngData(Solid(10, 10), keepPrivate: false); var hidden = ClipboardService.CreatePngData(Solid(10, 10), keepPrivate: true);
        Check("desligado: sem marcações", formats.All(format => !normal.GetDataPresent(format, false)));
        Check("ligado: três marcações e a imagem", formats.All(format => hidden.GetDataPresent(format, false)) && hidden.GetDataPresent("PNG", false) && hidden.GetDataPresent(DataFormats.Bitmap, true));
        // Os bytes que o Windows realmente recebe, sem usar a área de transferência do usuário.
        foreach (var format in formats) Check($"{format}: DWORD zero", NativeBytes(hidden, format) is { Length: >= 4 } bytes && bytes.Take(4).All(value => value == 0));
    }
    private static byte[] NativeBytes(DataObject data, string format)
    {
        var request = new System.Runtime.InteropServices.ComTypes.FORMATETC { cfFormat = (short)DataFormats.GetDataFormat(format).Id, dwAspect = System.Runtime.InteropServices.ComTypes.DVASPECT.DVASPECT_CONTENT, lindex = -1, tymed = System.Runtime.InteropServices.ComTypes.TYMED.TYMED_HGLOBAL };
        ((System.Runtime.InteropServices.ComTypes.IDataObject)data).GetData(ref request, out var medium);
        var size = (int)GlobalSize(medium.unionmember); var bytes = new byte[size]; var source = GlobalLock(medium.unionmember);
        try { Marshal.Copy(source, bytes, 0, size); } finally { GlobalUnlock(medium.unionmember); }
        return bytes;
    }
    [DllImport("kernel32.dll")] private static extern nint GlobalSize(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint handle);

    private static void Toolbar()
    {
        var bar = new Size(640, 44); var first = new Rect(0, 0, 1920, 1080); var second = new Rect(1920, 173, 1440, 900);
        foreach (var (name, region, monitor) in new[]
        {
            ("direita", new Rect(1800, 500, 100, 100), first), ("topo", new Rect(100, 0, 300, 200), first), ("tela cheia", first, first), ("rodapé", new Rect(500, 1000, 200, 80), first),
            ("topo do segundo monitor", new Rect(2000, 173, 200, 100), second), ("direita do segundo monitor", new Rect(3300, 600, 60, 60), second), ("entre monitores", new Rect(1000, 10, 2000, 1000), first)
        }) Check($"dentro do monitor: {name}", monitor.Contains(new Rect(SelectionOverlay.PlaceToolbar(region, bar, monitor), bar)));
        Check("caso comum: acima da seleção", SelectionOverlay.PlaceToolbar(new Rect(300, 400, 800, 300), bar, first) == new Point(300, 350));

        var window = Open(Solid(1100, 300)); var surface = Edit(window, new Rect(20, 120, 600, 100)); var toolbar = (Border)Field(window, "_toolbar")!;
        Check("cor inicial é a da amostra", ((SolidColorBrush)surface.DrawBrush).Color == (Color)ColorConverter.ConvertFromString("#E53935"));
        var options = Find<RadioButton>(toolbar).ToList();
        foreach (var option in options) option.IsChecked = true;
        Check("uma ferramenta e uma cor ativas", options.Count(option => option.IsChecked == true) == 2);
        Find<Slider>(toolbar).Single().Value = 10; Check("espessura aplicada", surface.DrawThickness == 10);
        Check("todos os controles têm nome acessível", Find<Control>(toolbar).Where(control => control is ButtonBase and not RepeatButton || control is Slider).All(control => System.Windows.Automation.AutomationProperties.GetName(control) != ""));
        Check("cabe em uma linha em 1440 pixels a 150%", toolbar.DesiredSize.Width * 1.5 <= 1440 - 12, $"{toolbar.DesiredSize.Width}");
        foreach (var scale in new[] { 1, 1.25, 1.5, 2, 3 })
        {
            var monitor = new SelectionOverlay.MonitorArea(new Rect(0, 0, 1920 * scale, 1080 * scale), scale);
            typeof(SelectionOverlay).GetMethod("PositionToolbar", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { toolbar, new Rect(1700 * scale, 500 * scale, 100, 100), monitor });
            Check($"escala {scale:0%}: ampliada e dentro do monitor", monitor.Bounds.Contains(new Rect(new Point(Canvas.GetLeft(toolbar), Canvas.GetTop(toolbar)), toolbar.DesiredSize)) && Math.Abs(toolbar.DesiredSize.Height - 46 * scale) <= 2 * scale);
        }
        window.Close();
    }

    internal static void Dpi(bool visible)
    {
        var source = Gradient(800, 600);
        var screen = System.Windows.Forms.SystemInformation.VirtualScreen; var origin = visible ? new System.Drawing.Point(screen.Left + 200, screen.Top + 200) : new System.Drawing.Point(Offscreen, Offscreen);
        foreach (var dpi in new[] { 96, 120, 144, 192, 288 })
        {
            var window = Open(source, origin); var surface = Edit(window, new Rect(200, 150, 300, 200));
            var handle = new WindowInteropHelper(window).Handle;
            if (VisualTreeHelper.GetDpi(window).DpiScaleX != dpi / 96d) SendDpiChanged(handle, dpi);
            Pump();
            var name = $"{dpi / 96d:0%}"; var canvas = (Canvas)window.Content;
            Check($"{name}: WPF usa o DPI simulado", VisualTreeHelper.GetDpi(window).DpiScaleX == dpi / 96d);
            NativeMethods.GetWindowRect(handle, out var rect);
            Check($"{name}: janela cobre a captura, em pixels", rect.Left == origin.X && rect.Top == origin.Y && rect.Right - rect.Left == 800 && rect.Bottom - rect.Top == 600);
            Check($"{name}: uma unidade é um pixel", Math.Abs(canvas.ActualWidth - 800) < .01 && Math.Abs(canvas.ActualHeight - 600) < .01);
            var point = surface.PointToScreen(new Point(333, 222));
            Check($"{name}: coordenadas coincidem com a tela", Math.Abs(point.X - origin.X - 333) < .01 && Math.Abs(point.Y - origin.Y - 222) < .01);
            Check($"{name}: recorte idêntico", MatchesSource(surface, source));
            if (visible)
            {
                // Compara o interior da seleção, longe das alças, com os pixels que chegaram à tela.
                System.Threading.Thread.Sleep(300); Pump(); System.Threading.Thread.Sleep(100);
                var shown = Pixels(ScreenCapture.Capture(new Rect(origin.X, origin.Y, 800, 600))); var original = Pixels(source); var different = 0;
                for (var y = 170; y < 330; y++) for (var x = 220; x < 480; x++)
                    for (var channel = 0; channel < 3; channel++) if (shown[(y * 800 + x) * 4 + channel] != original[(y * 800 + x) * 4 + channel]) { different++; break; }
                Check($"{name}: imagem na tela idêntica à captura", different == 0, $"{different} pixels diferentes");
            }
            window.Close();
        }
        Check("escala do monitor principal é válida", NativeMethods.GetMonitorScale(1, 1) >= 1);
    }

    private static void Files()
    {
        var folder = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"quickshot-files-{Environment.ProcessId}")).FullName;
        try
        {
            var path = Path.Combine(folder, "captura.png");
            SafeFile.Write(path, stream => stream.Write(new byte[] { 1, 2, 3 }));
            Check("grava arquivo novo", File.ReadAllBytes(path).SequenceEqual(new byte[] { 1, 2, 3 }));
            SafeFile.Write(path, stream => stream.Write(new byte[] { 4 }));
            Check("substitui arquivo existente", File.ReadAllBytes(path).SequenceEqual(new byte[] { 4 }));
            try { SafeFile.Write(path, stream => { stream.Write(new byte[] { 9 }); throw new IOException("disco cheio"); }); Check("falha é informada", false); }
            catch (IOException) { Check("falha preserva o arquivo anterior", File.ReadAllBytes(path).SequenceEqual(new byte[] { 4 })); }
            Check("sem temporários", Directory.GetFiles(folder).Length == 1);
        }
        finally { Directory.Delete(folder, true); }

        File.WriteAllBytes(DebugLog.Path, new byte[1024 * 1024]); DebugLog.Write("após rotação");
        Check("log rotaciona em 1 MiB", File.Exists(DebugLog.Path + ".old") && File.ReadAllText(DebugLog.Path).Contains("após rotação") && new FileInfo(DebugLog.Path).Length < 1024);
    }

    private static void Icon()
    {
        using var icon = AppIcon.CreateIcon();
        Check("ícone no tamanho da bandeja", icon.Size == System.Windows.Forms.SystemInformation.SmallIconSize);
    }

    // --- Medições

    private static void MeasureMemory()
    {
        void Report(string name) { using var process = Process.GetCurrentProcess(); Console.WriteLine($"{name}: privada={process.PrivateMemorySize64 >> 20} MiB, física={process.WorkingSet64 >> 20} MiB"); }
        var bounds = System.Windows.Forms.SystemInformation.VirtualScreen;
        SelectionOverlay.WarmUp(); Pump(); Report("início");
        for (var count = 1; count <= 30; count++)
        {
            var snapshot = ScreenCapture.Capture(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
            var window = new SelectionOverlay(snapshot, new DrawingRectangle(Offscreen, Offscreen, bounds.Width, bounds.Height)) { ShowActivated = false };
            window.Show(); Pump(); var surface = Edit(window, new Rect(50, 50, bounds.Width / 2, bounds.Height - 100));
            ClipboardService.CreatePngData(surface.RenderImage(), keepPrivate: false); window.Close(); Pump();
            if (count is 1 or 5 or 10 or 20 or 30) Report($"{count} capturas");
        }
    }

    private static void MeasureStartup(bool warm)
    {
        if (warm) { SelectionOverlay.WarmUp(); Pump(); }
        var bounds = System.Windows.Forms.SystemInformation.VirtualScreen; var watch = Stopwatch.StartNew();
        var snapshot = ScreenCapture.Capture(new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        var window = new SelectionOverlay(snapshot, new DrawingRectangle(Offscreen, Offscreen, bounds.Width, bounds.Height)) { ShowActivated = false };
        var prepared = watch.ElapsedMilliseconds; var rendered = false;
        window.ContentRendered += (_, _) => rendered = true; window.Show();
        while (!rendered) Pump(DispatcherPriority.Background);
        Console.WriteLine($"preparado={prepared} ms, exibido={watch.ElapsedMilliseconds} ms"); window.Close();
    }

    // --- Apoio

    internal static void Check(string name, bool passed, string details = "")
    {
        Console.WriteLine($"  {(passed ? "OK   " : "FALHA")} {name}{(passed || details == "" ? "" : $" [{details}]")}");
        if (passed) _passed++; else _failures++;
    }
    internal static void Pump(DispatcherPriority priority = DispatcherPriority.ApplicationIdle) => Dispatcher.CurrentDispatcher.Invoke(() => { }, priority);
    internal static object? Field(object target, string name) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(target);
    private static void SetField(object target, string name, object? value) => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(target, value);
    private static object? Call(object target, string name, params object?[] arguments) => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)!.Invoke(target, arguments);
    private static SelectionOverlay Open(BitmapSource image, System.Drawing.Point? at = null)
    {
        var window = new SelectionOverlay(image, new DrawingRectangle(at?.X ?? Offscreen, at?.Y ?? Offscreen, image.PixelWidth, image.PixelHeight)) { ShowActivated = false };
        window.Show(); return window;
    }
    private static DrawingSurface Edit(SelectionOverlay window, Rect region) { Call(window, "StartEditing", region); Pump(); return (DrawingSurface)Field(window, "_editor")!; }
    private static void Apply(SelectionOverlay window, Rect region) { Call(window, "ApplyRegion", region); Pump(); }
    private static TextBox Editor(DrawingSurface surface) => (TextBox)Field(surface, "_textEditor")!;
    private static void Type(DrawingSurface surface, Point at, string text)
    {
        surface.UpdateLayout(); Call(surface, "BeginTextEdit", at, Call(surface, "FindAnnotation", at) as TextBlock); Pump(); Editor(surface).Text = text; Pump();
    }
    /// <summary>Percorre o desenho de uma forma pelo mesmo caminho do mouse, com um deslocamento definido.</summary>
    private static bool Draw(DrawingSurface surface, Vector drag)
    {
        var before = surface.Children.Count; var start = new Point(50, 50);
        SetField(surface, "_start", start); var shape = (UIElement)Call(surface, "CreateShape")!;
        surface.Children.Add(shape); SetField(surface, "_preview", shape); Call(surface, "FinishShape", start + drag); Pump();
        return surface.Children.Count > before;
    }
    internal static void Press(Window window, Key key) => window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(window)!, 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
    internal static IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in Find<T>(child)) yield return nested;
        }
    }
    private static BitmapSource Create(int width, int height, byte[] pixels) { var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4); bitmap.Freeze(); return bitmap; }
    private static BitmapSource Solid(int width, int height) { var pixels = new byte[width * height * 4]; Array.Fill(pixels, (byte)255); return Create(width, height, pixels); }
    /// <summary>Cada pixel tem uma cor própria, o que denuncia qualquer deslocamento ou ampliação.</summary>
    internal static BitmapSource Gradient(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++) for (var x = 0; x < width; x++)
        {
            var index = (y * width + x) * 4; pixels[index] = (byte)x; pixels[index + 1] = (byte)y; pixels[index + 2] = (byte)(x / 256 * 64 + y / 256);
        }
        return Create(width, height, pixels);
    }
    internal static byte[] Pixels(BitmapSource bitmap) { var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0); return pixels; }
    private static Color Pixel(BitmapSource bitmap, int x, int y) { var pixel = new byte[4]; bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0); return Color.FromRgb(pixel[2], pixel[1], pixel[0]); }
    internal static bool MatchesSource(DrawingSurface surface, BitmapSource source)
    {
        var region = surface.Region; var image = surface.RenderImage();
        if (image.PixelWidth != (int)region.Width || image.PixelHeight != (int)region.Height) return false;
        var actual = Pixels(image); var expected = Pixels(source);
        for (var y = 0; y < image.PixelHeight; y++) for (var x = 0; x < image.PixelWidth; x++)
        {
            var left = (y * image.PixelWidth + x) * 4; var right = ((y + (int)region.Y) * source.PixelWidth + x + (int)region.X) * 4;
            for (var channel = 0; channel < 3; channel++) if (actual[left + channel] != expected[right + channel]) return false;
        }
        return true;
    }

    /// <summary>Envia a mensagem que o Windows manda quando a janela passa a outra escala.</summary>
    private static void SendDpiChanged(nint handle, int dpi)
    {
        NativeMethods.GetWindowRect(handle, out var rect);
        rect.Right = rect.Left + (rect.Right - rect.Left) * dpi / 96; rect.Bottom = rect.Top + (rect.Bottom - rect.Top) * dpi / 96;
        var suggested = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.RECT>());
        try { Marshal.StructureToPtr(rect, suggested, false); SendMessage(handle, 0x02E0, dpi | dpi << 16, suggested); }
        finally { Marshal.FreeHGlobal(suggested); }
    }
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hWnd, int message, nint wParam, nint lParam);
}
