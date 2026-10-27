using EnGlam.Core.Drops;

namespace EnGlam.Core.Tests;

public class DropIndexTests
{
    private static DropIndex Build(
        BossDropRow[]? bossDrops = null,
        BossChestRow[]? bossChests = null,
        DungeonDropRow[]? drops = null,
        ChestRow[]? chests = null,
        ChestItemRow[]? chestItems = null) =>
        DropIndex.Build(bossDrops ?? [], bossChests ?? [], drops ?? [], chests ?? [], chestItems ?? []);

    [Fact]
    public void BossDrop_GivesSourceWithoutChance()
    {
        var index = Build(bossDrops: [new(4, 100)]);

        var source = Assert.Single(index.SourcesFor(100));
        Assert.Equal(new DropSource(4, DropKind.Boss, null), source);
    }

    [Fact]
    public void ChestItem_ResolvesDungeonThroughChest()
    {
        var index = Build(chests: [new(7, 12)], chestItems: [new(7, 200, 12.5m)]);

        var source = Assert.Single(index.SourcesFor(200));
        Assert.Equal(new DropSource(12, DropKind.Chest, 12.5m), source);
    }

    [Fact]
    public void ItemInSeveralDungeons_GetsOneSourcePerDungeon()
    {
        var index = Build(bossChests: [new(30, 300), new(10, 300), new(20, 300)]);

        Assert.Equal([10u, 20u, 30u], index.SourcesFor(300).Select(s => s.ContentFinderConditionId));
    }

    [Fact]
    public void ItemWithoutSource_GivesEmptyList()
    {
        var index = Build(bossDrops: [new(4, 100)]);

        Assert.Empty(index.SourcesFor(999));
    }

    [Fact]
    public void SameItemInSeveralChestsOfOneDungeon_KeepsHighestChance()
    {
        var index = Build(
            chests: [new(1, 12), new(2, 12)],
            chestItems: [new(1, 200, 5m), new(2, 200, 20m)]);

        var source = Assert.Single(index.SourcesFor(200));
        Assert.Equal(20m, source.Chance);
    }

    [Fact]
    public void ChanceAboveHundred_IsCappedAtHundred()
    {
        var index = Build(chests: [new(1, 12)], chestItems: [new(1, 200, 400m)]);

        Assert.Equal(100m, Assert.Single(index.SourcesFor(200)).Chance);
    }

    [Fact]
    public void ChestItemOfUnknownChest_IsIgnored()
    {
        var index = Build(chests: [new(1, 12)], chestItems: [new(99, 200, 10m)]);

        Assert.Empty(index.SourcesFor(200));
        Assert.Equal(0, index.ItemCount);
    }

    [Fact]
    public void BossAndChestInSameDungeon_AreSeparateSources()
    {
        var index = Build(
            bossDrops: [new(12, 200)],
            chests: [new(1, 12)],
            chestItems: [new(1, 200, 8m)]);

        Assert.Equal([DropKind.Boss, DropKind.Chest], index.SourcesFor(200).Select(s => s.Kind));
    }

    [Fact]
    public void DuplicateBossChestRows_AreMerged()
    {
        var index = Build(bossChests: [new(12, 200), new(12, 200)]);

        Assert.Single(index.SourcesFor(200));
    }
}
