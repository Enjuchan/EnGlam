namespace EnGlam.Core.Duties;

/// <summary>
/// Welche Modi für einen Inhalt möglich sind, nachgebildet nach AutoDuty 0.0.0.390
/// (AutoDuty.Helpers.ContentHelper.PopulateDuties), weil AutoDuty das nicht per IPC anbietet.
/// Neue Inhalte, die AutoDuty über feste Listen erkennt, müssen hier nachgetragen werden.
/// </summary>
public static class DutyModeRules
{
    /// <summary>ContentType-Werte, die AutoDuty überhaupt aufnimmt.</summary>
    public static readonly IReadOnlySet<uint> SupportedContentTypes = new HashSet<uint> { 2, 4, 5, 20, 30, 40 };

    /// <summary>TerritoryType-IDs der Squadron-Inhalte (AutoDuty: ListGCArmyContent).</summary>
    public static readonly IReadOnlySet<uint> SquadronTerritories =
        new HashSet<uint> { 1245, 1039, 1041, 1042, 1330, 1331, 0, 160, 349, 362, 188, 1064, 1066, 430, 510 };

    /// <summary>TerritoryType-IDs der Variant-Dungeons (AutoDuty: ListVVDContent).</summary>
    public static readonly IReadOnlySet<uint> VariantTerritories = new HashSet<uint> { 1069, 1137, 1176, 1315 };

    /// <summary>Modi, die sich aus der Art des Inhalts ergeben statt aus der Wahl der Gruppe.</summary>
    public const DutyMode ContentModes =
        DutyMode.Regular | DutyMode.Trial | DutyMode.Raid | DutyMode.Variant | DutyMode.NoviceHall | DutyMode.Crucible;

    /// <summary>
    /// Der AutoDuty-Modus für einen Inhalt bei gewählter Gruppe, oder None, wenn der Inhalt so nicht läuft.
    /// Beim Duty Finder bestimmt der Inhalt selbst den Modus, etwa Trial für Ifrit.
    /// </summary>
    public static DutyMode EffectiveMode(DutyInfo duty, Party party)
    {
        var modes = ModesFor(duty);

        var wanted = party switch
        {
            Party.DutyFinder => ContentModes,
            Party.Support => DutyMode.Support,
            Party.Trust => DutyMode.Trust,
            Party.Squadron => DutyMode.Squadron,
            _ => DutyMode.None,
        };

        var possible = modes & wanted;
        return possible & (DutyMode)(-(int)possible);   // niedrigstes gesetztes Bit
    }

    /// <summary>Modi, in denen das Spiel unsynced erlaubt; die NPC-Modi sind immer synced.</summary>
    public const DutyMode UnsyncableModes = DutyMode.Regular | DutyMode.Trial | DutyMode.Raid;

    /// <summary>Unsynced nur in passenden Modi und nicht für Dawntrail-Inhalte (ExVersion 5), wie AutoDuty CanRun.</summary>
    public static bool CanRunUnsynced(DutyInfo duty, DutyMode mode) =>
        (UnsyncableModes & mode) != 0 && duty.ExVersion != 5;

    public static DutyMode ModesFor(DutyInfo duty)
    {
        var modes = duty.ContentType switch
        {
            2 => DutyMode.Regular,
            4 => DutyMode.Trial,
            5 => DutyMode.Raid,
            20 => DutyMode.NoviceHall,
            30 when VariantTerritories.Contains(duty.TerritoryType) => DutyMode.Variant,
            40 => DutyMode.Crucible,
            _ => DutyMode.None,
        };

        if (SquadronTerritories.Contains(duty.TerritoryType))
            modes |= DutyMode.Squadron;

        if (duty.DawnRowId >= 1 && DawnIndex(duty.DawnRowId, duty.ExVersion) > -1 && duty.DawnParticipableCount > 1)
            modes |= DutyMode.Support;

        if (duty.TrustOrdinal >= 0 && TrustIndex(duty.TrustOrdinal, duty.ExVersion) > -1)
            modes |= DutyMode.Trust;

        return modes;
    }

    private static long DawnIndex(uint rowId, uint exVersion) => exVersion switch
    {
        0 => rowId - 200L,
        1 => rowId - 215L,
        2 => rowId - 224L,
        3 => rowId - 1L,
        4 => rowId - 12L,
        5 => rowId - 24L,
        _ => -1,
    };

    private static int TrustIndex(int ordinal, uint exVersion) => exVersion switch
    {
        3 => ordinal,
        4 => ordinal - 11,
        5 => ordinal - 22,
        _ => -1,
    };
}
