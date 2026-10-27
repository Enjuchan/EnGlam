using Dalamud.Plugin.Services;

namespace EnGlam;

/// <summary>
/// Misst, wie lange das Fenster pro Frame zum Zeichnen braucht, und schreibt alle 10 Sekunden
/// Durchschnitt und Maximum ins Log, solange es offen ist.
/// </summary>
public sealed class FrameTiming(IPluginLog log)
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private TimeSpan total;
    private TimeSpan max;
    private int frames;
    private DateTime nextReport = DateTime.UtcNow + Interval;

    public void Add(TimeSpan elapsed)
    {
        total += elapsed;
        frames++;
        if (elapsed > max)
            max = elapsed;

        if (DateTime.UtcNow < nextReport)
            return;

        log.Information("Fenster-Zeichenzeit: Ø {Average:0.00} ms, max {Max:0.00} ms über {Frames} Frames.",
            total.TotalMilliseconds / frames, max.TotalMilliseconds, frames);

        total = TimeSpan.Zero;
        max = TimeSpan.Zero;
        frames = 0;
        nextReport = DateTime.UtcNow + Interval;
    }
}
