using EnGlam.Core.Drops;
using EnGlam.Core.Duties;

namespace EnGlam.Core.Planning;

/// <summary>
/// Warum ein Item nicht gefarmt werden kann. Die Reihenfolge entspricht dem Weg zum lauffähigen Dungeon;
/// scheitern alle Quellen, wird der Grund der Quelle gemeldet, die am weitesten kam.
/// </summary>
public enum SkipReason
{
    NoSource,
    NoDutyData,
    ModeNotAvailable,
    UnsyncNotPossible,
    NoPath,
    Locked,
    LevelTooLow,
}

/// <param name="Mode">AutoDuty-Modus, in dem der gewählte Inhalt läuft.</param>
public sealed record PlanResult(DutyInfo? Duty, decimal? Chance, SkipReason? Reason, DutyMode Mode = DutyMode.None)
{
    public bool CanFarm => Duty is not null;
}

/// <summary>Was die Planung über die Umgebung wissen muss.</summary>
/// <param name="HasPath">Ob AutoDuty für eine TerritoryType-ID einen Pfad hat.</param>
/// <param name="IsUnlocked">Ob der Spieler den Inhalt freigeschaltet hat.</param>
public sealed record PlanContext(
    Party Party,
    int PlayerLevel,
    Func<uint, bool> HasPath,
    Func<DutyInfo, bool> IsUnlocked,
    bool Unsynced = false);

public sealed class Planner(DropIndex drops, IReadOnlyDictionary<uint, DutyInfo> duties)
{
    public PlanResult Plan(uint itemId, PlanContext context)
    {
        var sources = drops.SourcesFor(itemId);
        if (sources.Count == 0)
            return new PlanResult(null, null, SkipReason.NoSource);

        var furthest = SkipReason.NoDutyData;
        var candidates = new List<(DutyInfo Duty, decimal? Chance, DutyMode Mode)>();

        foreach (var group in sources.GroupBy(s => s.ContentFinderConditionId))
        {
            var reason = Check(group.Key, context, out var duty, out var mode);
            if (reason is null)
                candidates.Add((duty!, group.Max(s => s.Chance), mode));
            else if (reason > furthest)
                furthest = reason.Value;
        }

        if (candidates.Count == 0)
            return new PlanResult(null, null, furthest);

        // Bekannte Chance vor unbekannter, dann höhere Chance, dann niedrigere Stufe.
        var best = candidates
            .OrderBy(c => c.Chance is null)
            .ThenByDescending(c => c.Chance)
            .ThenBy(c => c.Duty.Level)
            .ThenBy(c => c.Duty.ContentFinderConditionId)
            .First();

        return new PlanResult(best.Duty, best.Chance, null, best.Mode);
    }

    private SkipReason? Check(uint cfc, PlanContext context, out DutyInfo? duty, out DutyMode mode)
    {
        mode = DutyMode.None;

        if (!duties.TryGetValue(cfc, out duty))
            return SkipReason.NoDutyData;

        mode = DutyModeRules.EffectiveMode(duty, context.Party);
        if (mode == DutyMode.None)
            return SkipReason.ModeNotAvailable;

        if (context.Unsynced && !DutyModeRules.CanRunUnsynced(duty, mode))
            return SkipReason.UnsyncNotPossible;

        if (!context.HasPath(duty.TerritoryType))
            return SkipReason.NoPath;

        if (!context.IsUnlocked(duty))
            return SkipReason.Locked;

        if (duty.Level > context.PlayerLevel)
            return SkipReason.LevelTooLow;

        return null;
    }
}
