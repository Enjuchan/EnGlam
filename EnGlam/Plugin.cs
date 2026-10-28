using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using EnGlam.Core.Farming;
using EnGlam.Core.Planning;
using EnGlam.Core.Settings;
using EnGlam.External;
using EnGlam.Ui;

namespace EnGlam;

public sealed class Plugin : IDalamudPlugin
{
    private const string Command = "/englam";
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(500);

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
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
    private WelcomeWindow welcomeWindow = null!;
    private ChangelogWindow changelogWindow = null!;
    private readonly Welcome welcome;
    private readonly WindowSystem windows = new("EnGlam");
    private DateTime nextTick;

    public Plugin()
    {
        // ECommons braucht das für DalamudReflector, das der Dependencies-Tab zum Installieren nutzt.
        ECommons.ECommonsMain.Init(PluginInterface, this);

        // Null heißt: Dalamud hatte nie eine Konfiguration dieses Plugins, also eine Neuinstallation.
        var savedConfig = PluginInterface.GetPluginConfig();
        config = savedConfig as Configuration ?? new Configuration();
        Loc.Init(PluginInterface, config.Language);

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
        var version = PluginInterface.Manifest.AssemblyVersion.ToString(3);
        var dependencies = new Dependencies(PluginInterface, Log, EnGlamDependencies.Groups);
        window = new MainWindow(config, Save, drops, game, planner, env, controller, farmList, autoDuty, guard, rolling,
            version, TryOn, history,
            dependencies, EnGlamAbout.Create(version),
            () => welcomeWindow.IsOpen = true, () => changelogWindow.IsOpen = true, Log);
        windows.AddWindow(window);

        var fullVersion = PluginInterface.Manifest.AssemblyVersion.ToString();
        var saveLanguage = new Action<string>(code =>
        {
            config.Language = code;
            Save();
        });
        welcome = new Welcome(ClientState, fullVersion, savedConfig is not null, config.LastSeenVersion, seen =>
        {
            config.LastSeenVersion = seen;
            Save();
        });
        changelogWindow = new ChangelogWindow("EnGlam", EnGlamChangelog.Entries, welcome.IsNew, saveLanguage);
        welcomeWindow = new WelcomeWindow(EnGlamWelcome.Create(version, config, Save),
            dependencies,
            () => window.IsOpen = true, () => changelogWindow.IsOpen = true, saveLanguage);
        windows.AddWindow(welcomeWindow);
        windows.AddWindow(changelogWindow);

        Framework.Update += OnUpdate;
        PluginInterface.UiBuilder.Draw += Draw;
        PluginInterface.UiBuilder.OpenMainUi += Toggle;
        PluginInterface.UiBuilder.OpenConfigUi += Toggle;

        // Dalamud übernimmt den Hilfetext bei der Anmeldung, deshalb wird der Befehl beim Sprachwechsel neu angemeldet.
        RegisterCommand();
        Loc.Changed += RegisterCommand;

        welcome.Start(() => welcomeWindow.IsOpen = true, () => changelogWindow.IsOpen = true);

        Log.Information("EnGlam geladen.");
    }

    private void RegisterCommand()
    {
        Commands.RemoveHandler(Command);
        Commands.AddHandler(Command, new CommandInfo((_, _) => Toggle())
        {
            HelpMessage = EnGlamAbout.ToggleEffect.ToString(),
        });
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

    private void Draw()
    {
        // Im Ladebildschirm wird nichts gezeichnet. Visible bleibt unberührt, damit das Fenster danach von selbst wiederkommt.
        // Eine Prüfung auf den Login fehlt mit Absicht: Das Fenster ist zugleich das Einstellungsfenster und soll
        // vom Titelbildschirm aus über den Plugin-Installer erreichbar sein.
        if (Condition[ConditionFlag.BetweenAreas] || Condition[ConditionFlag.BetweenAreas51])
            return;

        windows.Draw();
    }

    private void Toggle() => window.Toggle();

    /// <summary>Öffnet die Anprobe des Spiels; im Framework-Takt, so wie Allagan Tools es macht.</summary>
    private static void TryOn(uint itemId) =>
        Framework.RunOnFrameworkThread(() => FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentTryon.TryOn(0, itemId));

    public void Dispose()
    {
        welcome.Dispose();
        Loc.Changed -= RegisterCommand;
        Loc.Dispose();
        Commands.RemoveHandler(Command);
        windows.RemoveAllWindows();
        Framework.Update -= OnUpdate;
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenMainUi -= Toggle;
        PluginInterface.UiBuilder.OpenConfigUi -= Toggle;

        controller.Stop("plugin unloaded");
        ECommons.ECommonsMain.Dispose();
    }
}
