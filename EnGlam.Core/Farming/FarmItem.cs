using EnGlam.Core.Planning;

namespace EnGlam.Core.Farming;

public enum FarmItemState
{
    Waiting,
    Running,
    Obtained,
    AlreadyOwned,
    Skipped,
    LimitReached,
    Failed,
}

/// <summary>Ein Eintrag der Farm-Liste.</summary>
public sealed class FarmItem(uint itemId, string name)
{
    public uint ItemId { get; } = itemId;

    public string Name { get; } = name;

    public FarmItemState State { get; internal set; } = FarmItemState.Waiting;

    public int Runs { get; internal set; }

    /// <summary>Grund, wenn das Item übersprungen wurde.</summary>
    public SkipReason? SkipReason { get; internal set; }

    /// <summary>Dungeon des aktuellen oder letzten Runs.</summary>
    public string? DutyName { get; internal set; }

    public bool IsDone => State is not (FarmItemState.Waiting or FarmItemState.Running);

    /// <summary>Setzt einen erledigten Eintrag für einen neuen Durchgang zurück.</summary>
    public void Reset()
    {
        State = FarmItemState.Waiting;
        Runs = 0;
        SkipReason = null;
        DutyName = null;
    }
}
