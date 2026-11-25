using System.Windows.Threading;

namespace QuickShot;

/// <summary>
/// Devolve a memória das imagens depois de uma captura. Elas ficam fora do heap do .NET, que, com o
/// aplicativo ocioso na bandeja, não teria motivo para coletá-las tão cedo.
/// </summary>
internal static class MemoryCleaner
{
    private static DispatcherTimer? _timer;

    public static void Schedule()
    {
        if (_timer == null)
        {
            _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
            _timer.Tick += (_, _) => { _timer.Stop(); Clean(); };
        }
        _timer.Stop(); _timer.Start();
    }
    /// <summary>Uma nova captura não deve disputar tempo com a limpeza da anterior.</summary>
    public static void Cancel() => _timer?.Stop();

    internal static void Clean()
    {
        // A primeira coleta enfileira os finalizadores que soltam as imagens; a segunda recolhe o que eles liberaram.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }
}
