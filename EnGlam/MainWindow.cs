using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using EnGlam.Core.Drops;
using EnGlam.Core.Duties;
using EnGlam.Core.Farming;
using EnGlam.Core.History;
using EnGlam.Core.Import;
using EnGlam.Core.Planning;
using EnGlam.Core.Settings;
using EnGlam.External;
using EnGlam.Ui;

namespace EnGlam;

public sealed class MainWindow : Window
{
    private readonly Configuration config;
    private readonly Action save;
    private readonly DropIndex drops;
    private readonly GameData game;
    private readonly Planner planner;
    private readonly FarmEnvironment env;
    private readonly FarmController controller;
    private readonly List<FarmItem> farmList;
    private readonly AutoDutyIpc autoDuty;
    private readonly SettingsGuard guard;
    private readonly LootRolling rolling;
    private readonly string version;
    private readonly Action<uint> tryOn;
    private readonly FarmHistory history;
    private readonly Dependencies dependencies;
    private readonly AboutInfo about;
    private readonly Action openWelcome;
    private readonly Action openChangelog;
    private readonly FrameTiming timing;

    private Theme.Scope? themeScope;
    private string search = string.Empty;
    private string? resultsFor;
    private List<(uint Id, string Name, uint Icon, int Duties)> results = [];
    private uint selectedItem;

    public MainWindow(
        Configuration config,
        Action save,
        DropIndex drops,
        GameData game,
        Planner planner,
        FarmEnvironment env,
        FarmController controller,
        List<FarmItem> farmList,
        AutoDutyIpc autoDuty,
        SettingsGuard guard,
        LootRolling rolling,
        string version,
        Action<uint> tryOn,
        FarmHistory history,
        Dependencies dependencies,
        AboutInfo about,
        Action openWelcome,
        Action openChangelog,
        Dalamud.Plugin.Services.IPluginLog log)
        : base("EnGlam###EnGlamMain")
    {
        this.config = config;
        this.save = save;
        this.drops = drops;
        this.game = game;
        this.planner = planner;
        this.env = env;
        this.controller = controller;
        this.farmList = farmList;
        this.autoDuty = autoDuty;
        this.guard = guard;
        this.rolling = rolling;
        this.version = version;
        this.tryOn = tryOn;
        this.history = history;
        this.dependencies = dependencies;
        this.about = about;
        this.openWelcome = openWelcome;
        this.openChangelog = openChangelog;
        timing = new FrameTiming(log, "Draw", TimeSpan.FromSeconds(5), () => config.ShowDiagnostics);

        Size = new Vector2(960, 660);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(780, 480), MaximumSize = new Vector2(float.MaxValue, float.MaxValue) };

        // Das Fenster war bisher ein einfaches ImGui-Fenster ohne Escape-Schließen, Töne und Einblenden.
        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        DisableFadeInFadeOut = true;
    }

    // Theme muss vor dem Begin des Fensters gesetzt sein, weil Fensterhintergrund und Titelleiste davon abhängen.
    public override void PreDraw() => themeScope = Theme.Push();

    public override void PostDraw()
    {
        themeScope?.Dispose();
        themeScope = null;
    }

    public override void Draw()
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        DrawContent();
        timing.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started));
    }

    private void DrawContent()
    {
        Decor.Background();
        Decor.HeaderBand(ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetTextLineHeight());
        DrawHeader();
        ImGui.Dummy(new Vector2(0, 6));

        Widgets.PillTabs("tabs", Tabs(), ref tab);
        ImGui.Dummy(new Vector2(0, 8));

        switch ((Tab)tab)
        {
            case Tab.Farm:
                ImGui.BeginChild("left", new Vector2(250, 0));
                DrawSearch();
                ImGui.EndChild();

                ImGui.SameLine(0, 14);
                ImGui.BeginChild("right", new Vector2(0, 0));
                DrawFarmList();
                DrawSources();
                ImGui.EndChild();
                break;

            case Tab.History:
                ImGui.BeginChild("history", new Vector2(0, 0));
                DrawHistory();
                ImGui.EndChild();
                break;

            case Tab.Settings:
                ImGui.BeginChild("settings", new Vector2(0, 0));
                DrawSettings();
                ImGui.EndChild();
                break;

            case Tab.Dependencies:
                ImGui.BeginChild("dependencies", new Vector2(0, 0));
                DependenciesView.Draw(dependencies);
                ImGui.EndChild();
                break;

            case Tab.About:
                ImGui.BeginChild("about", new Vector2(0, 0));
                AboutView.Draw(about, openWelcome, openChangelog);
                ImGui.EndChild();
                break;
        }

        Preview.Draw();
    }

    private enum Tab { Farm, History, Settings, Dependencies, About }

    private static (FontAwesomeIcon Icon, string Label)[] Tabs() =>
    [
        (FontAwesomeIcon.Tshirt, Loc.T("Farm", "Farm")),
        (FontAwesomeIcon.History, Loc.T("History", "Verlauf")),
        (FontAwesomeIcon.Cog, Loc.T("Settings", "Einstellungen")),
        (FontAwesomeIcon.PuzzlePiece, Loc.T("Dependencies", "Abhängigkeiten")),
        (FontAwesomeIcon.InfoCircle, Loc.T("About", "Info")),
    ];

    private int tab;

    private void SaveLanguage(string code)
    {
        config.Language = code;
        save();
    }

    private void DrawHeader()
    {
        ImGui.AlignTextToFramePadding();
        Widgets.Icon(FontAwesomeIcon.Magic, Theme.AccentLight);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.AccentLight, "EnGlam");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(Theme.Muted, version);

        var missing = MissingRequirement();
        var rotationName = SettingsControls.RotationLabel(config.Rotation);
        var rollingText = rolling.Available ? Loc.T("Rolling", "Würfeln") : Loc.T("Rolling unavailable", "Würfeln nicht verfügbar");
        var startLabel = controller.IsRunning ? Loc.T("Stop", "Stopp") : Loc.T("Start", "Start");
        var startIcon = controller.IsRunning ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var total = Widgets.PillWidth("AutoDuty", true) + Widgets.PillWidth(rotationName, true)
                  + Widgets.PillWidth(rollingText, true) + Widgets.PrimaryButtonWidth(startLabel, startIcon) + Widgets.LanguagePillWidth() + spacing * 4;

        ImGui.SameLine(ImGui.GetContentRegionMax().X - total);
        Widgets.StatusPill("AutoDuty", autoDuty.IsLoaded ? Theme.Success : Theme.Danger);
        ImGui.SameLine();
        Widgets.StatusPill(rotationName, env.RotationAvailable ? Theme.Success : Theme.Danger);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(Loc.T("Rotation plugin AutoDuty uses while farming.", "Rotation-Plugin, das AutoDuty beim Farmen nutzt."));
        ImGui.SameLine();
        Widgets.StatusPill(rollingText, rolling.Available ? Theme.Success : Theme.Warning);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(rolling.Available
                ? Loc.T("Rolls Need on farm list items in groups with other players.", "Würfelt in Gruppen mit anderen Spielern Bedarf auf Items der Farmliste.")
                : Loc.T("Game function not found, probably after a patch. Farming works without rolling.", "Spielfunktion nicht gefunden, wahrscheinlich nach einem Patch. Farmen geht auch ohne Würfeln."));
        ImGui.SameLine();

        if (controller.IsRunning)
        {
            if (Widgets.PrimaryButton(startLabel, startIcon))
                controller.Stop();
        }
        else
        {
            var canStart = farmList.Count > 0 && missing is null;
            if (Widgets.PrimaryButton(startLabel, startIcon, canStart))
            {
                foreach (var item in farmList)
                    item.Reset();

                controller.Start(farmList, new FarmOptions(config.MaxRunsPerItem, config.MaxRunsPerSession, config.Unsynced));
            }

            if (!canStart && ImGui.IsItemHovered())
                ImGui.SetTooltip(missing ?? Loc.T("Add an item to the farm list first.", "Füge zuerst ein Item zur Farmliste hinzu."));
        }

        ImGui.SameLine();
        Widgets.LanguagePill(SaveLanguage);

        var status = Loc.T($"{StatusText()} · runs this session: {controller.SessionRuns}", $"{StatusText()} · Runs in dieser Sitzung: {controller.SessionRuns}");
        if (guard.Pending.Count > 0)
            status += Loc.T($" · settings waiting to be restored: {guard.Pending.Count}", $" · Einstellungen warten auf Wiederherstellung: {guard.Pending.Count}");
        if (missing is not null && !controller.IsRunning)
            status += $" · {missing}";
        var statusStart = ImGui.GetCursorPosX();
        ImGui.TextColored(Theme.Muted, status);

        if (config.ShowDiagnostics)
        {
            // Rechtsbündig, aber nie über den Statustext hinweg.
            var timingText = timing.Summary();
            ImGui.SameLine(Math.Max(statusStart + ImGui.CalcTextSize(status).X + ImGui.GetStyle().ItemSpacing.X * 2,
                ImGui.GetContentRegionMax().X - ImGui.CalcTextSize(timingText).X));
            ImGui.TextColored(Theme.Muted, timingText);
        }
    }

    private string StatusText()
    {
        var german = Loc.Current == Language.German;
        var n = controller.StatusCount;
        var item = controller.StatusItem;
        var duty = controller.StatusDuty;

        return controller.Code switch
        {
            StatusCode.Idle => german ? "bereit" : "idle",
            StatusCode.Starting => german ? "startet" : "starting",
            StatusCode.Stopped => german ? "gestoppt" : "stopped",
            StatusCode.PrepareFailed => german ? "AutoDuty ließ sich nicht vorbereiten" : "AutoDuty could not be prepared",
            StatusCode.LoggedOut => german ? "ausgeloggt" : "logged out",
            StatusCode.AutoDutyUnloaded => german ? "AutoDuty wurde entladen" : "AutoDuty was unloaded",
            StatusCode.JobChanged => german ? "Job gewechselt" : "job changed",
            StatusCode.Done => german ? "fertig" : "done",
            StatusCode.DoneManual => german
                ? $"fertig, {n} {(n == 1 ? "Item" : "Items")} zum Selbstfarmen übrig"
                : $"done, {n} {(n == 1 ? "item" : "items")} left to farm yourself",
            StatusCode.SessionLimit => german ? "Sitzungslimit erreicht" : "session limit reached",
            StatusCode.StartFailed => german ? "AutoDuty konnte den Run nicht starten" : "AutoDuty could not start the run",
            StatusCode.StoppedBeforeEntry => german ? "AutoDuty hat vor dem Betreten der Duty gestoppt" : "AutoDuty stopped before entering the duty",
            StatusCode.StoppedInDuty => german ? "AutoDuty hat in der Duty gestoppt" : "AutoDuty stopped inside the duty",
            StatusCode.RunStarting => german ? $"{item}: Run {n} in {duty}, startet" : $"{item}: run {n} in {duty}, starting",
            StatusCode.InRun => german ? $"{item}: Run {n} in {duty}" : $"{item}: run {n} in {duty}",
            _ => controller.Status,
        };
    }

    private void DrawSearch()
    {
        Widgets.SectionHeader(Loc.T("Search", "Suche"));

        ImGui.PushFont(UiBuilder.IconFont);
        var importWidth = ImGui.CalcTextSize(FontAwesomeIcon.FileImport.ToIconString()).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.PopFont();

        ImGui.SetNextItemWidth(-(importWidth + ImGui.GetStyle().ItemSpacing.X));
        ImGui.InputTextWithHint("##search", Loc.T("Search items with dungeon drops", "Items mit Dungeon-Drops suchen"), ref search, 100);
        ImGui.SameLine();
        DrawImportButton();
        ImGui.Spacing();

        // Mit Favoriten teilt sich die Spalte: oben die Treffer mit eigenem Scrollbereich, unten die Favoriten.
        var resultsHeight = config.Favorites.Count > 0 ? Math.Max(140, ImGui.GetContentRegionAvail().Y * 0.55f) : 0;
        ImGui.BeginChild("results", new Vector2(0, resultsHeight));
        DrawResults();
        ImGui.EndChild();

        if (config.Favorites.Count == 0)
            return;

        ImGui.Spacing();
        Widgets.SectionHeader($"{Loc.T("Favorites", "Favoriten")} · {config.Favorites.Count}");
        ImGui.BeginChild("favorites", new Vector2(0, 0));
        foreach (var id in config.Favorites.ToList())
            DrawItemRow("fav", id, game.ItemName(id), game.ItemIcon(id), DutyCount(id));
        ImGui.EndChild();
    }

    private ImportPreview? previewInstance;
    private ImportPreview Preview => previewInstance ??= new ImportPreview(
        game, drops, planner, env,
        () => autoDuty.IsLoaded,
        () => controller.IsRunning,
        id => farmList.Any(i => i.ItemId == id),
        Add);

    private GlamourImport? importer;

    private GlamourImport Importer => importer ??= new GlamourImport(
        game.EquipableItems.Select(i => new ImportItem(i.Id, i.Name, i.EnglishName, i.Icon)),
        id => drops.SourcesFor(id).Count > 0);

    private void DrawImportButton()
    {
        ImGui.BeginDisabled(controller.IsRunning);
        if (Widgets.IconButton("import", FontAwesomeIcon.FileImport, Loc.T("Import a glamour set", "Glamour-Set importieren")))
            ImGui.OpenPopup("importmenu");
        ImGui.EndDisabled();

        if (!ImGui.BeginPopup("importmenu"))
            return;

        if (ImGui.Selectable(Loc.T("From clipboard", "Aus der Zwischenablage")))
            Preview.Show(new LocText("Clipboard", "Zwischenablage"), Importer.MatchAll(GlamourImport.Parse(ImGui.GetClipboardText())));

        ImGui.TextColored(Theme.Muted, Loc.T("Eorzea Collection: open the glamour, press Ctrl+U,", "Eorzea Collection: Glamour öffnen, Strg+U drücken,"));
        ImGui.TextColored(Theme.Muted, Loc.T("then Ctrl+A and Ctrl+C. A list of item names works too.", "dann Strg+A und Strg+C. Eine Liste mit Itemnamen geht auch."));

        ImGui.Separator();
        var inspected = InspectImport.WindowOpen ? InspectImport.ItemIds() : [];
        ImGui.BeginDisabled(inspected.Count == 0);
        if (ImGui.Selectable(Loc.T("From inspected character", "Vom betrachteten Charakter")))
            Preview.Show(new LocText("Inspected character", "Betrachteter Charakter"), [.. inspected.Select(id => new ImportResult(new ImportEntry(game.ItemName(id), game.ItemIcon(id)), id))]);
        ImGui.EndDisabled();

        if (inspected.Count == 0)
            ImGui.TextColored(Theme.Muted, Loc.T("Inspect a character in game first.", "Betrachte zuerst im Spiel einen Charakter."));

        ImGui.EndPopup();
    }

    private void DrawResults()
    {
        if (search.Length < 2)
        {
            ImGui.TextColored(Theme.Muted, Loc.T("Type at least two letters.", "Gib mindestens zwei Buchstaben ein."));
            if (config.Favorites.Count == 0)
                ImGui.TextColored(Theme.Muted, Loc.T("Right-click an item to add it to your favorites.", "Rechtsklick auf ein Item fügt es den Favoriten hinzu."));
            return;
        }

        // Nur bei geändertem Suchtext neu filtern: Über alle Items in jedem Frame kostete das gemessen 46 ms
        // (kulturabhängiger Vergleich) und drückte die Bildrate.
        if (resultsFor != search)
        {
            resultsFor = search;
            results = game.EquipableItems
                .Where(i => (i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                             || i.EnglishName.Contains(search, StringComparison.OrdinalIgnoreCase))
                            && drops.SourcesFor(i.Id).Count > 0)
                .Take(100)
                .Select(i => (i.Id, i.Name, i.Icon, DutyCount(i.Id)))
                .ToList();
        }

        foreach (var (id, name, icon, dungeons) in results)
            DrawItemRow("result", id, name, icon, dungeons);
    }

    /// <summary>Zeile mit Icon, Name und Zahl der Inhalte; Klick wählt aus, Rechtsklick öffnet das Item-Menü.</summary>
    private void DrawItemRow(string scope, uint id, string name, uint icon, int duties)
    {
        var start = ImGui.GetCursorScreenPos();
        if (ImGui.Selectable($"##{scope}{id}", id == selectedItem, ImGuiSelectableFlags.None, new Vector2(0, 32)))
            selectedItem = id;

        if (ImGui.BeginPopupContextItem($"ctx{scope}{id}"))
        {
            DrawItemMenu(id);
            ImGui.EndPopup();
        }

        var draw = ImGui.GetWindowDrawList();
        Widgets.GameIconAt(start + new Vector2(2, 2), icon, 28);
        draw.AddText(start + new Vector2(38, 1), Theme.U32(Theme.Text), name);

        var info = duties == 1 ? Loc.T("1 duty", "1 Duty") : Loc.T($"{duties} duties", $"{duties} Duties");
        if (config.Favorites.Contains(id) && scope != "fav")
            info += Loc.T(" · favorite", " · Favorit");
        draw.AddText(start + new Vector2(38, 16), Theme.U32(Theme.Muted), info);
    }

    /// <summary>Inhalt des Rechtsklick-Menüs für ein Item.</summary>
    private void DrawItemMenu(uint id, bool fromHistory = false)
    {
        ImGui.TextColored(Theme.AccentLight, game.ItemName(id));
        ImGui.Separator();

        var inList = farmList.Any(i => i.ItemId == id);
        ImGui.BeginDisabled(controller.IsRunning);
        if (ImGui.Selectable(inList ? Loc.T("Remove from farm list", "Von der Farmliste entfernen") : Loc.T("Add to farm list", "Zur Farmliste hinzufügen")))
        {
            if (inList)
                Remove(id);
            else
                Add(id);
        }
        ImGui.EndDisabled();

        var favorite = config.Favorites.Contains(id);
        if (ImGui.Selectable(favorite ? Loc.T("Remove from favorites", "Aus den Favoriten entfernen") : Loc.T("Add to favorites", "Zu den Favoriten hinzufügen")))
        {
            if (favorite)
                config.Favorites.Remove(id);
            else
                config.Favorites.Add(id);
            save();
        }

        if (ImGui.Selectable(Loc.T("Try on", "Anprobieren")))
            tryOn(id);

        if (fromHistory)
        {
            ImGui.Separator();
            if (ImGui.Selectable(Loc.T("Remove from history", "Aus dem Verlauf entfernen")))
                history.Remove(id);
        }
    }

    private void DrawHistory()
    {
        var entries = history.Entries;
        Widgets.SectionHeader($"{Loc.T("History", "Verlauf")} · {entries.Count}");

        if (entries.Count == 0)
        {
            ImGui.TextColored(Theme.Muted, Loc.T("Everything you farm with EnGlam shows up here, with runs and whether you got it.", "Alles, was du mit EnGlam farmst, erscheint hier, mit Runs und ob du es bekommen hast."));
            return;
        }

        if (!ImGui.BeginTable("historyTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.PadOuterX))
            return;

        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Runs", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn(Loc.T("Sessions", "Sitzungen"), ImGuiTableColumnFlags.WidthFixed, 84);
        ImGui.TableSetupColumn(Loc.T("State", "Status"), ImGuiTableColumnFlags.WidthFixed, 170);
        ImGui.TableSetupColumn(Loc.T("Last farmed", "Zuletzt gefarmt"), ImGuiTableColumnFlags.WidthFixed, 140);
        ImGui.TableHeadersRow();

        foreach (var entry in entries)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var start = ImGui.GetCursorScreenPos();
            if (ImGui.Selectable($"##hist{entry.ItemId}", entry.ItemId == selectedItem, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0, 28)))
                selectedItem = entry.ItemId;

            if (ImGui.BeginPopupContextItem($"ctxhist{entry.ItemId}"))
            {
                DrawItemMenu(entry.ItemId, fromHistory: true);
                ImGui.EndPopup();
            }

            Widgets.GameIconAt(start + new Vector2(0, 2), game.ItemIcon(entry.ItemId), 24);
            ImGui.GetWindowDrawList().AddText(start + new Vector2(32, 6), Theme.U32(Theme.Text), game.ItemName(entry.ItemId));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.Runs.ToString());
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(entry.Sessions.ToString());
            ImGui.TableNextColumn();
            if (entry.State == HistoryState.Obtained)
                ImGui.TextColored(Theme.Success, Loc.T($"obtained {entry.ObtainedAt:dd.MM.yyyy}", $"erhalten {entry.ObtainedAt:dd.MM.yyyy}"));
            else
                ImGui.TextColored(Theme.Warning, Loc.T("open", "offen"));
            ImGui.TableNextColumn();
            ImGui.TextColored(Theme.Muted, entry.LastFarmed.ToString("dd.MM.yyyy HH:mm"));
        }

        ImGui.EndTable();
    }

    private int DutyCount(uint id) => drops.SourcesFor(id).Select(s => s.ContentFinderConditionId).Distinct().Count();

    private void DrawFarmList()
    {
        Widgets.SectionHeader($"{Loc.T("Farm list", "Farmliste")} · {farmList.Count}");

        if (selectedItem != 0 && !farmList.Any(i => i.ItemId == selectedItem))
        {
            ImGui.BeginDisabled(controller.IsRunning);
            if (ImGui.Button(Loc.T($"Add {game.ItemName(selectedItem)} to the list", $"{game.ItemName(selectedItem)} zur Liste hinzufügen")))
                Add(selectedItem);
            ImGui.EndDisabled();
            ImGui.Spacing();
        }

        if (farmList.Count == 0)
        {
            ImGui.TextColored(Theme.Muted, Loc.T("Search for an item on the left and add it here.", "Suche links ein Item und füge es hier hinzu."));
            ImGui.Spacing();
            return;
        }

        FarmItem? remove = null;

        // Kopie, weil das Rechtsklick-Menü Einträge entfernen kann, während die Karten gezeichnet werden.
        foreach (var item in farmList.ToList())
        {
            var clicked = Widgets.Card($"farm{item.ItemId}", innerWidth =>
            {
                // Feste Positionen relativ zur Karte, damit Markierung und Mülleimer bündig am rechten Rand sitzen.
                var origin = ImGui.GetCursorScreenPos();
                var right = origin.X + innerWidth;
                var textX = origin.X + 46;
                var lineHeight = ImGui.GetTextLineHeight();

                Widgets.GameIcon(game.ItemIcon(item.ItemId), 36);

                ImGui.SetCursorScreenPos(new Vector2(textX, origin.Y));
                ImGui.TextUnformatted(item.Name);

                // Nur ungelaufene Items neu planen; laufende und erledigte tragen ihren Stand selbst.
                var plan = item.State == FarmItemState.Waiting && item.DutyName is null ? planner.Plan(item.ItemId, env.Context) : null;
                var manual = item.State == FarmItemState.Manual || plan?.Reason == SkipReason.NoPath;

                var (pillText, pillBorder, pillColor) = StatePill(item, manual);
                var trashWidth = controller.IsRunning ? 0f : ImGui.GetFrameHeight();
                var pillX = right - trashWidth - (trashWidth > 0 ? 6 : 0) - Widgets.PillWidth(pillText, false);
                ImGui.SetCursorScreenPos(new Vector2(pillX, origin.Y - 2));
                Widgets.Pill(pillText, pillBorder, pillColor);

                if (!controller.IsRunning)
                {
                    ImGui.SetCursorScreenPos(new Vector2(right - trashWidth, origin.Y - 4));
                    if (Widgets.IconButton($"rm{item.ItemId}", FontAwesomeIcon.Trash, Loc.T("Remove from list", "Aus der Liste entfernen")))
                        remove = item;
                }

                ImGui.SetCursorScreenPos(new Vector2(textX, origin.Y + lineHeight + 4));
                ImGui.TextColored(Theme.Muted, DetailLine(item, plan, manual));

                var showBar = item.State == FarmItemState.Running || (item.Runs > 0 && !item.IsDone);
                if (showBar)
                {
                    ImGui.SetCursorScreenPos(new Vector2(textX, origin.Y + lineHeight * 2 + 10));
                    Widgets.ProgressBar((float)item.Runs / Math.Max(1, config.MaxRunsPerItem), right - textX);
                }

                // Mindesthöhe der Karte: Icon oder zwei Textzeilen, mit Balken etwas mehr.
                var bottom = Math.Max(origin.Y + 36, ImGui.GetCursorScreenPos().Y);
                ImGui.SetCursorScreenPos(new Vector2(origin.X, bottom));
                ImGui.Dummy(new Vector2(innerWidth, 0));
            }, item.ItemId == selectedItem);

            if (clicked)
                selectedItem = selectedItem == item.ItemId ? 0 : item.ItemId;

            // Die Karte endet mit einem Platzhalter über ihre ganze Fläche; dessen Rechteck ist die Karte.
            var popupId = $"ctxfarm{item.ItemId}";
            if (ImGui.IsMouseHoveringRect(ImGui.GetItemRectMin(), ImGui.GetItemRectMax()) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                ImGui.OpenPopup(popupId);

            if (ImGui.BeginPopup(popupId))
            {
                DrawItemMenu(item.ItemId);
                ImGui.EndPopup();
            }

            ImGui.Spacing();
        }

        if (remove is not null)
            Remove(remove.ItemId);
    }

    private void DrawSources()
    {
        if (selectedItem == 0)
            return;

        ImGui.Spacing();
        Widgets.SectionHeader($"{Loc.T("Sources", "Quellen")} · {game.ItemName(selectedItem)}");

        var result = planner.Plan(selectedItem, env.Context);
        if (result.CanFarm)
        {
            var unsynced = env.Context.Unsynced;
            ImGui.TextColored(Theme.AccentLight, Loc.T(
                $"Farms {result.Duty!.Name} as {result.Mode}{(unsynced ? ", unsynced" : "")} ({Chance(result.Chance)})",
                $"Farmt {result.Duty!.Name} als {result.Mode}{(unsynced ? ", ohne Sync" : "")} ({Chance(result.Chance)})"));
        }
        else
        {
            ImGui.TextColored(Theme.Warning, Loc.T($"Can't be farmed right now: {ReasonText(result.Reason).En}", $"Lässt sich gerade nicht farmen: {ReasonText(result.Reason).De}"));
        }

        if (!ImGui.BeginTable("sources", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.PadOuterX))
            return;

        ImGui.TableSetupColumn("Duty", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn(Loc.T("Kind", "Art"), ImGuiTableColumnFlags.WidthFixed, 90);
        ImGui.TableSetupColumn("Chance", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Lv", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn(Loc.T("Ready", "Bereit"), ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn(Loc.T("Modes", "Modi"), ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        foreach (var source in drops.SourcesFor(selectedItem))
        {
            game.Duties.TryGetValue(source.ContentFinderConditionId, out var duty);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(duty?.Name ?? Loc.T($"Unknown duty {source.ContentFinderConditionId}", $"Unbekannte Duty {source.ContentFinderConditionId}"));
            ImGui.TableNextColumn();
            ImGui.TextColored(Theme.Muted, KindText(source.Kind));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(Chance(source.Chance));
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(duty?.Level.ToString() ?? "-");
            ImGui.TableNextColumn();
            if (duty is null)
                ImGui.TextColored(Theme.Muted, "-");
            else if (!GameData.IsUnlocked(duty))
                ImGui.TextColored(Theme.Warning, Loc.T("locked", "gesperrt"));
            else if (!autoDuty.HasPath(duty.TerritoryType))
                ImGui.TextColored(Theme.Warning, Loc.T("no AutoDuty path", "kein AutoDuty-Pfad"));
            else
                ImGui.TextColored(Theme.Success, Loc.T("ready", "bereit"));
            ImGui.TableNextColumn();
            ImGui.TextColored(Theme.Muted, duty is null ? "-" : DutyModeRules.ModesFor(duty).ToString());
        }

        ImGui.EndTable();
    }

    private void DrawSettings()
    {
        ImGui.Spacing();
        Widgets.SectionHeader(Loc.T("Settings", "Einstellungen"));

        ImGui.BeginDisabled(controller.IsRunning);
        Widgets.Card("settings", width =>
        {
            var half = (width - 32) / 2;

            SettingsControls.PartyChoice(config, save, width);

            ImGui.Spacing();
            if (!ImGui.BeginTable("settingsGrid", 2, ImGuiTableFlags.None))
                return;

            ImGui.TableSetupColumn("a", ImGuiTableColumnFlags.WidthFixed, half + 16);
            ImGui.TableSetupColumn("b", ImGuiTableColumnFlags.WidthFixed, half);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var unsynced = config.Unsynced;
            ImGui.BeginDisabled(config.Party != Party.DutyFinder);
            if (Widgets.ToggleRow(Loc.T("Unsynced", "Ohne Sync"), ref unsynced, half,
                    config.Party == Party.DutyFinder
                        ? Loc.T("Run duties unsynced. Not possible for Dawntrail duties.", "Duties ohne Level-Sync laufen. Bei Dawntrail-Duties nicht möglich.")
                        : Loc.T("NPC parties are always synced.", "NPC-Gruppen sind immer gesynct.")))
            {
                config.Unsynced = unsynced;
                save();
            }
            ImGui.EndDisabled();

            ImGui.TableNextColumn();
            var pass = config.PassOthers;
            if (Widgets.ToggleRow(Loc.T("Pass other loot", "Bei fremdem Loot passen"), ref pass, half,
                    Loc.T("In groups with other players, roll Pass on everything that isn't on the farm list.",
                        "Passt in Gruppen mit anderen Spielern bei allem, was nicht auf der Farmliste steht.")))
            {
                config.PassOthers = pass;
                save();
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            SettingsControls.RotationChoiceRow(config, save, half);

            ImGui.TableNextColumn();
            SettingsControls.BareModeRow(config, save, half);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            NumberRow(Loc.T("Runs per item", "Runs pro Item"), config.MaxRunsPerItem, 1, 999, v => config.MaxRunsPerItem = v, half);
            ImGui.TableNextColumn();
            NumberRow(Loc.T("Runs per session", "Runs pro Sitzung"), config.MaxRunsPerSession, 1, 9999, v => config.MaxRunsPerSession = v, half);

            ImGui.EndTable();
        });
        ImGui.EndDisabled();

        ImGui.Spacing();
        Widgets.Card("diagnostics", width =>
        {
            var diagnostics = config.ShowDiagnostics;
            if (Widgets.ToggleRow(Loc.T("Diagnostics", "Diagnose"), ref diagnostics, width,
                    Loc.T("Show the draw time of this window in the status line and write it to the log every 10 seconds.",
                        "Zeigt die Zeichenzeit dieses Fensters in der Statuszeile und schreibt sie alle 10 Sekunden ins Log.")))
            {
                config.ShowDiagnostics = diagnostics;
                save();
            }
        });
    }

    private void NumberRow(string label, int value, int min, int max, Action<int> set, float width)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        ImGui.SameLine(width - 100);
        ImGui.SetNextItemWidth(100);
        if (ImGui.InputInt($"##{label}", ref value))
        {
            set(Math.Clamp(value, min, max));
            save();
        }
    }

    private void Add(uint itemId)
    {
        farmList.Add(new FarmItem(itemId, game.ItemName(itemId)));
        config.FarmList.Add(itemId);
        save();
    }

    private void Remove(uint itemId)
    {
        farmList.RemoveAll(i => i.ItemId == itemId);
        config.FarmList.Remove(itemId);
        if (selectedItem == itemId)
            selectedItem = 0;
        save();
    }

    private string? MissingRequirement() =>
        !autoDuty.IsLoaded ? Loc.T("AutoDuty isn't loaded.", "AutoDuty ist nicht geladen.")
        : env.RotationAvailable ? null
        : config.Rotation is RotationChoice.AutoDutySetting or RotationChoice.All
            ? Loc.T("No rotation plugin loaded (Boss Mod, Wrath Combo or Rotation Solver Reborn).", "Kein Rotation-Plugin geladen (Boss Mod, Wrath Combo oder Rotation Solver Reborn).")
            : Loc.T($"{SettingsControls.RotationLabel(config.Rotation)} isn't loaded.", $"{SettingsControls.RotationLabel(config.Rotation)} ist nicht geladen.");

    private string DetailLine(FarmItem item, PlanResult? plan, bool manual)
    {
        if (manual)
            return ManualText(item.ItemId);

        if (item.State == FarmItemState.Skipped)
            return ReasonText(item.SkipReason).ToString();

        var duty = item.DutyName;
        if (duty is null)
        {
            plan ??= planner.Plan(item.ItemId, env.Context);
            if (!plan.CanFarm)
                return ReasonText(plan.Reason).ToString();

            duty = $"{plan.Duty!.Name} · {plan.Mode}{(env.Context.Unsynced ? Loc.T(" · unsynced", " · ohne Sync") : "")}";
        }

        return item.Runs > 0 ? $"{duty} · {Loc.T("run", "Run")} {item.Runs}/{config.MaxRunsPerItem}" : duty;
    }

    /// <summary>Die Duties ohne AutoDuty-Pfad, in denen das Item droppt, für die Detailzeile.</summary>
    private string ManualText(uint itemId)
    {
        var duties = drops.SourcesFor(itemId)
            .Select(s => game.Duties.GetValueOrDefault(s.ContentFinderConditionId))
            .Where(d => d is not null && !autoDuty.HasPath(d.TerritoryType))
            .Select(d => d!.Name)
            .Distinct()
            .ToList();

        return duties.Count switch
        {
            0 => Loc.T("No AutoDuty path. Run it yourself.", "Kein AutoDuty-Pfad. Lauf es selbst."),
            1 => Loc.T($"No AutoDuty path. Run {duties[0]} yourself.", $"Kein AutoDuty-Pfad. Lauf {duties[0]} selbst."),
            _ => Loc.T($"No AutoDuty path. Run {duties[0]} (+{duties.Count - 1} more) yourself.", $"Kein AutoDuty-Pfad. Lauf {duties[0]} (+{duties.Count - 1} weitere) selbst."),
        };
    }

    private static (string Text, Vector4 Border, Vector4 Color) StatePill(FarmItem item, bool manual) => manual
        ? (Loc.T("farm yourself", "selbst farmen"), Theme.WarningBorder, Theme.Warning)
        : item.State switch
    {
        FarmItemState.Running => (Loc.T("running", "läuft"), Theme.Accent, Theme.AccentLight),
        FarmItemState.Obtained => (Loc.T("obtained", "erhalten"), Theme.SuccessBorder, Theme.Success),
        FarmItemState.AlreadyOwned => (Loc.T("already owned", "schon vorhanden"), Theme.SuccessBorder, Theme.Success),
        FarmItemState.Skipped => (Loc.T("skipped", "übersprungen"), Theme.WarningBorder, Theme.Warning),
        FarmItemState.LimitReached => (Loc.T("limit reached", "Limit erreicht"), Theme.WarningBorder, Theme.Warning),
        FarmItemState.Failed => (Loc.T("failed", "fehlgeschlagen"), Theme.DangerBorder, Theme.Danger),
        _ => (Loc.T("waiting", "wartet"), Theme.Border, Theme.Muted),
    };

    internal static LocText ReasonText(SkipReason? reason) => reason switch
    {
        SkipReason.NoSource => new("No known dungeon drops this item.", "Kein bekannter Dungeon liefert dieses Item."),
        SkipReason.NoDutyData => new("The duty isn't in the game data.", "Die Duty steht nicht in den Spieldaten."),
        SkipReason.ModeNotAvailable => new("No source can be run with this party.", "Keine Quelle lässt sich mit dieser Gruppe laufen."),
        SkipReason.UnsyncNotPossible => new("Can't run unsynced (Dawntrail or NPC party).", "Ohne Sync nicht möglich (Dawntrail oder NPC-Gruppe)."),
        SkipReason.NoPath => new("AutoDuty has no path for the duty.", "AutoDuty hat keinen Pfad für die Duty."),
        SkipReason.Locked => new("Duty locked.", "Duty gesperrt."),
        SkipReason.LevelTooLow => new("Your level is too low.", "Dein Level ist zu niedrig."),
        _ => new("Unknown reason.", "Unbekannter Grund."),
    };

    private static string KindText(DropKind kind) => kind switch
    {
        DropKind.Boss => Loc.T("boss", "Boss"),
        DropKind.BossChest => Loc.T("boss chest", "Bosstruhe"),
        DropKind.Chest => Loc.T("chest", "Truhe"),
        _ => Loc.T("other", "Sonstige"),
    };

    private static string Chance(decimal? chance) => chance is { } c ? $"{c:0.##} %" : Loc.T("unknown", "unbekannt");
}
