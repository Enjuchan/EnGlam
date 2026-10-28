using Dalamud.Configuration;
using EnGlam.Core.Duties;
using EnGlam.Core.History;
using EnGlam.Core.Settings;

namespace EnGlam;

/// <summary>
/// Welches Rotations-Plugin AutoDuty nehmen soll. Die Namen außer <see cref="AutoDutySetting"/> entsprechen
/// AutoDutys Enum RotationPlugin und gehen so per SetConfig an AutoDuty.
/// </summary>
public enum RotationChoice
{
    AutoDutySetting,
    BossMod,
    WrathCombo,
    RotationSolverReborn,
    All,
}

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public RotationChoice Rotation { get; set; } = RotationChoice.AutoDutySetting;

    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>Ursprüngliche Werte fremder Einstellungen, die noch zurückgeschrieben werden müssen.</summary>
    public List<SavedSetting> PendingRestore { get; set; } = [];

    public List<uint> FarmList { get; set; } = [];

    public List<uint> Favorites { get; set; } = [];

    public List<HistoryEntry> History { get; set; } = [];

    public Party Party { get; set; } = Party.DutyFinder;

    public bool Unsynced { get; set; }

    public int MaxRunsPerItem { get; set; } = 30;

    public int MaxRunsPerSession { get; set; } = 60;

    /// <summary>Beim Farmen auf alles außer den gesuchten Items Pass würfeln.</summary>
    public bool PassOthers { get; set; } = true;

    /// <summary>AutoDutys Bare Mode: lässt beim Run Extras weg, so wie Questionable es standardmäßig nutzt.</summary>
    public bool BareMode { get; set; } = true;

    /// <summary>Gewählte Sprache ("de" oder "en"); null heißt wie Dalamud.</summary>
    public string? Language { get; set; }

    /// <summary>Zeigt die Zeichenzeit in der Statuszeile und schreibt sie ins Log.</summary>
    public bool ShowDiagnostics { get; set; }
}
