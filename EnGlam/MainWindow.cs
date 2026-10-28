using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
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

public sealed class MainWindow(
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
    Dalamud.Plugin.Services.IPluginLog log)
{
    private static readonly Party[] Parties = Enum.GetValues<Party>();
    private static readonly RotationChoice[] Rotations = Enum.GetValues<RotationChoice>();

    public bool Visible;

    private string search = string.Empty;
    private string? resultsFor;
    private List<(uint Id, string Name, uint Icon, int Duties)> results = [];
    private uint selectedItem;

    public void Draw()
    {
        if (!Visible)
            return;

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        DrawWindow();
        timing.Add(System.Diagnostics.Stopwatch.GetElapsedTime(started));
    }

    private readonly FrameTiming timing = new(log, "Draw", TimeSpan.FromSeconds(5), () => config.ShowDiagnostics);

    private void DrawWindow()
    {
        using var theme = Theme.Push();

        ImGui.SetNextWindowSize(new Vector2(960, 660), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(780, 480), new Vector2(float.MaxValue, float.MaxValue));

        if (!ImGui.Begin("EnGlam###EnGlamMain", ref Visible))
        {
            ImGui.End();
            return;
        }

        Decor.Background();
        Decor.HeaderBand(ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y + ImGui.GetTextLineHeight());
        DrawHeader();
        ImGui.Dummy(new Vector2(0, 6));

        Widgets.PillTabs("tabs", Tabs, ref tab);
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
                AboutView.Draw(about);
                ImGui.EndChild();
                break;
        }

        Preview.Draw();
        ImGui.End();
    }

    private enum Tab { Farm, History, Settings, Dependencies, About }

    private static readonly (FontAwesomeIcon Icon, string Label)[] Tabs =
    [
        (FontAwesomeIcon.Tshirt, "Farm"),
        (FontAwesomeIcon.History, "History"),
        (FontAwesomeIcon.Cog, "Settings"),
        (FontAwesomeIcon.PuzzlePiece, "Dependencies"),
        (FontAwesomeIcon.InfoCircle, "About"),
    ];

    private int tab;

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
        var rotationName = RotationLabel(config.Rotation);
        var rollingText = rolling.Available ? "Rolling" : "Rolling unavailable";
        var startLabel = controller.IsRunning ? "Stop" : "Start";
        var startIcon = controller.IsRunning ? FontAwesomeIcon.Stop : FontAwesomeIcon.Play;

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var total = Widgets.PillWidth("AutoDuty", true) + Widgets.PillWidth(rotationName, true)
                  + Widgets.PillWidth(rollingText, true) + Widgets.PrimaryButtonWidth(startLabel, startIcon) + spacing * 3;

        ImGui.SameLine(ImGui.GetContentRegionMax().X - total);
        Widgets.StatusPill("AutoDuty", autoDuty.IsLoaded ? Theme.Success : Theme.Danger);
        ImGui.SameLine();
        Widgets.StatusPill(rotationName, env.RotationAvailable ? Theme.Success : Theme.Danger);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Rotation plugin AutoDuty uses while farming.");
        ImGui.SameLine();
        Widgets.StatusPill(rollingText, rolling.Available ? Theme.Success : Theme.Warning);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(rolling.Available
                ? "Rolls Need on farm list items in groups with other players."
                : "Game function not found, probably after a patch. Farming works without rolling.");
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
                ImGui.SetTooltip(missing ?? "Add an item to the farm list first.");
        }

        var status = $"{controller.Status} · runs this session: {controller.SessionRuns}";
        if (guard.Pending.Count > 0)
            status += $" · settings waiting to be restored: {guard.Pending.Count}";
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

    private void DrawSearch()
    {
        Widgets.SectionHeader("Search");

        ImGui.PushFont(UiBuilder.IconFont);
        var importWidth = ImGui.CalcTextSize(FontAwesomeIcon.FileImport.ToIconString()).X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.PopFont();

        ImGui.SetNextItemWidth(-(importWidth + ImGui.GetStyle().ItemSpacing.X));
        ImGui.InputTextWithHint("##search", "Search items with dungeon drops", ref search, 100);
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
        Widgets.SectionHeader($"Favorites · {config.Favorites.Count}");
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
        if (Widgets.IconButton("import", FontAwesomeIcon.FileImport, "Import a glamour set"))
            ImGui.OpenPopup("importmenu");
        ImGui.EndDisabled();

        if (!ImGui.BeginPopup("importmenu"))
            return;

        if (ImGui.Selectable("From clipboard"))
            Preview.Show("Clipboard", Importer.MatchAll(GlamourImport.Parse(ImGui.GetClipboardText())));

        ImGui.TextColored(Theme.Muted, "Eorzea Collection: open the glamour, press Ctrl+U,");
        ImGui.TextColored(Theme.Muted, "then Ctrl+A and Ctrl+C. A list of item names works too.");

        ImGui.Separator();
        var inspected = InspectImport.WindowOpen ? InspectImport.ItemIds() : [];
        ImGui.BeginDisabled(inspected.Count == 0);
        if (ImGui.Selectable("From inspected character"))
            Preview.Show("Inspected character", [.. inspected.Select(id => new ImportResult(new ImportEntry(game.ItemName(id), game.ItemIcon(id)), id))]);
        ImGui.EndDisabled();

        if (inspected.Count == 0)
            ImGui.TextColored(Theme.Muted, "Inspect a character in game first.");

        ImGui.EndPopup();
    }

    private void DrawResults()
    {
        if (search.Length < 2)
        {
            ImGui.TextColored(Theme.Muted, "Type at least two letters.");
            if (config.Favorites.Count == 0)
                ImGui.TextColored(Theme.Muted, "Right-click an item to add it to your favorites.");
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

        var info = duties == 1 ? "1 duty" : $"{duties} duties";
        if (config.Favorites.Contains(id) && scope != "fav")
            info += " · favorite";
        draw.AddText(start + new Vector2(38, 16), Theme.U32(Theme.Muted), info);
    }

    /// <summary>Inhalt des Rechtsklick-Menüs für ein Item.</summary>
    private void DrawItemMenu(uint id, bool fromHistory = false)
    {
        ImGui.TextColored(Theme.AccentLight, game.ItemName(id));
        ImGui.Separator();

        var inList = farmList.Any(i => i.ItemId == id);
        ImGui.BeginDisabled(controller.IsRunning);
        if (ImGui.Selectable(inList ? "Remove from farm list" : "Add to farm list"))
        {
            if (inList)
                Remove(id);
            else
                Add(id);
        }
        ImGui.EndDisabled();

        var favorite = config.Favorites.Contains(id);
        if (ImGui.Selectable(favorite ? "Remove from favorites" : "Add to favorites"))
        {
            if (favorite)
                config.Favorites.Remove(id);
            else
                config.Favorites.Add(id);
            save();
        }

        if (ImGui.Selectable("Try on"))
            tryOn(id);

        if (fromHistory)
        {
            ImGui.Separator();
            if (ImGui.Selectable("Remove from history"))
                history.Remove(id);
        }
    }

    private void DrawHistory()
    {
        var entries = history.Entries;
        Widgets.SectionHeader($"History · {entries.Count}");

        if (entries.Count == 0)
        {
            ImGui.TextColored(Theme.Muted, "Everything you farm with EnGlam shows up here, with runs and whether you got it.");
            return;
        }

        if (!ImGui.BeginTable("historyTable", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.PadOuterX))
            return;

        ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Runs", ImGuiTableColumnFlags.WidthFixed, 50);
        ImGui.TableSetupColumn("Sessions", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("State", ImGuiTableColumnFlags.WidthFixed, 170);
        ImGui.TableSetupColumn("Last farmed", ImGuiTableColumnFlags.WidthFixed, 120);
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
                ImGui.TextColored(Theme.Success, $"obtained {entry.ObtainedAt:dd.MM.yyyy}");
            else
                ImGui.TextColored(Theme.Warning, "open");
            ImGui.TableNextColumn();
            ImGui.TextColored(Theme.Muted, entry.LastFarmed.ToString("dd.MM.yyyy HH:mm"));
        }

        ImGui.EndTable();
    }

    private int DutyCount(uint id) => drops.SourcesFor(id).Select(s => s.ContentFinderConditionId).Distinct().Count();

    private void DrawFarmList()
    {
        Widgets.SectionHeader($"Farm list · {farmList.Count}");

        if (selectedItem != 0 && !farmList.Any(i => i.ItemId == selectedItem))
        {
            ImGui.BeginDisabled(controller.IsRunning);
            if (ImGui.Button($"Add {game.ItemName(selectedItem)} to the list"))
                Add(selectedItem);
            ImGui.EndDisabled();
            ImGui.Spacing();
        }

        if (farmList.Count == 0)
        {
            ImGui.TextColored(Theme.Muted, "Search for an item on the left and add it here.");
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
                    if (Widgets.IconButton($"rm{item.ItemId}", FontAwesomeIcon.Trash, "Remove from list"))
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
        Widgets.SectionHeader($"Sources · {game.ItemName(selectedItem)}");

        var result = planner.Plan(selectedItem, env.Context);
        if (result.CanFarm)
            ImGui.TextColored(Theme.AccentLight,
                $"Farms {result.Duty!.Name} as {result.Mode}{(env.Context.Unsynced ? ", unsynced" : "")} ({Chance(result.Chance)})");
        else
            ImGui.TextColored(Theme.Warning, $"Can't be farmed right now: {ReasonText(result.Reason)}");

        if (!ImGui.BeginTable("sources", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.PadOuterX))
            return;

        ImGui.TableSetupColumn("Duty", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Kind", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Chance", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Lv", ImGuiTableColumnFlags.WidthFixed, 30);
        ImGui.TableSetupColumn("Ready", ImGuiTableColumnFlags.WidthFixed, 110);
        ImGui.TableSetupColumn("Modes", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        foreach (var source in drops.SourcesFor(selectedItem))
        {
            game.Duties.TryGetValue(source.ContentFinderConditionId, out var duty);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(duty?.Name ?? $"Unknown duty {source.ContentFinderConditionId}");
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
                ImGui.TextColored(Theme.Warning, "locked");
            else if (!autoDuty.HasPath(duty.TerritoryType))
                ImGui.TextColored(Theme.Warning, "no AutoDuty path");
            else
                ImGui.TextColored(Theme.Success, "ready");
            ImGui.TableNextColumn();
            ImGui.TextColored(Theme.Muted, duty is null ? "-" : DutyModeRules.ModesFor(duty).ToString());
        }

        ImGui.EndTable();
    }

    private void DrawSettings()
    {
        ImGui.Spacing();
        Widgets.SectionHeader("Settings");

        ImGui.BeginDisabled(controller.IsRunning);
        Widgets.Card("settings", width =>
        {
            var half = (width - 32) / 2;

            ImGui.TextColored(Theme.Muted, "Party");
            var party = config.Party;
            if (Widgets.Segmented("party", Parties, PartyLabel, ref party, width))
            {
                config.Party = party;
                save();
            }

            ImGui.Spacing();
            if (!ImGui.BeginTable("settingsGrid", 2, ImGuiTableFlags.None))
                return;

            ImGui.TableSetupColumn("a", ImGuiTableColumnFlags.WidthFixed, half + 16);
            ImGui.TableSetupColumn("b", ImGuiTableColumnFlags.WidthFixed, half);

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            var unsynced = config.Unsynced;
            ImGui.BeginDisabled(config.Party != Party.DutyFinder);
            if (Widgets.ToggleRow("Unsynced", ref unsynced, half,
                    config.Party == Party.DutyFinder ? "Run duties unsynced. Not possible for Dawntrail duties." : "NPC parties are always synced."))
            {
                config.Unsynced = unsynced;
                save();
            }
            ImGui.EndDisabled();

            ImGui.TableNextColumn();
            var pass = config.PassOthers;
            if (Widgets.ToggleRow("Pass other loot", ref pass, half,
                    "In groups with other players, roll Pass on everything that isn't on the farm list."))
            {
                config.PassOthers = pass;
                save();
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("Rotation");
            ImGui.SameLine(half - 180);
            ImGui.SetNextItemWidth(180);
            if (ImGui.BeginCombo("##rotation", RotationLabel(config.Rotation)))
            {
                foreach (var r in Rotations)
                {
                    if (ImGui.Selectable(RotationLabel(r), r == config.Rotation))
                    {
                        config.Rotation = r;
                        save();
                    }
                }

                ImGui.EndCombo();
            }

            ImGui.TableNextColumn();
            var bare = config.BareMode;
            if (Widgets.ToggleRow("Bare mode", ref bare, half, "AutoDuty skips its extras during the run, like Questionable does."))
            {
                config.BareMode = bare;
                save();
            }

            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            NumberRow("Runs per item", config.MaxRunsPerItem, 1, 999, v => config.MaxRunsPerItem = v, half);
            ImGui.TableNextColumn();
            NumberRow("Runs per session", config.MaxRunsPerSession, 1, 9999, v => config.MaxRunsPerSession = v, half);

            ImGui.EndTable();
        });
        ImGui.EndDisabled();

        ImGui.Spacing();
        Widgets.Card("diagnostics", width =>
        {
            var diagnostics = config.ShowDiagnostics;
            if (Widgets.ToggleRow("Diagnostics", ref diagnostics, width,
                    "Show the draw time of this window in the status line and write it to the log every 10 seconds."))
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
        !autoDuty.IsLoaded ? "AutoDuty isn't loaded."
        : env.RotationAvailable ? null
        : config.Rotation is RotationChoice.AutoDutySetting or RotationChoice.All
            ? "No rotation plugin loaded (Boss Mod, Wrath Combo or Rotation Solver Reborn)."
            : $"{RotationLabel(config.Rotation)} isn't loaded.";

    private string DetailLine(FarmItem item, PlanResult? plan, bool manual)
    {
        if (manual)
            return ManualText(item.ItemId);

        if (item.State == FarmItemState.Skipped)
            return ReasonText(item.SkipReason);

        var duty = item.DutyName;
        if (duty is null)
        {
            plan ??= planner.Plan(item.ItemId, env.Context);
            if (!plan.CanFarm)
                return ReasonText(plan.Reason);

            duty = $"{plan.Duty!.Name} · {plan.Mode}{(env.Context.Unsynced ? " · unsynced" : "")}";
        }

        return item.Runs > 0 ? $"{duty} · run {item.Runs}/{config.MaxRunsPerItem}" : duty;
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
            0 => "No AutoDuty path. Run it yourself.",
            1 => $"No AutoDuty path. Run {duties[0]} yourself.",
            _ => $"No AutoDuty path. Run {duties[0]} (+{duties.Count - 1} more) yourself.",
        };
    }

    private static (string Text, Vector4 Border, Vector4 Color) StatePill(FarmItem item, bool manual) => manual
        ? ("farm yourself", Theme.WarningBorder, Theme.Warning)
        : item.State switch
    {
        FarmItemState.Running => ("running", Theme.Accent, Theme.AccentLight),
        FarmItemState.Obtained => ("obtained", Theme.SuccessBorder, Theme.Success),
        FarmItemState.AlreadyOwned => ("already owned", Theme.SuccessBorder, Theme.Success),
        FarmItemState.Skipped => ("skipped", Theme.WarningBorder, Theme.Warning),
        FarmItemState.LimitReached => ("limit reached", Theme.WarningBorder, Theme.Warning),
        FarmItemState.Failed => ("failed", Theme.DangerBorder, Theme.Danger),
        _ => ("waiting", Theme.Border, Theme.Muted),
    };

    internal static string ReasonText(SkipReason? reason) => reason switch
    {
        SkipReason.NoSource => "No known dungeon drops this item.",
        SkipReason.NoDutyData => "The duty isn't in the game data.",
        SkipReason.ModeNotAvailable => "No source can be run with this party.",
        SkipReason.UnsyncNotPossible => "Can't run unsynced (Dawntrail or NPC party).",
        SkipReason.NoPath => "AutoDuty has no path for the duty.",
        SkipReason.Locked => "Duty locked.",
        SkipReason.LevelTooLow => "Your level is too low.",
        _ => "Unknown reason.",
    };

    private static string KindText(DropKind kind) => kind switch
    {
        DropKind.Boss => "boss",
        DropKind.BossChest => "boss chest",
        DropKind.Chest => "chest",
        _ => "other",
    };

    private static string PartyLabel(Party p) => p switch
    {
        Party.DutyFinder => "Duty Finder",
        Party.Support => "Duty Support",
        _ => p.ToString(),
    };

    private static string RotationLabel(RotationChoice r) => r switch
    {
        RotationChoice.AutoDutySetting => "AutoDuty setting",
        RotationChoice.BossMod => "Boss Mod",
        RotationChoice.WrathCombo => "Wrath Combo",
        RotationChoice.RotationSolverReborn => "Rotation Solver Reborn",
        RotationChoice.All => "All (AutoDuty picks)",
        _ => r.ToString(),
    };

    private static string Chance(decimal? chance) => chance is { } c ? $"{c:0.##} %" : "unknown";
}
