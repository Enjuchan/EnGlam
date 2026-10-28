using EnGlam.Core.Duties;
using EnGlam.Core.Farming;
using EnGlam.Core.Planning;

namespace EnGlam.Core.Tests;

public class FarmStatusCodeTests
{
    private sealed class Env : IFarmEnvironment
    {
        public DateTime Now { get; set; } = new(2026, 10, 28, 12, 0, 0);
        public uint CurrentTerritory { get; set; } = 1;
        public bool AutoDutyStopped { get; set; } = true;
        public bool AutoDutyLoaded { get; set; } = true;
        public bool IsLoggedIn { get; set; } = true;
        public uint ClassJobId { get; set; } = 19;
        public bool PrepareSucceeds { get; set; } = true;
        public PlanResult? Plan { get; set; }

        public void OnSessionStarted() { }
        public void OnRunStarted(uint itemId) { }
        public void OnItemObtained(uint itemId) { }
        public bool Prepare() => PrepareSucceeds;
        PlanResult IFarmEnvironment.Plan(uint itemId) => Plan ?? new PlanResult(Duty, 10m, null, DutyMode.Regular);
        public int CountItem(uint itemId) => 0;
        public bool StartRun(DutyInfo duty, DutyMode mode, bool unsynced) => true;
        public void StopAutoDuty() { }
        public void RestoreSettings() { }
    }

    private static readonly DutyInfo Duty = new(4, 1036, 1, "Sastasha", 2, 0, 15, 0, 0, -1);

    private readonly Env env = new();
    private readonly FarmController controller;

    public FarmStatusCodeTests() => controller = new FarmController(env);

    private static FarmOptions Options() => new(10, 100, false);

    [Fact]
    public void BeforeStart_IsIdle()
    {
        Assert.Equal(StatusCode.Idle, controller.Code);
    }

    [Fact]
    public void RunStarted_CarriesItemRunAndDuty()
    {
        controller.Start([new FarmItem(100, "Sabatons")], Options());
        controller.Tick();
        controller.Tick();

        Assert.Equal(StatusCode.RunStarting, controller.Code);
        Assert.Equal(1, controller.StatusCount);
        Assert.Equal("Sabatons", controller.StatusItem);
        Assert.Equal("Sastasha", controller.StatusDuty);

        env.CurrentTerritory = 1036;
        controller.Tick();

        Assert.Equal(StatusCode.InRun, controller.Code);
    }

    [Fact]
    public void PrepareFails_HasPrepareFailedCode()
    {
        env.PrepareSucceeds = false;
        controller.Start([new FarmItem(100, "Sabatons")], Options());
        controller.Tick();

        Assert.Equal(StatusCode.PrepareFailed, controller.Code);
    }

    [Theory]
    [InlineData(1, "done, 1 item left to farm yourself")]
    [InlineData(2, "done, 2 items left to farm yourself")]
    public void ManualItemsLeft_CarryTheirCount(int count, string text)
    {
        env.Plan = new PlanResult(null, null, SkipReason.NoPath, DutyMode.None);
        controller.Start([.. Enumerable.Range(0, count).Select(i => new FarmItem((uint)(100 + i), "Item"))], Options());
        controller.Tick();
        controller.Tick();
        for (var i = 0; i < count; i++)
            controller.Tick();

        Assert.Equal(StatusCode.DoneManual, controller.Code);
        Assert.Equal(count, controller.StatusCount);
        Assert.Equal(text, controller.Status);
    }

    [Fact]
    public void LoggedOutDuringRun_AbortsWithLoggedOutCode()
    {
        controller.Start([new FarmItem(100, "Sabatons")], Options());
        env.IsLoggedIn = false;
        controller.Tick();

        Assert.Equal(StatusCode.LoggedOut, controller.Code);
        Assert.Equal("logged out", controller.Status);
    }

    [Fact]
    public void ManualStop_HasStoppedCode()
    {
        controller.Start([new FarmItem(100, "Sabatons")], Options());
        controller.Stop();

        Assert.Equal(StatusCode.Stopped, controller.Code);
    }
}
