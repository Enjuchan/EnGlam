using Dalamud.Bindings.ImGui;
using EnGlam.Ui;

namespace EnGlam;

/// <summary>Inhalte des Willkommensfensters: Zweck, erste Schritte und die wichtigsten Einstellungen.</summary>
public static class EnGlamWelcome
{
    public static WelcomeContent Create(string version, Configuration config, Action save) => new(
        "EnGlam",
        version,
        new("EnGlam finds the dungeons that drop the glamour items you pick and has AutoDuty run them, one run at a time, until each item is in your inventory. It never walks or fights itself: AutoDuty runs the duty and your rotation plugin fights.",
            "EnGlam findet die Dungeons, in denen die gewählten Glamour-Items droppen, und lässt AutoDuty sie Run für Run laufen, bis jedes Item in deinem Inventar liegt. Es läuft und kämpft nie selbst: AutoDuty führt die Duty aus, dein Rotations-Plugin kämpft."),
        [
            new("Make sure AutoDuty and a rotation plugin are installed. The list below shows what is missing and installs it for you.",
                "Stell sicher, dass AutoDuty und ein Rotations-Plugin installiert sind. Die Liste unten zeigt, was fehlt, und installiert es für dich."),
            new("Open EnGlam, search for an item and add it to the farm list. Only items that drop in a duty show up.",
                "Öffne EnGlam, such ein Item und füge es der Farmliste hinzu. Es erscheinen nur Items, die in einer Duty droppen."),
            new("Check the settings below, then press Start. EnGlam checks your inventory after every run and moves on to the next item.",
                "Prüfe die Einstellungen unten und drück dann auf Start. EnGlam prüft nach jedem Run dein Inventar und macht mit dem nächsten Item weiter."),
        ],
        width => DrawSettings(config, save, width));

    private static void DrawSettings(Configuration config, Action save, float width)
    {
        SettingsControls.PartyChoice(config, save, width);
        ImGui.Spacing();
        if (!ImGui.BeginTable("welcomeSettings", 1, ImGuiTableFlags.None))
            return;

        ImGui.TableSetupColumn("a", ImGuiTableColumnFlags.WidthFixed, width);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        SettingsControls.RotationChoiceRow(config, save, width);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        SettingsControls.BareModeRow(config, save, width);
        ImGui.EndTable();
    }
}
