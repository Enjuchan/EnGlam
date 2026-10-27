using EnGlam.Core.Farming;

namespace EnGlam.Core.Tests;

public class ManualTrackerTests
{
    private const uint Town = 1;
    private const uint Savage = 900;
    private const uint OtherDuty = 901;
    private const uint Helm = 100;
    private const uint Coat = 200;

    private readonly Dictionary<uint, uint[]> sources = new() { [Helm] = [Savage], [Coat] = [Savage, OtherDuty] };
    private readonly Dictionary<uint, int> counts = [];
    private readonly List<uint> runs = [];
    private readonly List<uint> obtained = [];
    private readonly ManualTracker tracker;
    private DateTime now = new(2026, 10, 27, 20, 0, 0);

    public ManualTrackerTests() => tracker = new ManualTracker(
        id => sources.GetValueOrDefault(id, []),
        id => counts.GetValueOrDefault(id),
        runs.Add,
        obtained.Add);

    private void Tick(uint territory, IReadOnlyList<FarmItem> list, bool farming = false, double seconds = 1)
    {
        now = now.AddSeconds(seconds);
        tracker.Tick(now, territory, farming, list);
    }

    private void RunSavage(IReadOnlyList<FarmItem> list)
    {
        Tick(Town, list);
        Tick(Savage, list);
        Tick(Savage, list, seconds: 600);
        Tick(Town, list);
    }

    [Fact]
    public void LeavingSourceDuty_CountsRunForEachItemThatDropsThere()
    {
        var list = new[] { new FarmItem(Helm, "Helm"), new FarmItem(Coat, "Coat") };

        RunSavage(list);

        Assert.Equal([Helm, Coat], runs);
    }

    [Fact]
    public void InsideSourceDuty_ReportsTrackedItems()
    {
        var list = new[] { new FarmItem(Helm, "Helm"), new FarmItem(Coat, "Coat") };

        Tick(OtherDuty, list);

        Assert.True(tracker.InTrackedDuty);
        Assert.Equal([Coat], tracker.Tracked.Select(i => i.ItemId));
    }

    [Fact]
    public void ItemInInventoryAfterDelay_IsObtained()
    {
        var helm = new FarmItem(Helm, "Helm");
        var list = new[] { helm };
        RunSavage(list);

        counts[Helm] = 1;
        Tick(Town, list, seconds: FarmController.CheckDelay.TotalSeconds);

        Assert.Equal(FarmItemState.Obtained, helm.State);
        Assert.Equal([Helm], obtained);
    }

    [Fact]
    public void InventoryIsNotCheckedBeforeDelay()
    {
        var helm = new FarmItem(Helm, "Helm");
        var list = new[] { helm };
        RunSavage(list);

        counts[Helm] = 1;
        Tick(Town, list, seconds: 1);

        Assert.Equal(FarmItemState.Waiting, helm.State);
        Assert.Empty(obtained);
    }

    [Fact]
    public void ItemNotDropped_StaysOpen()
    {
        var helm = new FarmItem(Helm, "Helm");
        var list = new[] { helm };
        RunSavage(list);

        Tick(Town, list, seconds: 10);

        Assert.Equal(FarmItemState.Waiting, helm.State);
        Assert.Empty(obtained);
        Assert.False(tracker.InTrackedDuty);
    }

    [Fact]
    public void DutyWithoutListItem_IsIgnored()
    {
        var list = new[] { new FarmItem(Helm, "Helm") };

        Tick(OtherDuty, list);
        Tick(Town, list);

        Assert.False(tracker.InTrackedDuty);
        Assert.Empty(runs);
    }

    [Fact]
    public void WhileFarming_NothingIsCounted()
    {
        var list = new[] { new FarmItem(Helm, "Helm") };

        Tick(Savage, list, farming: true);
        Tick(Town, list, farming: true);

        Assert.False(tracker.InTrackedDuty);
        Assert.Empty(runs);
    }

    [Fact]
    public void FarmingStartsInsideDuty_DropsTrackedRun()
    {
        var list = new[] { new FarmItem(Helm, "Helm") };

        Tick(Savage, list);
        Tick(Savage, list, farming: true);
        Tick(Town, list);

        Assert.Empty(runs);
    }

    [Fact]
    public void ItemAlreadyOwnedAtEntry_IsNotTracked()
    {
        counts[Helm] = 1;
        var list = new[] { new FarmItem(Helm, "Helm") };

        RunSavage(list);

        Assert.Empty(runs);
    }

    [Fact]
    public void FinishedItems_AreNotTracked()
    {
        var helm = new FarmItem(Helm, "Helm") { State = FarmItemState.Obtained };
        var coat = new FarmItem(Coat, "Coat") { State = FarmItemState.AlreadyOwned };

        RunSavage([helm, coat]);

        Assert.Empty(runs);
    }

    [Fact]
    public void ManualAndSkippedItems_AreTracked()
    {
        var helm = new FarmItem(Helm, "Helm") { State = FarmItemState.Manual };
        var coat = new FarmItem(Coat, "Coat") { State = FarmItemState.Skipped };

        RunSavage([helm, coat]);

        Assert.Equal([Helm, Coat], runs);
    }
}
