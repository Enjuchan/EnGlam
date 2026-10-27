namespace EnGlam.Core.History;

public enum HistoryState
{
    /// <summary>Angefangen, aber noch nicht erhalten: gestoppt, Limit erreicht oder übersprungen.</summary>
    Open,

    /// <summary>Erhalten; bleibt so, auch wenn das Item später nicht mehr im Inventar ist.</summary>
    Obtained,
}

/// <summary>Ein Item in der Historie. Eigenschaften mit set, damit die Plugin-Konfiguration es speichern kann.</summary>
public sealed class HistoryEntry
{
    public uint ItemId { get; set; }
    public int Runs { get; set; }
    public int Sessions { get; set; }
    public DateTime FirstFarmed { get; set; }
    public DateTime LastFarmed { get; set; }
    public HistoryState State { get; set; }
    public DateTime? ObtainedAt { get; set; }
}

/// <summary>
/// Hält fest, was mit EnGlam gefarmt wurde. Eine Sitzung ist ein Druck auf Start; ein Item zählt pro Sitzung
/// höchstens einmal als Sitzung, egal wie viele Runs es dort bekommt.
/// </summary>
/// <param name="store">Liste aus der Plugin-Konfiguration.</param>
/// <param name="save">Speichert die Konfiguration.</param>
public sealed class FarmHistory(List<HistoryEntry> store, Action save)
{
    private readonly HashSet<uint> countedThisSession = [];

    /// <summary>Zuletzt gefarmte zuerst.</summary>
    public IReadOnlyList<HistoryEntry> Entries => [.. store.OrderByDescending(e => e.LastFarmed)];

    public void BeginSession() => countedThisSession.Clear();

    public void RecordRun(uint itemId, DateTime now)
    {
        var entry = GetOrCreate(itemId, now);

        if (countedThisSession.Add(itemId))
            entry.Sessions++;

        entry.Runs++;
        entry.LastFarmed = now;
        save();
    }

    public void RecordObtained(uint itemId, DateTime now)
    {
        var entry = GetOrCreate(itemId, now);
        if (entry.State == HistoryState.Obtained)
            return;

        entry.State = HistoryState.Obtained;
        entry.ObtainedAt = now;
        entry.LastFarmed = now;
        save();
    }

    public void Remove(uint itemId)
    {
        if (store.RemoveAll(e => e.ItemId == itemId) > 0)
            save();
    }

    private HistoryEntry GetOrCreate(uint itemId, DateTime now)
    {
        var entry = store.FirstOrDefault(e => e.ItemId == itemId);
        if (entry is not null)
            return entry;

        entry = new HistoryEntry { ItemId = itemId, FirstFarmed = now, LastFarmed = now, State = HistoryState.Open };
        store.Add(entry);
        return entry;
    }
}
