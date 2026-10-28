using EnGlam.Core.Planning;

namespace EnGlam.Core.Farming;

public sealed record FarmOptions(int MaxRunsPerItem, int MaxRunsPerSession, bool Unsynced);

/// <summary>Art des Status, damit die Anzeige ihn in jeder Sprache formulieren kann.</summary>
public enum StatusCode
{
    Idle,
    Starting,
    Stopped,
    PrepareFailed,
    LoggedOut,
    AutoDutyUnloaded,
    JobChanged,
    Done,
    DoneManual,
    SessionLimit,
    StartFailed,
    StoppedBeforeEntry,
    StoppedInDuty,
    RunStarting,
    InRun,
}

/// <summary>
/// Arbeitet die Farm-Liste ab: pro Item planen, einzelne Runs über AutoDuty starten, nach jedem Run das Inventar
/// prüfen, bis das Item da ist oder ein Limit greift. Wird regelmäßig über <see cref="Tick"/> angetrieben.
/// </summary>
public sealed class FarmController(IFarmEnvironment env)
{
    /// <summary>So lange darf AutoDuty am Stück gestoppt sein, bevor ein Run als Fehlstart gilt.</summary>
    public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    /// <summary>Wartezeit nach dem Verlassen, damit zuletzt verteilte Beute im Inventar ankommt.</summary>
    public static readonly TimeSpan CheckDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// So lange darf AutoDuty im Inhalt am Stück gestoppt sein, bevor der Lauf abbricht, etwa nach Tod, Wipe
    /// oder wenn AutoDuty hängt. Die kurzen "gestoppt"-Meldungen während eines Runs dauern nur Sekunden.
    /// </summary>
    public static readonly TimeSpan StuckTimeout = TimeSpan.FromMinutes(3);

    private enum Phase { Idle, Prepare, NextItem, StartRun, Starting, InDuty, Checking }

    private Phase phase = Phase.Idle;
    private IReadOnlyList<FarmItem> items = [];
    private FarmOptions options = new(1, 1, false);
    private FarmItem? current;
    private uint dutyTerritory;
    private uint startJob;
    private DateTime? stoppedSince;
    private DateTime leftAt;

    public bool IsRunning => phase != Phase.Idle;

    public int SessionRuns { get; private set; }

    /// <summary>Englischer Text für Log und Tests; die Anzeige übersetzt über <see cref="Code"/>.</summary>
    public string Status { get; private set; } = "idle";

    public StatusCode Code { get; private set; } = StatusCode.Idle;

    /// <summary>Run-Nummer oder Zahl der Items zum Selbstfarmen, je nach <see cref="Code"/>.</summary>
    public int StatusCount { get; private set; }

    public string StatusItem { get; private set; } = string.Empty;

    public string StatusDuty { get; private set; } = string.Empty;

    private void SetStatus(string text, StatusCode code, int count = 0, string item = "", string duty = "")
    {
        Status = text;
        Code = code;
        StatusCount = count;
        StatusItem = item;
        StatusDuty = duty;
    }

    public void Start(IReadOnlyList<FarmItem> list, FarmOptions farmOptions)
    {
        if (IsRunning)
            return;

        items = list;
        options = farmOptions;
        SessionRuns = 0;
        startJob = env.ClassJobId;
        env.OnSessionStarted();
        phase = Phase.Prepare;
        SetStatus("starting", StatusCode.Starting);
    }

    /// <summary>Bricht ab, stoppt AutoDuty und setzt die Einstellungen zurück.</summary>
    public void Stop(string reason = "stopped") => Stop(reason, StatusCode.Stopped);

    private void Stop(string reason, StatusCode code)
    {
        if (!IsRunning)
            return;

        if (phase is Phase.Starting or Phase.InDuty)
            env.StopAutoDuty();

        if (current is { State: FarmItemState.Running })
            current.State = FarmItemState.Waiting;

        End(reason, code);
    }

    public void Tick()
    {
        if (phase != Phase.Idle && AbortReason() is { } abort)
        {
            Stop(abort.Text, abort.Code);
            return;
        }

        switch (phase)
        {
            case Phase.Prepare:
                if (!env.Prepare())
                {
                    End("AutoDuty could not be prepared", StatusCode.PrepareFailed);
                    break;
                }

                phase = Phase.NextItem;
                NextItem();
                break;
            case Phase.NextItem:
                NextItem();
                break;
            case Phase.StartRun:
                StartRun();
                break;
            case Phase.Starting:
                WaitForEntry();
                break;
            case Phase.InDuty:
                WaitForExit();
                break;
            case Phase.Checking:
                Check();
                break;
        }
    }

    /// <summary>Gründe, den Lauf geordnet abzubrechen; null, wenn alles in Ordnung ist.</summary>
    private (string Text, StatusCode Code)? AbortReason()
    {
        if (!env.IsLoggedIn)
            return ("logged out", StatusCode.LoggedOut);

        if (!env.AutoDutyLoaded)
            return ("AutoDuty was unloaded", StatusCode.AutoDutyUnloaded);

        // Stufe, Rotation und Planung hängen am Job; ein Wechsel mitten im Lauf ist kein Zustand, den EnGlam bedient.
        if (env.ClassJobId != startJob)
            return ("job changed", StatusCode.JobChanged);

        return null;
    }

    private void NextItem()
    {
        current = items.FirstOrDefault(i => i.State == FarmItemState.Waiting);
        if (current is null)
        {
            var manual = items.Count(i => i.State == FarmItemState.Manual);
            if (manual == 0)
                End("done", StatusCode.Done);
            else
                End($"done, {manual} {(manual == 1 ? "item" : "items")} left to farm yourself", StatusCode.DoneManual, manual);
            return;
        }

        if (env.CountItem(current.ItemId) > 0)
        {
            current.State = FarmItemState.AlreadyOwned;
            return;
        }

        var plan = env.Plan(current.ItemId);
        if (!plan.CanFarm)
        {
            MarkUnplannable(current, plan);
            return;
        }

        current.State = FarmItemState.Running;
        current.DutyName = plan.Duty!.Name;
        phase = Phase.StartRun;
    }

    private void StartRun()
    {
        var item = current!;

        if (SessionRuns >= options.MaxRunsPerSession)
        {
            item.State = FarmItemState.Waiting;
            End("session limit reached", StatusCode.SessionLimit);
            return;
        }

        if (item.Runs >= options.MaxRunsPerItem)
        {
            item.State = FarmItemState.LimitReached;
            phase = Phase.NextItem;
            return;
        }

        // Neu planen, weil sich Stufe oder Freischaltung seit dem letzten Run geändert haben können.
        var plan = env.Plan(item.ItemId);
        if (!plan.CanFarm)
        {
            MarkUnplannable(item, plan);
            phase = Phase.NextItem;
            return;
        }

        if (!env.StartRun(plan.Duty!, plan.Mode, options.Unsynced))
        {
            item.State = FarmItemState.Failed;
            End("AutoDuty could not start the run", StatusCode.StartFailed);
            return;
        }

        item.Runs++;
        item.DutyName = plan.Duty!.Name;
        SessionRuns++;
        env.OnRunStarted(item.ItemId);
        dutyTerritory = plan.Duty.TerritoryType;
        stoppedSince = null;
        phase = Phase.Starting;
        SetStatus($"{item.Name}: run {item.Runs} in {plan.Duty.Name}, starting", StatusCode.RunStarting, item.Runs, item.Name, plan.Duty.Name);
    }

    private void WaitForEntry()
    {
        if (env.CurrentTerritory == dutyTerritory)
        {
            phase = Phase.InDuty;
            stoppedSince = null;
            SetStatus($"{current!.Name}: run {current.Runs} in {current.DutyName}", StatusCode.InRun, current.Runs, current.Name, current.DutyName ?? string.Empty);
            return;
        }

        // In der Warteschlange läuft AutoDuty noch; nur ein dauerhaft gestopptes AutoDuty ist ein Fehlstart.
        var now = env.Now;
        stoppedSince = env.AutoDutyStopped ? stoppedSince ?? now : null;

        if (now - stoppedSince > StartTimeout)
        {
            current!.State = FarmItemState.Failed;
            End("AutoDuty stopped before entering the duty", StatusCode.StoppedBeforeEntry);
        }
    }

    private void WaitForExit()
    {
        var now = env.Now;
        var stopped = env.AutoDutyStopped;
        stoppedSince = stopped ? stoppedSince ?? now : null;

        // AutoDuty meldet zwischendurch kurz "gestoppt"; erst gestoppt und draußen heißt fertig.
        if (env.CurrentTerritory != dutyTerritory)
        {
            if (!stopped)
                return;

            leftAt = now;
            phase = Phase.Checking;
            return;
        }

        if (now - stoppedSince > StuckTimeout)
            Stop("AutoDuty stopped inside the duty", StatusCode.StoppedInDuty);
    }

    private void Check()
    {
        if (env.Now - leftAt < CheckDelay)
            return;

        var item = current!;
        if (env.CountItem(item.ItemId) > 0)
        {
            item.State = FarmItemState.Obtained;
            env.OnItemObtained(item.ItemId);
            phase = Phase.NextItem;
            return;
        }

        phase = Phase.StartRun;
    }

    /// <summary>Fehlt nur der AutoDuty-Pfad, bleibt das Item für Handläufe offen; sonst wird es übersprungen.</summary>
    private static void MarkUnplannable(FarmItem item, PlanResult plan)
    {
        item.State = plan.Reason == SkipReason.NoPath ? FarmItemState.Manual : FarmItemState.Skipped;
        item.SkipReason = plan.Reason;
    }

    private void End(string reason, StatusCode code, int count = 0)
    {
        phase = Phase.Idle;
        current = null;
        SetStatus(reason, code, count);
        env.RestoreSettings();
    }
}
