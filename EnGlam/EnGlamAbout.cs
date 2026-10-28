using Dalamud.Interface;
using EnGlam.Ui;

namespace EnGlam;

/// <summary>Angaben für den About-Tab; Wirkung des Befehls steht auch als Hilfetext im Befehl selbst.</summary>
public static class EnGlamAbout
{
    public const string ToggleEffect = "Opens or closes the EnGlam window.";

    public static AboutInfo Create(string version) => new(
        "EnGlam",
        version,
        "Finds the dungeons that drop the items you select and has AutoDuty run them until each item is in your inventory.",
        [new("/englam", ToggleEffect)],
        "GPL-3.0",
        "EnGlam - Copyright (c) 2026 Enjuchan",
        [new("Open GitHub", FontAwesomeIcon.ExternalLinkAlt, "https://github.com/Enjuchan/EnGlam")]);
}
