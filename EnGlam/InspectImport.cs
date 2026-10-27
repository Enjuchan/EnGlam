using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace EnGlam;

/// <summary>Liest die Ausrüstung des Charakters, der gerade im Spiel untersucht wird.</summary>
public static unsafe class InspectImport
{
    private const int SoulCrystalSlot = 13;

    /// <summary>
    /// Nur bei offenem Untersuchen-Fenster, weil der Examine-Container nach dem Schließen den letzten
    /// Charakter behält und sonst ein alter Stand importiert würde.
    /// </summary>
    public static bool WindowOpen
    {
        get
        {
            var addon = RaptureAtkUnitManager.Instance()->GetAddonByName("CharacterInspect");
            return addon != null && addon->IsVisible;
        }
    }

    /// <summary>Sichtbare Items: das Glamour-Item, wo eines aufgelegt ist, sonst das getragene Item.</summary>
    public static IReadOnlyList<uint> ItemIds()
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.Examine);
        if (container == null)
            return [];

        var ids = new List<uint>();
        for (var i = 0; i < container->Size; i++)
        {
            if (i == SoulCrystalSlot)
                continue;

            var slot = container->GetInventorySlot(i);
            if (slot == null || slot->GetItemId() == 0)
                continue;

            var glamour = slot->GetGlamourId();
            ids.Add(glamour != 0 ? glamour : slot->GetItemId());
        }

        return [.. ids.Distinct()];
    }
}
