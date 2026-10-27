using EnGlam.Core.Drops;
using EnGlam.Core.Duties;
using EnGlam.Core.Planning;

namespace EnGlam.Core.Tests;

public class PlannerTests
{
    private const uint Item = 500;

    private static DutyInfo Duty(uint cfc, byte level = 15, uint territory = 0) =>
        new(cfc, territory == 0 ? cfc + 1000 : territory, cfc, $"Dungeon {cfc}", 2, 0, level, 0, 0, -1);

    private static PlanContext Context(
        Party party = Party.DutyFinder,
        int level = 100,
        Func<uint, bool>? hasPath = null,
        Func<DutyInfo, bool>? unlocked = null) =>
        new(party, level, hasPath ?? (_ => true), unlocked ?? (_ => true));

    private static Planner Planner(DutyInfo[] duties, BossChestRow[]? bossChests = null, ChestRow[]? chests = null, ChestItemRow[]? chestItems = null)
    {
        var index = DropIndex.Build([], bossChests ?? [], [], chests ?? [], chestItems ?? []);
        return new Planner(index, duties.ToDictionary(d => d.ContentFinderConditionId));
    }

    [Fact]
    public void ItemWithoutSource_IsSkippedWithNoSource()
    {
        var result = Planner([Duty(1)]).Plan(Item, Context());

        Assert.False(result.CanFarm);
        Assert.Equal(SkipReason.NoSource, result.Reason);
    }

    [Fact]
    public void SingleRunnableSource_IsChosen()
    {
        var result = Planner([Duty(1)], bossChests: [new(1, Item)]).Plan(Item, Context());

        Assert.True(result.CanFarm);
        Assert.Equal(1u, result.Duty!.ContentFinderConditionId);
        Assert.Null(result.Reason);
    }

    [Fact]
    public void SourceWithoutDutyData_IsSkipped()
    {
        var result = Planner([Duty(1)], bossChests: [new(7, Item)]).Plan(Item, Context());

        Assert.Equal(SkipReason.NoDutyData, result.Reason);
    }

    [Fact]
    public void ModeNotAvailable_IsSkipped()
    {
        var result = Planner([Duty(1)], bossChests: [new(1, Item)]).Plan(Item, Context(party: Party.Trust));

        Assert.Equal(SkipReason.ModeNotAvailable, result.Reason);
    }

    [Fact]
    public void UnsyncedRegular_IsAllowedBeforeDawntrail()
    {
        var result = Planner([Duty(1)], bossChests: [new(1, Item)]).Plan(Item, Context() with { Unsynced = true });

        Assert.True(result.CanFarm);
    }

    [Fact]
    public void UnsyncedDawntrailDungeon_IsSkipped()
    {
        var dawntrail = Duty(1) with { ExVersion = 5 };

        var result = Planner([dawntrail], bossChests: [new(1, Item)]).Plan(Item, Context() with { Unsynced = true });

        Assert.Equal(SkipReason.UnsyncNotPossible, result.Reason);
    }

    [Fact]
    public void UnsyncedInNpcMode_IsSkipped()
    {
        var supportDungeon = Duty(1) with { ExVersion = 0, DawnRowId = 201, DawnParticipableCount = 2 };

        var result = Planner([supportDungeon], bossChests: [new(1, Item)])
            .Plan(Item, Context(party: Party.Support) with { Unsynced = true });

        Assert.Equal(SkipReason.UnsyncNotPossible, result.Reason);
    }

    [Fact]
    public void DutyFinder_UsesRegularForDungeons()
    {
        var result = Planner([Duty(1)], bossChests: [new(1, Item)]).Plan(Item, Context());

        Assert.Equal(DutyMode.Regular, result.Mode);
    }

    [Fact]
    public void DutyFinder_UsesTrialForTrials()
    {
        var trial = Duty(1) with { ContentType = 4 };

        var result = Planner([trial], bossChests: [new(1, Item)]).Plan(Item, Context());

        Assert.Equal(DutyMode.Trial, result.Mode);
    }

    [Fact]
    public void UnsyncedTrial_IsAllowed()
    {
        var trial = Duty(1) with { ContentType = 4 };

        var result = Planner([trial], bossChests: [new(1, Item)]).Plan(Item, Context() with { Unsynced = true });

        Assert.True(result.CanFarm);
        Assert.Equal(DutyMode.Trial, result.Mode);
    }

    [Fact]
    public void Support_ReturnsSupportMode()
    {
        var supportDungeon = Duty(1) with { DawnRowId = 201, DawnParticipableCount = 2 };

        var result = Planner([supportDungeon], bossChests: [new(1, Item)]).Plan(Item, Context(party: Party.Support));

        Assert.Equal(DutyMode.Support, result.Mode);
    }

    [Fact]
    public void MissingPath_IsSkipped()
    {
        var result = Planner([Duty(1)], bossChests: [new(1, Item)]).Plan(Item, Context(hasPath: _ => false));

        Assert.Equal(SkipReason.NoPath, result.Reason);
    }

    [Fact]
    public void LockedDungeon_IsSkipped()
    {
        var result = Planner([Duty(1)], bossChests: [new(1, Item)]).Plan(Item, Context(unlocked: _ => false));

        Assert.Equal(SkipReason.Locked, result.Reason);
    }

    [Fact]
    public void LevelTooLow_IsSkipped()
    {
        var result = Planner([Duty(1, level: 50)], bossChests: [new(1, Item)]).Plan(Item, Context(level: 49));

        Assert.Equal(SkipReason.LevelTooLow, result.Reason);
    }

    [Fact]
    public void FailingSources_ReportTheFurthestReason()
    {
        // Dungeon 1 ist gesperrt, Dungeon 2 hat keinen Pfad: gemeldet wird der gesperrte.
        var result = Planner([Duty(1), Duty(2)], bossChests: [new(1, Item), new(2, Item)])
            .Plan(Item, Context(hasPath: t => t != 1002, unlocked: d => d.ContentFinderConditionId != 1));

        Assert.Equal(SkipReason.Locked, result.Reason);
    }

    [Fact]
    public void HigherChance_Wins()
    {
        var result = Planner(
                [Duty(1), Duty(2)],
                chests: [new(10, 1), new(20, 2)],
                chestItems: [new(10, Item, 5m), new(20, Item, 15m)])
            .Plan(Item, Context());

        Assert.Equal(2u, result.Duty!.ContentFinderConditionId);
        Assert.Equal(15m, result.Chance);
    }

    [Fact]
    public void KnownChance_BeatsUnknownChance()
    {
        var result = Planner(
                [Duty(1, level: 10), Duty(2, level: 50)],
                bossChests: [new(1, Item)],
                chests: [new(20, 2)],
                chestItems: [new(20, Item, 1m)])
            .Plan(Item, Context());

        Assert.Equal(2u, result.Duty!.ContentFinderConditionId);
    }

    [Fact]
    public void SameChance_LowerLevelWins()
    {
        var result = Planner([Duty(1, level: 50), Duty(2, level: 20)], bossChests: [new(1, Item), new(2, Item)])
            .Plan(Item, Context());

        Assert.Equal(2u, result.Duty!.ContentFinderConditionId);
    }

    [Fact]
    public void BossAndChestInSameDungeon_UseBestChance()
    {
        var result = Planner([Duty(1)], bossChests: [new(1, Item)], chests: [new(10, 1)], chestItems: [new(10, Item, 7m)])
            .Plan(Item, Context());

        Assert.Equal(7m, result.Chance);
    }
}
