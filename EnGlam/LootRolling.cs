using Dalamud.Plugin.Services;
using EnGlam.Core.Loot;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using RollState = EnGlam.Core.Loot.RollState;

namespace EnGlam;

/// <summary>
/// Würfelt im Loot-Fenster. FFXIVClientStructs hat dafür keine Methode; die Funktion wird per Signatur gesucht,
/// dieselbe, die LazyLoot nutzt: bool RollItem(Loot*, RollResult, uint slot). Findet der Scan sie nicht,
/// etwa nach einem Spiel-Patch, bleibt das Würfeln aus, und das Farmen läuft ohne weiter.
/// </summary>
public sealed unsafe class LootRolling
{
    private const string RollItemSignature = "41 83 F8 ?? 0F 83 ?? ?? ?? ?? 48 89 5C 24 08";

    private readonly delegate* unmanaged<Loot*, int, uint, byte> rollItem;
    private readonly LootRoller roller = new();
    private readonly IPluginLog log;

    public LootRolling(ISigScanner sigScanner, IPluginLog log)
    {
        this.log = log;

        if (sigScanner.TryScanText(RollItemSignature, out var address))
        {
            rollItem = (delegate* unmanaged<Loot*, int, uint, byte>)address;
            log.Information("Würfelfunktion gefunden.");
        }
        else
        {
            log.Warning("Würfelfunktion nicht gefunden; automatisches Würfeln ist aus.");
        }
    }

    public bool Available => rollItem != null;

    public void Update(IReadOnlySet<uint> wanted, bool passOthers)
    {
        if (!Available)
            return;

        var loot = Loot.Instance();
        if (loot == null)
            return;

        var slots = new List<LootSlot>();
        for (var i = 0; i < loot->Items.Length; i++)
        {
            ref var item = ref loot->Items[i];
            slots.Add(new LootSlot(i, item.ChestObjectId, item.ChestItemIndex, item.ItemId,
                (RollState)item.RollState, item.RollResult != RollResult.UnAwarded));
        }

        foreach (var decision in roller.Decide(slots, wanted, passOthers))
        {
            var ok = rollItem(loot, (int)decision.Choice, (uint)decision.Index) != 0;
            log.Information("Würfeln: Slot {Slot}, Item {Item}, {Choice}{Result}.",
                decision.Index, loot->Items[decision.Index].ItemId, decision.Choice, ok ? "" : " (vom Spiel abgelehnt)");
        }
    }
}
