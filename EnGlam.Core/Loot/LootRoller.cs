namespace EnGlam.Core.Loot;

/// <summary>Wurfmöglichkeit eines Slots; Werte wie FFXIVClientStructs RollState.</summary>
public enum RollState : byte
{
    UpToNeed = 0,
    UpToGreed = 1,
    UpToPass = 2,
    Rolled = 17,
    Unavailable = 21,
    Unknown = 28,
}

/// <summary>Wurf; Werte wie FFXIVClientStructs RollResult, so gehen sie an die Würfelfunktion des Spiels.</summary>
public enum RollChoice
{
    None = 0,
    Need = 1,
    Greed = 2,
    Pass = 5,
}

/// <param name="Index">Position im Loot-Fenster (0 bis 15).</param>
/// <param name="ItemId">Wie im Spiel, bei HQ um 1000000 erhöht.</param>
/// <param name="Awarded">Ob der Slot schon vergeben ist (RollResult ungleich UnAwarded).</param>
public readonly record struct LootSlot(int Index, uint ChestObjectId, uint ChestItemIndex, uint ItemId, RollState State, bool Awarded);

public readonly record struct RollDecision(int Index, RollChoice Choice);

/// <summary>
/// Entscheidet, wie auf offene Slots gewürfelt wird: Need auf gesuchte Items, wo erlaubt, sonst Greed;
/// auf alles andere Pass, wenn eingestellt. Jeder Slot wird nur einmal entschieden, damit ein Wurf, dessen
/// Ergebnis im Spiel noch nicht angekommen ist, nicht wiederholt wird.
/// </summary>
public sealed class LootRoller
{
    private const uint HqOffset = 1_000_000;

    private readonly HashSet<(uint Chest, uint Index, uint Item)> decided = [];

    public IReadOnlyList<RollDecision> Decide(IEnumerable<LootSlot> slots, IReadOnlySet<uint> wanted, bool passOthers)
    {
        var result = new List<RollDecision>();
        var present = new HashSet<(uint, uint, uint)>();

        foreach (var slot in slots)
        {
            if (slot.ItemId == 0)
                continue;

            var key = (slot.ChestObjectId, slot.ChestItemIndex, slot.ItemId);
            present.Add(key);

            if (slot.Awarded || slot.State is not (RollState.UpToNeed or RollState.UpToGreed or RollState.UpToPass))
                continue;

            if (decided.Contains(key))
                continue;

            var itemId = slot.ItemId >= HqOffset ? slot.ItemId - HqOffset : slot.ItemId;

            var choice = wanted.Contains(itemId)
                ? slot.State switch
                {
                    RollState.UpToNeed => RollChoice.Need,
                    RollState.UpToGreed => RollChoice.Greed,
                    _ => RollChoice.None,       // nur Pass möglich: liegen lassen, falls doch noch jemand verteilt
                }
                : passOthers ? RollChoice.Pass : RollChoice.None;

            if (choice == RollChoice.None)
                continue;

            decided.Add(key);
            result.Add(new RollDecision(slot.Index, choice));
        }

        // Vergessen, was nicht mehr im Fenster ist, damit die Menge nicht wächst.
        decided.IntersectWith(present);
        return result;
    }
}
