namespace EnGlam.Core.Farming;

/// <summary>
/// Zählt Runs, die der Spieler selbst läuft, solange EnGlam nicht farmt: Betritt er eine Duty, in der ein offenes
/// Item der Farm-Liste droppt, zählt beim Verlassen ein Run für jedes dieser Items, und nach der Wartezeit wird
/// das Inventar geprüft. Handläufe zählen nicht gegen die Run-Limits und öffnen keine Farm-Sitzung.
/// </summary>
/// <param name="sourceTerritories">TerritoryType-IDs der Duties, in denen ein Item droppt.</param>
public sealed class ManualTracker(
    Func<uint, IEnumerable<uint>> sourceTerritories,
    Func<uint, int> countItem,
    Action<uint> onRun,
    Action<uint> onObtained)
{
    private uint? dutyTerritory;
    private List<FarmItem> inDuty = [];
    private List<FarmItem> toCheck = [];
    private DateTime leftAt;

    /// <summary>Ob der Spieler gerade in einer Duty mit offenen Listen-Items ist.</summary>
    public bool InTrackedDuty => dutyTerritory is not null;

    /// <summary>Die offenen Listen-Items, die in der aktuellen Duty droppen.</summary>
    public IReadOnlyList<FarmItem> Tracked => inDuty;

    public void Tick(DateTime now, uint territory, bool farming, IReadOnlyList<FarmItem> farmList)
    {
        if (farming)
        {
            // Runs beim Farmen zählt der Ablauf selbst.
            dutyTerritory = null;
            inDuty = [];
            toCheck = [];
            return;
        }

        if (dutyTerritory is { } duty)
        {
            if (territory == duty)
                return;

            foreach (var item in inDuty)
                onRun(item.ItemId);

            toCheck = inDuty;
            inDuty = [];
            dutyTerritory = null;
            leftAt = now;
        }

        if (toCheck.Count > 0 && now - leftAt >= FarmController.CheckDelay)
        {
            foreach (var item in toCheck.Where(i => countItem(i.ItemId) > 0))
            {
                item.State = FarmItemState.Obtained;
                onObtained(item.ItemId);
            }

            toCheck = [];
        }

        var candidates = farmList
            .Where(IsOpen)
            .Where(i => sourceTerritories(i.ItemId).Contains(territory))
            .Where(i => countItem(i.ItemId) == 0)
            .ToList();

        if (candidates.Count > 0)
        {
            dutyTerritory = territory;
            inDuty = candidates;
        }
    }

    private static bool IsOpen(FarmItem item) =>
        item.State is not (FarmItemState.Obtained or FarmItemState.AlreadyOwned or FarmItemState.Running);
}
