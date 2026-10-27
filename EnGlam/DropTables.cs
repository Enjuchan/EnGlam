using Dalamud.Plugin.Services;
using EnGlam.Core.Drops;
using LuminaSupplemental.Excel.Model;
using LuminaSupplemental.Excel.Services;

namespace EnGlam;

/// <summary>
/// Dungeon-Drop-Tabellen aus LuminaSupplemental. Die Spieldaten selbst enthalten keine Loot-Tabellen.
/// </summary>
public sealed class DropTables
{
    public required List<DungeonBossDrop> BossDrops { get; init; }
    public required List<DungeonBossChest> BossChests { get; init; }
    public required List<DungeonDrop> Drops { get; init; }
    public required List<DungeonChest> Chests { get; init; }
    public required List<DungeonChestItem> ChestItems { get; init; }

    public static DropTables Load(IPluginLog log) => new()
    {
        BossDrops = Read<DungeonBossDrop>(CsvLoader.DungeonBossDropResourceName, log),
        BossChests = Read<DungeonBossChest>(CsvLoader.DungeonBossChestResourceName, log),
        Drops = Read<DungeonDrop>(CsvLoader.DungeonDropItemResourceName, log),
        Chests = Read<DungeonChest>(CsvLoader.DungeonChestResourceName, log),
        ChestItems = Read<DungeonChestItem>(CsvLoader.DungeonChestItemResourceName, log),
    };

    public DropIndex ToIndex() => DropIndex.Build(
        BossDrops.Select(r => new BossDropRow(r.ContentFinderConditionId, r.ItemId)),
        BossChests.Select(r => new BossChestRow(r.ContentFinderConditionId, r.ItemId)),
        Drops.Select(r => new DungeonDropRow(r.ContentFinderConditionId, r.ItemId)),
        Chests.Select(r => new ChestRow(r.RowId, r.ContentFinderConditionId)),
        ChestItems.Select(r => new ChestItemRow(r.ChestId, r.ItemId, r.Probability)));

    private static List<T> Read<T>(string resource, IPluginLog log) where T : ICsv, new()
    {
        var rows = CsvLoader.LoadResource<T>(resource, true, out var failedLines, out var exceptions);

        log.Information("Drop-Daten {Table}: {Rows} Zeilen, {Failed} fehlerhaft.", typeof(T).Name, rows.Count, failedLines.Count);

        if (exceptions.Count > 0)
            log.Warning(exceptions[0], "Erste fehlerhafte Zeile in {Table}: {Line}", typeof(T).Name, failedLines[0]);

        return rows;
    }
}
