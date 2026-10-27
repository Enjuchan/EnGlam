using EnGlam.Core.Settings;

namespace EnGlam.Core.Tests;

public class SettingsGuardTests
{
    private readonly List<SavedSetting> store = [];
    private readonly Dictionary<string, string> foreign = new() { ["Mode"] = "Trust", ["Unsynced"] = "False" };
    private int saves;

    private SettingsGuard Guard() => new(store, () => saves++);

    private void Set(SettingsGuard guard, string key, string value) =>
        guard.Set("AutoDuty", key, value, () => foreign[key], v => foreign[key] = v);

    [Fact]
    public void Set_RemembersOriginalAndWritesNewValue()
    {
        var guard = Guard();

        Set(guard, "Mode", "Regular");

        Assert.Equal("Regular", foreign["Mode"]);
        Assert.Equal([new SavedSetting("AutoDuty", "Mode", "Trust")], store);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void SecondSet_KeepsFirstOriginal()
    {
        var guard = Guard();

        Set(guard, "Mode", "Regular");
        Set(guard, "Mode", "Support");

        Assert.Equal("Trust", Assert.Single(store).Value);
        Assert.Equal("Support", foreign["Mode"]);
    }

    [Fact]
    public void RestoreAll_WritesOriginalsBackAndClears()
    {
        var guard = Guard();
        Set(guard, "Mode", "Regular");
        Set(guard, "Unsynced", "True");

        var restored = guard.RestoreAll(s => { foreign[s.Key] = s.Value; return true; });

        Assert.Equal(2, restored);
        Assert.Equal("Trust", foreign["Mode"]);
        Assert.Equal("False", foreign["Unsynced"]);
        Assert.Empty(store);
    }

    [Fact]
    public void RestoreAll_GoesInReverseOrder()
    {
        var guard = Guard();
        Set(guard, "Unsynced", "True");
        Set(guard, "Mode", "Regular");
        var order = new List<string>();

        guard.RestoreAll(s => { order.Add(s.Key); return true; });

        Assert.Equal(["Mode", "Unsynced"], order);
    }

    [Fact]
    public void FailedRestore_KeepsEntryForNextAttempt()
    {
        var guard = Guard();
        Set(guard, "Mode", "Regular");
        Set(guard, "Unsynced", "True");

        var restored = guard.RestoreAll(s => s.Key == "Unsynced");

        Assert.Equal(1, restored);
        Assert.Equal("Mode", Assert.Single(store).Key);
    }

    [Fact]
    public void PendingEntries_SurviveANewGuardOnTheSameStore()
    {
        Set(Guard(), "Mode", "Regular");

        // Neuer Guard wie nach einem Neustart des Plugins.
        var afterRestart = Guard();

        Assert.Equal("Trust", Assert.Single(afterRestart.Pending).Value);
    }

    [Fact]
    public void RestoreAll_WithNothingPending_DoesNotSave()
    {
        var restored = Guard().RestoreAll(_ => true);

        Assert.Equal(0, restored);
        Assert.Equal(0, saves);
    }
}
