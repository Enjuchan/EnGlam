using EnGlam.Core.Loot;

namespace EnGlam.Core.Tests;

public class LootRollerTests
{
    private const uint Wanted = 500;
    private const uint Other = 600;

    private readonly LootRoller roller = new();
    private readonly HashSet<uint> wanted = [Wanted];

    private static LootSlot Slot(int index, uint itemId, RollState state = RollState.UpToNeed, bool awarded = false) =>
        new(index, 1, (uint)index, itemId, state, awarded);

    [Fact]
    public void WantedItem_GetsNeed()
    {
        var decisions = roller.Decide([Slot(0, Wanted)], wanted, passOthers: true);

        Assert.Equal([new RollDecision(0, RollChoice.Need)], decisions);
    }

    [Fact]
    public void WantedItemInHq_IsRecognised()
    {
        var decisions = roller.Decide([Slot(0, Wanted + 1_000_000)], wanted, passOthers: true);

        Assert.Equal(RollChoice.Need, Assert.Single(decisions).Choice);
    }

    [Fact]
    public void WantedItemWithoutNeedRight_GetsGreed()
    {
        var decisions = roller.Decide([Slot(0, Wanted, RollState.UpToGreed)], wanted, passOthers: true);

        Assert.Equal(RollChoice.Greed, Assert.Single(decisions).Choice);
    }

    [Fact]
    public void WantedItemWithOnlyPass_IsLeftAlone()
    {
        Assert.Empty(roller.Decide([Slot(0, Wanted, RollState.UpToPass)], wanted, passOthers: true));
    }

    [Fact]
    public void OtherItem_IsPassedWhenEnabled()
    {
        var decisions = roller.Decide([Slot(0, Other)], wanted, passOthers: true);

        Assert.Equal(RollChoice.Pass, Assert.Single(decisions).Choice);
    }

    [Fact]
    public void OtherItem_IsLeftAloneWhenPassingDisabled()
    {
        Assert.Empty(roller.Decide([Slot(0, Other)], wanted, passOthers: false));
    }

    [Theory]
    [InlineData(RollState.Rolled)]
    [InlineData(RollState.Unavailable)]
    [InlineData(RollState.Unknown)]
    public void SlotsThatCannotBeRolled_AreIgnored(RollState state)
    {
        Assert.Empty(roller.Decide([Slot(0, Wanted, state)], wanted, passOthers: true));
    }

    [Fact]
    public void AwardedSlot_IsIgnored()
    {
        Assert.Empty(roller.Decide([Slot(0, Wanted, awarded: true)], wanted, passOthers: true));
    }

    [Fact]
    public void EmptySlot_IsIgnored()
    {
        Assert.Empty(roller.Decide([Slot(0, 0)], wanted, passOthers: true));
    }

    [Fact]
    public void SameSlot_IsDecidedOnlyOnce()
    {
        roller.Decide([Slot(0, Wanted)], wanted, passOthers: true);

        // Das Spiel hat den Wurf noch nicht übernommen; der Slot sieht unverändert aus.
        Assert.Empty(roller.Decide([Slot(0, Wanted)], wanted, passOthers: true));
    }

    [Fact]
    public void SlotThatLeftAndCameBack_IsDecidedAgain()
    {
        roller.Decide([Slot(0, Wanted)], wanted, passOthers: true);
        roller.Decide([], wanted, passOthers: true);

        Assert.Single(roller.Decide([Slot(0, Wanted)], wanted, passOthers: true));
    }

    [Fact]
    public void SeveralSlots_AreDecidedTogether()
    {
        var decisions = roller.Decide([Slot(0, Other), Slot(1, Wanted), Slot(2, Other)], wanted, passOthers: true);

        Assert.Equal(
            [new RollDecision(0, RollChoice.Pass), new RollDecision(1, RollChoice.Need), new RollDecision(2, RollChoice.Pass)],
            decisions);
    }
}
