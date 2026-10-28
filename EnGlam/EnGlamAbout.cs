using Dalamud.Interface;
using EnGlam.Ui;

namespace EnGlam;

/// <summary>Angaben für den About-Tab; Wirkung des Befehls steht auch als Hilfetext im Befehl selbst.</summary>
public static class EnGlamAbout
{
    public static readonly LocText ToggleEffect = new("Opens or closes the EnGlam window.", "Öffnet oder schließt das EnGlam-Fenster.");

    public static AboutInfo Create(string version) => new(
        "EnGlam",
        version,
        new("Finds the dungeons that drop the items you select and has AutoDuty run them until each item is in your inventory.",
            "Findet die Dungeons, in denen die gewählten Items droppen, und lässt AutoDuty sie laufen, bis jedes Item in deinem Inventar liegt."),
        [new("/englam", ToggleEffect)],
        "GPL-3.0",
        "EnGlam - Copyright (c) 2026 Enjuchan",
        [new(new("Open GitHub", "GitHub öffnen"), FontAwesomeIcon.ExternalLinkAlt, "https://github.com/Enjuchan/EnGlam")]);
}
