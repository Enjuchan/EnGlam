namespace EnGlam.Core.Drops;

public enum DropKind
{
    Boss,
    BossChest,
    Chest,
    Other,
}

/// <summary>
/// Eine Quelle eines Items: ein Dungeon und die Art, wie das Item dort fällt.
/// Chance in Prozent, nur bei Schatztruhen bekannt.
/// </summary>
public sealed record DropSource(uint ContentFinderConditionId, DropKind Kind, decimal? Chance);

/// <summary>
/// Ordnet jedem Item seine Dungeon-Quellen zu. Pro Item, Dungeon und Art gibt es genau eine Quelle.
/// </summary>
public sealed class DropIndex
{
    private readonly Dictionary<uint, List<DropSource>> sources;

    private DropIndex(Dictionary<uint, List<DropSource>> sources) => this.sources = sources;

    public int ItemCount => sources.Count;

    public IReadOnlyList<DropSource> SourcesFor(uint itemId) =>
        sources.TryGetValue(itemId, out var list) ? list : [];

    public static DropIndex Build(
        IEnumerable<BossDropRow> bossDrops,
        IEnumerable<BossChestRow> bossChests,
        IEnumerable<DungeonDropRow> drops,
        IEnumerable<ChestRow> chests,
        IEnumerable<ChestItemRow> chestItems)
    {
        // Schlüssel: (Item, Dungeon, Art) → höchste bekannte Chance. Mehrere Truhen eines
        // Dungeons können dasselbe Item enthalten; für die Planung zählt die beste.
        var merged = new Dictionary<(uint Item, uint Cfc, DropKind Kind), decimal?>();

        void Add(uint item, uint cfc, DropKind kind, decimal? chance)
        {
            var key = (item, cfc, kind);
            merged[key] = merged.TryGetValue(key, out var old) ? Max(old, chance) : chance;
        }

        foreach (var r in bossDrops)
            Add(r.ItemId, r.ContentFinderConditionId, DropKind.Boss, null);

        foreach (var r in bossChests)
            Add(r.ItemId, r.ContentFinderConditionId, DropKind.BossChest, null);

        foreach (var r in drops)
            Add(r.ItemId, r.ContentFinderConditionId, DropKind.Other, null);

        var chestDungeon = new Dictionary<uint, uint>();
        foreach (var c in chests)
            chestDungeon[c.ChestId] = c.ContentFinderConditionId;

        foreach (var r in chestItems)
        {
            if (chestDungeon.TryGetValue(r.ChestId, out var cfc))
                Add(r.ItemId, cfc, DropKind.Chest, Cap(r.Probability));
        }

        var result = new Dictionary<uint, List<DropSource>>();
        foreach (var ((item, cfc, kind), chance) in merged)
        {
            if (!result.TryGetValue(item, out var list))
                result[item] = list = [];

            list.Add(new DropSource(cfc, kind, chance));
        }

        foreach (var list in result.Values)
            list.Sort((a, b) => a.ContentFinderConditionId != b.ContentFinderConditionId
                ? a.ContentFinderConditionId.CompareTo(b.ContentFinderConditionId)
                : a.Kind.CompareTo(b.Kind));

        return new DropIndex(result);
    }

    // Die Rohdaten zählen Items pro 100 Öffnungen; eine Truhe kann ein Item mehrfach geben,
    // dann liegt der Wert über 100. Als Chance, es mindestens einmal zu bekommen, ist 100 die Grenze.
    private static decimal? Cap(decimal? probability) =>
        probability is { } p ? Math.Min(p, 100m) : null;

    private static decimal? Max(decimal? a, decimal? b) =>
        a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);
}
