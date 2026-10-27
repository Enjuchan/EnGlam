using Dalamud.Plugin.Services;
using EnGlam.Core.Duties;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace EnGlam;

/// <summary>Liest Inhalte und anlegbare Items aus den Spieldaten.</summary>
public sealed class GameData
{
    public required IReadOnlyDictionary<uint, DutyInfo> Duties { get; init; }

    /// <summary>Alle anlegbaren Items, nach Name sortiert.</summary>
    public required IReadOnlyList<(uint Id, string Name, uint Icon)> EquipableItems { get; init; }

    public static GameData Load(IDataManager data)
    {
        var dawnRows = data.GetExcelSheet<DawnContent>().ToList();
        var participable = data.GetSubrowExcelSheet<DawnContentParticipable>();

        var dawnByCfc = new Dictionary<uint, uint>();
        foreach (var row in dawnRows)
            dawnByCfc.TryAdd(row.Content.RowId, row.RowId);

        // Position unter den Trust-Zeilen; Unknown13 ist das Trust-Kennzeichen, wie AutoDuty es liest.
        var trustOrdinal = new Dictionary<uint, int>();
        var ordinal = 0;
        foreach (var row in dawnRows.Where(r => r.Unknown13))
            trustOrdinal.TryAdd(row.Content.RowId, ordinal++);

        var duties = new Dictionary<uint, DutyInfo>();
        foreach (var cfc in data.GetExcelSheet<ContentFinderCondition>())
        {
            if (!DutyModeRules.SupportedContentTypes.Contains(cfc.ContentType.RowId))
                continue;

            if (cfc.TerritoryType.ValueNullable is not { } territory || territory.ExVersion.ValueNullable is null)
                continue;

            var name = cfc.Name.ExtractText();
            if (string.IsNullOrEmpty(name))
                continue;

            var dawnRow = dawnByCfc.GetValueOrDefault(cfc.RowId);
            var participants = dawnRow != 0 && participable.TryGetSubrowCount(dawnRow, out var count) ? count : 0;

            duties[cfc.RowId] = new DutyInfo(
                cfc.RowId,
                territory.RowId,
                cfc.Content.RowId,
                char.ToUpper(name[0]) + name[1..],
                cfc.ContentType.RowId,
                territory.ExVersion.RowId,
                cfc.ClassJobLevelRequired,
                dawnRow,
                participants,
                trustOrdinal.GetValueOrDefault(cfc.RowId, -1));
        }

        var items = data.GetExcelSheet<Item>()
            .Where(i => i.EquipSlotCategory.RowId != 0)
            .Select(i => (i.RowId, Name: i.Name.ExtractText(), Icon: (uint)i.Icon))
            .Where(i => i.Name.Length > 0)
            .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new GameData { Duties = duties, EquipableItems = items };
    }

    private Dictionary<uint, (string Name, uint Icon)>? items;

    public string ItemName(uint itemId) => Lookup(itemId).Name;

    public uint ItemIcon(uint itemId) => Lookup(itemId).Icon;

    private (string Name, uint Icon) Lookup(uint itemId)
    {
        items ??= EquipableItems.ToDictionary(i => i.Id, i => (i.Name, i.Icon));
        return items.GetValueOrDefault(itemId, ($"Item {itemId}", 0u));
    }

    public static bool IsUnlocked(DutyInfo duty) => UIState.IsInstanceContentUnlocked(duty.InstanceContentId);
}
