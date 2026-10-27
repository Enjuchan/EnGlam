using EnGlam.Core.Duties;
using EnGlam.Core.Farming;
using EnGlam.Core.Planning;

namespace EnGlam.Core.Tests;

public class FarmControllerTests
{
    private const uint Town = 1;
    private const uint DutyTerritory = 1036;

    private sealed class FakeEnv : IFarmEnvironment
    {
        public DateTime Now { get; set; } = new(2026, 10, 27, 12, 0, 0);
        public uint CurrentTerritory { get; set; } = Town;
        public bool AutoDutyStopped { get; set; } = true;
        public bool AutoDutyLoaded { get; set; } = true;
        public bool IsLoggedIn { get; set; } = true;
        public uint ClassJobId { get; set; } = 19;
        public Dictionary<uint, int> Counts { get; } = [];
        public Dictionary<uint, PlanResult> Plans { get; } = [];
        public bool StartSucceeds { get; set; } = true;
        public List<uint> Started { get; } = [];
        public int Stops { get; private set; }
        public int Restores { get; private set; }
        public bool PrepareSucceeds { get; set; } = true;
        public int Prepares { get; private set; }

        public int Sessions { get; private set; }
        public List<uint> RunsReported { get; } = [];
        public List<uint> Obtained { get; } = [];

        public void OnSessionStarted() => Sessions++;
        public void OnRunStarted(uint itemId) => RunsReported.Add(itemId);
        public void OnItemObtained(uint itemId) => Obtained.Add(itemId);

        public bool Prepare()
        {
            Prepares++;
            return PrepareSucceeds;
        }

        public PlanResult Plan(uint itemId) =>
            Plans.TryGetValue(itemId, out var plan) ? plan : new PlanResult(Duty, 10m, null, DutyMode.Regular);

        public int CountItem(uint itemId) => Counts.GetValueOrDefault(itemId);

        public bool StartRun(DutyInfo duty, DutyMode mode, bool unsynced)
        {
            if (!StartSucceeds)
                return false;

            Started.Add(duty.TerritoryType);
            AutoDutyStopped = false;
            return true;
        }

        public void StopAutoDuty() => Stops++;

        public void RestoreSettings() => Restores++;
    }

    private static readonly DutyInfo Duty = new(4, DutyTerritory, 1, "Sastasha", 2, 0, 15, 0, 0, -1);

    private readonly FakeEnv env = new();
    private readonly FarmController controller;

    public FarmControllerTests() => controller = new FarmController(env);

    private static FarmOptions Options(int perItem = 10, int perSession = 100) => new(perItem, perSession, false);

    private void Ticks(int count = 1)
    {
        for (var i = 0; i < count; i++)
            controller.Tick();
    }

    /// <summary>Startet einen Run und spielt ihn bis nach dem Verlassen und der Prüfwartezeit durch.</summary>
    private void PlayRun(uint itemId, bool dropsItem)
    {
        Ticks(2);                               // planen, Run starten
        env.CurrentTerritory = DutyTerritory;
        Ticks();                                // betreten
        env.CurrentTerritory = Town;
        env.AutoDutyStopped = true;
        Ticks();                                // verlassen
        if (dropsItem)
            env.Counts[itemId] = 1;
        env.Now += FarmController.CheckDelay;
        Ticks();                                // prüfen
    }

    [Fact]
    public void ItemDropsInFirstRun_IsObtainedAndFarmingEnds()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        PlayRun(100, dropsItem: true);
        Ticks();

        Assert.Equal(FarmItemState.Obtained, item.State);
        Assert.Equal(1, item.Runs);
        Assert.False(controller.IsRunning);
        Assert.Equal("done", controller.Status);
        Assert.Equal(1, env.Restores);
    }

    [Fact]
    public void ItemNotDropped_StartsAnotherRun()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        PlayRun(100, dropsItem: false);
        Ticks();

        Assert.Equal(2, env.Started.Count);
        Assert.Equal(2, item.Runs);
        Assert.Equal(FarmItemState.Running, item.State);
    }

    [Fact]
    public void ItemAlreadyOwned_IsNotFarmed()
    {
        env.Counts[100] = 1;
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        Ticks(2);

        Assert.Equal(FarmItemState.AlreadyOwned, item.State);
        Assert.Empty(env.Started);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void UnplannableItem_IsSkippedWithReason()
    {
        env.Plans[100] = new PlanResult(null, null, SkipReason.Locked);
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        Ticks(2);

        Assert.Equal(FarmItemState.Skipped, item.State);
        Assert.Equal(SkipReason.Locked, item.SkipReason);
        Assert.Empty(env.Started);
    }

    [Fact]
    public void ItemWithoutPath_IsLeftToFarmYourself()
    {
        env.Plans[100] = new PlanResult(null, null, SkipReason.NoPath);
        var item = new FarmItem(100, "Edengrace Helm");
        controller.Start([item], Options());

        Ticks(3);

        Assert.Equal(FarmItemState.Manual, item.State);
        Assert.Equal(SkipReason.NoPath, item.SkipReason);
        Assert.Empty(env.Started);
        Assert.False(controller.IsRunning);
        Assert.Equal("done, 1 item left to farm yourself", controller.Status);
    }

    [Fact]
    public void ItemWithoutPath_DoesNotBlockTheNextItem()
    {
        env.Plans[100] = new PlanResult(null, null, SkipReason.NoPath);
        var manual = new FarmItem(100, "Edengrace Helm");
        var next = new FarmItem(200, "Gloves");
        controller.Start([manual, next], Options());

        Ticks(3);

        Assert.Equal(FarmItemState.Manual, manual.State);
        Assert.Equal(FarmItemState.Running, next.State);
        Assert.Single(env.Started);
    }

    [Fact]
    public void PathLostBeforeNextRun_IsLeftToFarmYourself()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());
        PlayRun(100, dropsItem: false);

        env.Plans[100] = new PlanResult(null, null, SkipReason.NoPath);
        Ticks(2);

        Assert.Equal(FarmItemState.Manual, item.State);
    }

    [Fact]
    public void AllDone_StatusWithoutManualItems_IsDone()
    {
        env.Counts[100] = 1;
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        Ticks(3);

        Assert.Equal("done", controller.Status);
    }

    [Fact]
    public void PerItemLimit_MovesOnToNextItem()
    {
        var first = new FarmItem(100, "Sabatons");
        var second = new FarmItem(200, "Gloves");
        controller.Start([first, second], Options(perItem: 1));

        PlayRun(100, dropsItem: false);
        Ticks();                                // Limit erreicht
        Ticks(2);                               // zweites Item planen und starten

        Assert.Equal(FarmItemState.LimitReached, first.State);
        Assert.Equal(FarmItemState.Running, second.State);
        Assert.Equal(2, env.Started.Count);
    }

    [Fact]
    public void SessionLimit_EndsFarmingAndRestores()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options(perSession: 1));

        PlayRun(100, dropsItem: false);
        Ticks();

        Assert.False(controller.IsRunning);
        Assert.Equal("session limit reached", controller.Status);
        Assert.Equal(FarmItemState.Waiting, item.State);
        Assert.Equal(1, env.Restores);
    }

    [Fact]
    public void FailedStart_EndsWithFailure()
    {
        env.StartSucceeds = false;
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        Ticks(2);

        Assert.Equal(FarmItemState.Failed, item.State);
        Assert.False(controller.IsRunning);
        Assert.Equal(1, env.Restores);
    }

    [Fact]
    public void AutoDutyStoppedTooLongBeforeEntering_IsAFailedStart()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());
        Ticks(2);

        env.AutoDutyStopped = true;
        Ticks();
        env.Now += FarmController.StartTimeout + TimeSpan.FromSeconds(1);
        Ticks();

        Assert.Equal(FarmItemState.Failed, item.State);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void WaitingInQueue_IsNotAFailedStart()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());
        Ticks(2);

        env.Now += TimeSpan.FromMinutes(10);   // AutoDuty läuft, Spieler noch nicht drin
        Ticks();

        Assert.True(controller.IsRunning);
        Assert.Equal(FarmItemState.Running, item.State);
    }

    [Fact]
    public void BriefStopInsideDuty_DoesNotEndTheRun()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());
        Ticks(2);
        env.CurrentTerritory = DutyTerritory;
        Ticks();

        env.AutoDutyStopped = true;            // kurzes "gestoppt" im Inhalt
        env.Now += TimeSpan.FromMinutes(1);
        Ticks(3);

        Assert.Single(env.Started);
        Assert.StartsWith("Sabatons: run 1 in Sastasha", controller.Status);
    }

    [Fact]
    public void Check_WaitsForLootDelay()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());
        Ticks(2);
        env.CurrentTerritory = DutyTerritory;
        Ticks();
        env.CurrentTerritory = Town;
        env.AutoDutyStopped = true;
        Ticks();

        Ticks();                                // noch innerhalb der Wartezeit
        env.Counts[100] = 1;                    // Beute kommt verspätet an
        env.Now += FarmController.CheckDelay;
        Ticks();

        Assert.Equal(FarmItemState.Obtained, item.State);
        Assert.Single(env.Started);
    }

    [Fact]
    public void StopDuringRun_StopsAutoDutyAndRestores()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());
        Ticks(2);
        env.CurrentTerritory = DutyTerritory;
        Ticks();

        controller.Stop();

        Assert.Equal(1, env.Stops);
        Assert.Equal(1, env.Restores);
        Assert.Equal(FarmItemState.Waiting, item.State);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public void StopWhenIdle_DoesNothing()
    {
        controller.Stop();

        Assert.Equal(0, env.Stops);
        Assert.Equal(0, env.Restores);
    }

    [Fact]
    public void FailedPrepare_EndsWithoutRun()
    {
        env.PrepareSucceeds = false;
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        Ticks(2);

        Assert.Empty(env.Started);
        Assert.False(controller.IsRunning);
        Assert.Equal("AutoDuty could not be prepared", controller.Status);
        Assert.Equal(1, env.Restores);
    }

    [Fact]
    public void Prepare_RunsOnlyOncePerSession()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        PlayRun(100, dropsItem: false);
        PlayRun(100, dropsItem: false);

        Assert.Equal(1, env.Prepares);
    }

    /// <summary>Startet und bringt den Lauf in den Inhalt.</summary>
    private FarmItem StartAndEnter()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());
        Ticks(2);
        env.CurrentTerritory = DutyTerritory;
        Ticks();
        return item;
    }

    [Fact]
    public void Logout_StopsRun()
    {
        var item = StartAndEnter();

        env.IsLoggedIn = false;
        Ticks();

        Assert.False(controller.IsRunning);
        Assert.Equal("logged out", controller.Status);
        Assert.Equal(1, env.Restores);
        Assert.Equal(FarmItemState.Waiting, item.State);
    }

    [Fact]
    public void AutoDutyUnloaded_StopsRun()
    {
        StartAndEnter();

        env.AutoDutyLoaded = false;
        Ticks();

        Assert.False(controller.IsRunning);
        Assert.Equal("AutoDuty was unloaded", controller.Status);
    }

    [Fact]
    public void JobChange_StopsRun()
    {
        StartAndEnter();

        env.ClassJobId = 24;
        Ticks();

        Assert.False(controller.IsRunning);
        Assert.Equal("job changed", controller.Status);
        Assert.Equal(1, env.Stops);
    }

    [Fact]
    public void AutoDutyStoppedTooLongInsideDuty_StopsRun()
    {
        StartAndEnter();

        env.AutoDutyStopped = true;
        Ticks();
        env.Now += FarmController.StuckTimeout + TimeSpan.FromSeconds(1);
        Ticks();

        Assert.False(controller.IsRunning);
        Assert.Equal("AutoDuty stopped inside the duty", controller.Status);
        Assert.Equal(1, env.Restores);
    }

    [Fact]
    public void AutoDutyRunningAgainInsideDuty_ResetsStuckTimer()
    {
        StartAndEnter();

        env.AutoDutyStopped = true;
        Ticks();
        env.Now += TimeSpan.FromMinutes(2);
        env.AutoDutyStopped = false;           // läuft wieder, etwa nach dem Wiederbeleben
        Ticks();
        env.AutoDutyStopped = true;
        env.Now += TimeSpan.FromMinutes(2);
        Ticks();

        Assert.True(controller.IsRunning);
    }

    [Fact]
    public void HistoryEvents_AreReported()
    {
        var item = new FarmItem(100, "Sabatons");
        controller.Start([item], Options());

        PlayRun(100, dropsItem: false);
        PlayRun(100, dropsItem: true);

        Assert.Equal(1, env.Sessions);
        Assert.Equal([100u, 100u], env.RunsReported);
        Assert.Equal([100u], env.Obtained);
    }

    [Fact]
    public void AlreadyOwnedItem_IsNotReportedAsObtained()
    {
        env.Counts[100] = 1;
        controller.Start([new FarmItem(100, "Sabatons")], Options());

        Ticks(2);

        Assert.Empty(env.RunsReported);
        Assert.Empty(env.Obtained);
    }

    [Fact]
    public void ItemsAreFarmedInOrder()
    {
        var first = new FarmItem(100, "Sabatons");
        var second = new FarmItem(200, "Gloves");
        controller.Start([first, second], Options());

        PlayRun(100, dropsItem: true);
        PlayRun(200, dropsItem: true);
        Ticks();

        Assert.Equal(FarmItemState.Obtained, first.State);
        Assert.Equal(FarmItemState.Obtained, second.State);
        Assert.Equal("done", controller.Status);
    }
}
