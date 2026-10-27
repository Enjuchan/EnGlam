namespace EnGlam.Core.Duties;

/// <summary>
/// Ein Inhalt aus dem Sheet ContentFinderCondition mit allem, was für Modus, Freischaltung und Planung gebraucht wird.
/// </summary>
/// <param name="DawnRowId">Zeile in DawnContent, die auf diesen Inhalt zeigt, sonst 0.</param>
/// <param name="DawnParticipableCount">Anzahl Subrows in DawnContentParticipable für DawnRowId.</param>
/// <param name="TrustOrdinal">Position unter den DawnContent-Zeilen mit Trust-Kennzeichen, sonst -1.</param>
public sealed record DutyInfo(
    uint ContentFinderConditionId,
    uint TerritoryType,
    uint InstanceContentId,
    string Name,
    uint ContentType,
    uint ExVersion,
    byte Level,
    uint DawnRowId,
    int DawnParticipableCount,
    int TrustOrdinal);
