using Dalamud.Bindings.ImGui;
using EnGlam.Core.Duties;
using EnGlam.Core.Settings;
using EnGlam.Ui;

namespace EnGlam;

/// <summary>
/// Die Bedienelemente für Gruppe, Rotation und Bare Mode, die der Settings-Tab und das Willkommensfenster teilen,
/// damit beide dieselben Felder schreiben und sofort speichern.
/// </summary>
public static class SettingsControls
{
    private static readonly Party[] Parties = Enum.GetValues<Party>();
    private static readonly RotationChoice[] Rotations = Enum.GetValues<RotationChoice>();

    /// <summary>Beschriftung und Umschaltleiste über die ganze Breite.</summary>
    public static void PartyChoice(Configuration config, Action save, float width)
    {
        ImGui.TextColored(Theme.Muted, Loc.T("Party", "Gruppe"));
        var party = config.Party;
        if (Widgets.Segmented("party", Parties, PartyLabel, ref party, width))
        {
            config.Party = party;
            save();
        }
    }

    /// <summary>Beschriftung links, Auswahlfeld rechtsbündig; gehört in eine Tabellenspalte der Breite <paramref name="width"/>.</summary>
    public static void RotationChoiceRow(Configuration config, Action save, float width)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Rotation");
        ImGui.SameLine(width - 180);
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
    }

    public static void BareModeRow(Configuration config, Action save, float width)
    {
        var bare = config.BareMode;
        if (Widgets.ToggleRow("Bare Mode", ref bare, width,
                Loc.T("AutoDuty skips its extras during the run, like Questionable does.", "AutoDuty lässt im Run seine Extras weg, so wie Questionable es macht.")))
        {
            config.BareMode = bare;
            save();
        }
    }

    private static string PartyLabel(Party p) => p switch
    {
        Party.DutyFinder => "Duty Finder",
        Party.Support => "Duty Support",
        _ => p.ToString(),
    };

    public static string RotationLabel(RotationChoice r) => r switch
    {
        RotationChoice.AutoDutySetting => Loc.T("AutoDuty setting", "AutoDuty-Einstellung"),
        RotationChoice.BossMod => "Boss Mod",
        RotationChoice.WrathCombo => "Wrath Combo",
        RotationChoice.RotationSolverReborn => "Rotation Solver Reborn",
        RotationChoice.All => Loc.T("All (AutoDuty picks)", "Alle (AutoDuty wählt)"),
        _ => r.ToString(),
    };
}
