using EnGlam.Core.Duties;
using EnGlam.Core.Planning;

namespace EnGlam.Core.Farming;

/// <summary>Alles, was die Ablaufsteuerung vom Spiel und von AutoDuty braucht.</summary>
public interface IFarmEnvironment
{
    DateTime Now { get; }

    uint CurrentTerritory { get; }

    bool AutoDutyStopped { get; }

    bool AutoDutyLoaded { get; }

    bool IsLoggedIn { get; }

    /// <summary>Aktueller Job; 0, wenn kein Charakter geladen ist.</summary>
    uint ClassJobId { get; }

    /// <summary>Einmal vor dem ersten Run: AutoDuty für die Rotation einstellen. False, wenn das nicht ging.</summary>
    bool Prepare();

    PlanResult Plan(uint itemId);

    /// <summary>Anzahl im Inventar, im Arsenal und angelegt, NQ und HQ zusammen.</summary>
    int CountItem(uint itemId);

    /// <summary>Setzt Modus und Unsynced in AutoDuty und startet einen einzelnen Run. False, wenn das nicht ging.</summary>
    bool StartRun(DutyInfo duty, DutyMode mode, bool unsynced);

    void StopAutoDuty();

    /// <summary>Ein Druck auf Start hat eine neue Farm-Sitzung begonnen.</summary>
    void OnSessionStarted();

    /// <summary>Für dieses Item wurde ein Run gestartet.</summary>
    void OnRunStarted(uint itemId);

    /// <summary>Das Item ist nach einem Run im Inventar angekommen.</summary>
    void OnItemObtained(uint itemId);

    /// <summary>Schreibt alle geänderten fremden Einstellungen zurück.</summary>
    void RestoreSettings();
}
