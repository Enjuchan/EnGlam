using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using EnGlam.Core.Drops;
using EnGlam.Core.Import;
using EnGlam.Core.Planning;
using EnGlam.Ui;

namespace EnGlam;

public enum ImportStatus { Farmable, FarmYourself, Blocked, NoSource, NotFound, OnList, Owned }

/// <summary>Vorschau eines Imports: zeigt je Item den Stand und übernimmt die Auswahl in die Farm-Liste.</summary>
public sealed class ImportPreview(
    GameData game,
    DropIndex drops,
    Planner planner,
    FarmEnvironment env,
    Func<bool> autoDutyLoaded,
    Func<bool> farming,
    Func<uint, bool> onList,
    Action<uint> add)
{
    private const string PopupId = "Import###EnGlamImport";
    private const float RowHeight = 34f;

    private sealed class Row(string name, uint itemId, uint icon, ImportStatus status, string detail)
    {
        public string Name { get; } = name;
        public uint ItemId { get; } = itemId;
        public uint Icon { get; } = icon;
        public ImportStatus Status { get; } = status;
        public string Detail { get; } = detail;
        public bool Selectable => Status is ImportStatus.Farmable or ImportStatus.FarmYourself or ImportStatus.Blocked;
        public bool Selected;
    }

    private List<Row> rows = [];
    private string source = string.Empty;
    private bool pathsUnknown;
    private bool openRequested;

    public void Show(string sourceLabel, IReadOnlyList<ImportResult> results)
    {
        source = sourceLabel;
        pathsUnknown = !autoDutyLoaded();

        var context = env.Context;
        rows = [.. results.Select(r => Classify(r, context))];
        foreach (var row in rows)
            row.Selected = row.Selectable;

        openRequested = true;
    }

    private Row Classify(ImportResult result, PlanContext context)
    {
        if (result.ItemId is not { } id)
            return new Row(result.Entry.Name, 0, 0, ImportStatus.NotFound, "Not in the game data.");

        Row Make(ImportStatus status, string detail) => new(game.ItemName(id), id, game.ItemIcon(id), status, detail);

        if (onList(id))
            return Make(ImportStatus.OnList, "Already on your farm list.");

        if (env.CountItem(id) > 0)
            return Make(ImportStatus.Owned, "Already in your inventory or armoury.");

        if (drops.SourcesFor(id).Count == 0)
            return Make(ImportStatus.NoSource, "No known dungeon drops this item.");

        var plan = planner.Plan(id, context);
        if (plan.CanFarm)
            return Make(ImportStatus.Farmable, plan.Duty!.Name);

        return plan.Reason == SkipReason.NoPath
            ? Make(ImportStatus.FarmYourself, "AutoDuty has no path. Run it yourself.")
            : Make(ImportStatus.Blocked, MainWindow.ReasonText(plan.Reason));
    }

    public void Draw()
    {
        if (openRequested)
        {
            ImGui.OpenPopup(PopupId);
            openRequested = false;
        }

        ImGui.SetNextWindowSize(new Vector2(600, 500), ImGuiCond.Appearing);
        if (!ImGui.BeginPopupModal(PopupId))
            return;

        var selected = rows.Count(r => r.Selected);
        Widgets.SectionHeader($"Import · {source}");
        ImGui.TextColored(Theme.Muted, rows.Count == 0
            ? "Nothing found to import."
            : $"{rows.Count} items found, {selected} selected.");

        if (pathsUnknown)
            ImGui.TextColored(Theme.Warning, "AutoDuty isn't loaded, so paths can't be checked.");

        ImGui.Spacing();
        var footer = ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.Y * 2;
        ImGui.BeginChild("rows", new Vector2(0, -footer));
        for (var i = 0; i < rows.Count; i++)
            DrawRow(i, rows[i]);
        ImGui.EndChild();

        ImGui.Spacing();
        var locked = farming();
        if (Widgets.PrimaryButton(selected == 1 ? "Add 1 item" : $"Add {selected} items", FontAwesomeIcon.Plus, selected > 0 && !locked))
        {
            foreach (var row in rows.Where(r => r.Selected))
                add(row.ItemId);
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel", new Vector2(0, ImGui.GetFrameHeight())))
            ImGui.CloseCurrentPopup();

        if (locked)
        {
            ImGui.SameLine();
            ImGui.TextColored(Theme.Muted, "Stop farming to change the list.");
        }

        ImGui.EndPopup();
    }

    private void DrawRow(int index, Row row)
    {
        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var box = ImGui.GetFrameHeight();

        ImGui.SetCursorScreenPos(start + new Vector2(0, (RowHeight - box) / 2));
        if (row.Selectable)
            ImGui.Checkbox($"##sel{index}", ref row.Selected);
        else
            ImGui.Dummy(new Vector2(box));

        var x = box + 10;
        if (row.Icon != 0)
            Widgets.GameIconAt(start + new Vector2(x, 2), row.Icon, 30);

        var textColor = row.Selectable ? Theme.Text : Theme.Muted;
        draw.AddText(start + new Vector2(x + 38, 1), Theme.U32(textColor), row.Name);
        draw.AddText(start + new Vector2(x + 38, 17), Theme.U32(Theme.Muted), row.Detail);

        var (text, border, color) = StatusPill(row.Status);
        var pillWidth = Widgets.PillWidth(text, false);
        ImGui.SetCursorScreenPos(new Vector2(start.X + width - pillWidth, start.Y + (RowHeight - ImGui.GetTextLineHeight() - 6) / 2));
        Widgets.Pill(text, border, color);

        ImGui.SetCursorScreenPos(start + new Vector2(0, RowHeight));
        ImGui.Dummy(new Vector2(width, 2));
    }

    private static (string Text, Vector4 Border, Vector4 Color) StatusPill(ImportStatus status) => status switch
    {
        ImportStatus.Farmable => ("farmable", Theme.Accent, Theme.AccentLight),
        ImportStatus.FarmYourself => ("farm yourself", Theme.WarningBorder, Theme.Warning),
        ImportStatus.Blocked => ("not now", Theme.WarningBorder, Theme.Warning),
        ImportStatus.Owned => ("owned", Theme.SuccessBorder, Theme.Success),
        ImportStatus.OnList => ("on list", Theme.Border, Theme.Muted),
        ImportStatus.NoSource => ("no dungeon source", Theme.Border, Theme.Muted),
        _ => ("not found", Theme.DangerBorder, Theme.Danger),
    };
}
