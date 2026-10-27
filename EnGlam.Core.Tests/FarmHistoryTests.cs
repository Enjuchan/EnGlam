using EnGlam.Core.History;

namespace EnGlam.Core.Tests;

public class FarmHistoryTests
{
    private static readonly DateTime Day1 = new(2026, 10, 27, 12, 0, 0);

    private readonly List<HistoryEntry> store = [];
    private int saves;

    private FarmHistory History() => new(store, () => saves++);

    [Fact]
    public void FirstRun_CreatesOpenEntry()
    {
        var history = History();
        history.BeginSession();

        history.RecordRun(100, Day1);

        var entry = Assert.Single(history.Entries);
        Assert.Equal(1, entry.Runs);
        Assert.Equal(1, entry.Sessions);
        Assert.Equal(HistoryState.Open, entry.State);
        Assert.Equal(Day1, entry.FirstFarmed);
        Assert.Equal(1, saves);
    }

    [Fact]
    public void SeveralRunsInOneSession_CountAsOneSession()
    {
        var history = History();
        history.BeginSession();

        history.RecordRun(100, Day1);
        history.RecordRun(100, Day1.AddMinutes(1));
        history.RecordRun(100, Day1.AddMinutes(2));

        var entry = Assert.Single(history.Entries);
        Assert.Equal(3, entry.Runs);
        Assert.Equal(1, entry.Sessions);
        Assert.Equal(Day1.AddMinutes(2), entry.LastFarmed);
    }

    [Fact]
    public void NewSession_CountsAgain()
    {
        var history = History();
        history.BeginSession();
        history.RecordRun(100, Day1);

        history.BeginSession();
        history.RecordRun(100, Day1.AddDays(1));

        var entry = Assert.Single(history.Entries);
        Assert.Equal(2, entry.Sessions);
        Assert.Equal(Day1, entry.FirstFarmed);
    }

    [Fact]
    public void Obtained_IsRecordedWithDate()
    {
        var history = History();
        history.BeginSession();
        history.RecordRun(100, Day1);

        history.RecordObtained(100, Day1.AddMinutes(5));

        var entry = Assert.Single(history.Entries);
        Assert.Equal(HistoryState.Obtained, entry.State);
        Assert.Equal(Day1.AddMinutes(5), entry.ObtainedAt);
    }

    [Fact]
    public void Obtained_StaysWhenFarmedAgain()
    {
        var history = History();
        history.BeginSession();
        history.RecordRun(100, Day1);
        history.RecordObtained(100, Day1);

        history.BeginSession();
        history.RecordRun(100, Day1.AddDays(2));
        history.RecordObtained(100, Day1.AddDays(2));

        var entry = Assert.Single(history.Entries);
        Assert.Equal(HistoryState.Obtained, entry.State);
        Assert.Equal(Day1, entry.ObtainedAt);
        Assert.Equal(2, entry.Runs);
    }

    [Fact]
    public void Entries_AreOrderedByLastFarmed()
    {
        var history = History();
        history.BeginSession();
        history.RecordRun(100, Day1);
        history.RecordRun(200, Day1.AddHours(1));

        Assert.Equal([200u, 100u], history.Entries.Select(e => e.ItemId));
    }

    [Fact]
    public void Remove_DeletesEntry()
    {
        var history = History();
        history.BeginSession();
        history.RecordRun(100, Day1);

        history.Remove(100);

        Assert.Empty(history.Entries);
    }

    [Fact]
    public void Entries_SurviveANewHistoryOnTheSameStore()
    {
        var first = History();
        first.BeginSession();
        first.RecordRun(100, Day1);

        Assert.Single(History().Entries);
    }
}
