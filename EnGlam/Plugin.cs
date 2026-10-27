using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using EnGlam.Core.Farming;
using EnGlam.Core.Planning;
using EnGlam.Core.Settings;
using EnGlam.External;

namespace EnGlam;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/englam";
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(500);

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ISigScanner SigScanner { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;

    private readonly Configuration config;
    private readonly LootRolling rolling;
    private readonly List<FarmItem> farmList;
    private readonly AutoDutyIpc autoDuty;
    private readonly SettingsGuard guard;
    private readonly FarmEnvironment env;
    private readonly FarmController controller;
    private readonly ManualTracker manual;
    private readonly MainWindow window;
    private DateTime nextTick;

    public Plugin()
    {
        // ECommons braucht das für DalamudReflector, das der Dependencies-Tab zum Installieren nutzt.
        ECommons.ECommonsMain.Init(PluginInterface, this);

        config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var drops = DropTables.Load(Log).ToIndex();
        var game = GameData.Load(Data);
        Log.Information("Drop-Index: {Items} Items mit Dungeon-Quelle, {Duties} Inhalte, {Equip} anlegbare Items.",
            drops.ItemCount, game.Duties.Count, game.EquipableItems.Count);

        autoDuty = new AutoDutyIpc(PluginInterface, Log);
        guard = new SettingsGuard(config.PendingRestore, Save);

        if (guard.Pending.Count > 0)
            Log.Warning("{Count} fremde Einstellungen vom letzten Lauf offen; werden zurückgesetzt, sobald AutoDuty erreichbar ist.", guard.Pending.Count);

        var planner = new Planner(drops, game.Duties);
        var history = new Core.History.FarmHistory(config.History, Save);
        env = new FarmEnvironment(config, planner, autoDuty, guard, history, PluginInterface, ClientState, PlayerState, Log);
        controller = new FarmController(env);

        rolling = new LootRolling(SigScanner, Log);

        farmList = config.FarmList.Select(id => new FarmItem(id, game.ItemName(id))).ToList();

        manual = new ManualTracker(
            id => drops.SourcesFor(id)
                .Select(s => game.Duties.GetValueOrDefault(s.ContentFinderConditionId))
                .Where(d => d is not null)
                .Select(d => d!.TerritoryType),
            env.CountItem,
            id =>
            {
                history.RecordRun(id, DateTime.Now);
                Log.Information("Handlauf gezählt: {Item}", game.ItemName(id));
            },
            id =>
            {
                history.RecordObtained(id, DateTime.Now);
                Log.Information("Im Handlauf erhalten: {Item}", game.ItemName(id));
            });
        Ui.Widgets.Init(Textures);
        window = new MainWindow(config, Save, drops, game, planner, env, controller, farmList, autoDuty, guard, rolling,
            PluginInterface.Manifest.AssemblyVersion.ToString(3), TryOn, history, new Dependencies(PluginInterface, Log), Log);

        Framework.Update += OnUpdate;
        PluginInterface.UiBuilder.Draw += window.Draw;
        PluginInterface.UiBuilder.OpenMainUi += Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += Toggle;

        Commands.AddHandler(Command, new CommandInfo((_, _) => Toggle())
        {
            HelpMessage = "Opens or closes the EnGlam window.",
        });

        Log.Information("EnGlam geladen.");
    }

    private void Save() => PluginInterface.SavePluginConfig(config);

    private void OnUpdate(IFramework framework)
    {
        var now = DateTime.UtcNow;
        if (now < nextTick)
            return;

        nextTick = now + TickInterval;

        if (controller.IsRunning)
        {
            // Gewürfelt wird nur beim Farmen; auf alle Items der Liste, die noch fehlen.
            var wanted = farmList.Where(i => !i.IsDone).Select(i => i.ItemId).ToHashSet();
            rolling.Update(wanted, config.PassOthers);

            controller.Tick();
            manual.Tick(DateTime.Now, env.CurrentTerritory, farming: true, farmList);
        }
        else
        {
            // Beim Ausloggen fällt das Gebiet auf 0; das wäre sonst ein verlassener Run.
            if (env.IsLoggedIn)
                manual.Tick(DateTime.Now, env.CurrentTerritory, farming: false, farmList);

            // In Handläufen nur Need auf Listen-Items; den Rest entscheidet der Spieler selbst.
            if (manual.InTrackedDuty)
                rolling.Update(manual.Tracked.Select(i => i.ItemId).ToHashSet(), passOthers: false);

            // Offene Einstellungen nach einem Absturz oder einem abgebrochenen Lauf.
            if (guard.Pending.Count > 0)
                env.RestoreSettings();
        }

        // Auch Wechsel durch Start und Stopp im Fenster landen so im Log, nicht nur die aus dem Ablauf.
        if (controller.Status != loggedStatus)
        {
            loggedStatus = controller.Status;
            Log.Information("Ablauf: {Status}", loggedStatus);
        }
    }

    private string loggedStatus = "idle";

    private void Toggle() => window.Visible = !window.Visible;

    /// <summary>Öffnet die Anprobe des Spiels; im Framework-Takt, so wie Allagan Tools es macht.</summary>
    private static void TryOn(uint itemId) =>
        Framework.RunOnFrameworkThread(() => FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentTryon.TryOn(0, itemId));

    public void Dispose()
    {
        Commands.RemoveHandler(Command);
        Framework.Update -= OnUpdate;
        PluginInterface.UiBuilder.Draw -= window.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= Toggle;

        controller.Stop("plugin unloaded");
        ECommons.ECommonsMain.Dispose();
    }
}
