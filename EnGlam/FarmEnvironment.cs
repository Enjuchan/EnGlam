using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using EnGlam.Core.Duties;
using EnGlam.Core.Farming;
using EnGlam.Core.History;
using EnGlam.Core.Planning;
using EnGlam.Core.Settings;
using EnGlam.External;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace EnGlam;

/// <summary>Verbindet die Ablaufsteuerung mit Spiel, AutoDuty und gemerkten Einstellungen.</summary>
public sealed class FarmEnvironment(
    Configuration config,
    Planner planner,
    AutoDutyIpc autoDuty,
    SettingsGuard guard,
    FarmHistory history,
    IDalamudPluginInterface pluginInterface,
    IClientState clientState,
    IPlayerState playerState,
    IPluginLog log) : IFarmEnvironment
{
    /// <summary>InternalName der Rotations-Plugins, die AutoDuty schalten kann.</summary>
    private static string[] InternalNames(RotationChoice choice) => choice switch
    {
        RotationChoice.BossMod => ["BossMod"],
        RotationChoice.WrathCombo => ["WrathCombo"],
        RotationChoice.RotationSolverReborn => ["RotationSolver"],
        _ => ["BossMod", "WrathCombo", "RotationSolver"],
    };

    public DateTime Now => DateTime.UtcNow;

    public uint CurrentTerritory => clientState.TerritoryType;

    public bool AutoDutyStopped => autoDuty.IsStopped();

    public bool AutoDutyLoaded => autoDuty.IsLoaded;

    public bool IsLoggedIn => clientState.IsLoggedIn;

    public uint ClassJobId => playerState.IsLoaded ? playerState.ClassJob.RowId : 0;

    /// <summary>Ob das gewählte Rotations-Plugin geladen ist; ohne würde AutoDuty nur laufen, aber nicht kämpfen.</summary>
    public bool RotationAvailable =>
        pluginInterface.InstalledPlugins.Any(p => p.IsLoaded && InternalNames(config.Rotation).Contains(p.InternalName));

    public PlanContext Context => new(
        config.Party,
        playerState.IsLoaded ? playerState.Level : 0,
        autoDuty.HasPath,
        GameData.IsUnlocked,
        config.Unsynced && config.Party == Party.DutyFinder);

    /// <summary>
    /// Stellt AutoDuty so ein, dass es die Rotation selbst verwaltet, und sagt ihm, welches Plugin es nehmen soll.
    /// Das Umschalten des Plugins übernimmt AutoDuty; so gibt es keine zwei Steuerungen nebeneinander.
    /// </summary>
    public bool Prepare()
    {
        if (!autoDuty.IsLoaded)
        {
            log.Warning("AutoDuty ist nicht geladen.");
            return false;
        }

        var ok = ApplyAutoDuty(AutoDutyIpc.KeyManageRotation, "True")
              && ApplyAutoDuty(AutoDutyIpc.KeyAlternativeRotation, "False")
              && (config.Rotation == RotationChoice.AutoDutySetting
                  || ApplyAutoDuty(AutoDutyIpc.KeyRotationPlugin, config.Rotation.ToString()));

        if (ok)
            log.Information("AutoDuty verwaltet die Rotation mit {Rotation}.", config.Rotation);

        return ok;
    }

    public PlanResult Plan(uint itemId) => planner.Plan(itemId, Context);

    public unsafe int CountItem(uint itemId)
    {
        var inventory = InventoryManager.Instance();
        if (inventory == null)
            return 0;

        return inventory->GetInventoryItemCount(itemId, false, true, true)
             + inventory->GetInventoryItemCount(itemId, true, true, true);
    }

    public bool StartRun(DutyInfo duty, DutyMode mode, bool unsynced)
    {
        if (!autoDuty.IsLoaded)
        {
            log.Warning("AutoDuty ist nicht geladen.");
            return false;
        }

        var useUnsynced = unsynced && DutyModeRules.CanRunUnsynced(duty, mode);

        if (!ApplyAutoDuty(AutoDutyIpc.KeyUnsynced, useUnsynced.ToString()) || !ApplyAutoDuty(AutoDutyIpc.KeyDutyMode, mode.ToString()))
            return false;

        if (!autoDuty.Run(duty.TerritoryType, 1, config.BareMode))
            return false;

        log.Information("Run gestartet: {Duty} ({Territory}), Modus {Mode}, unsynced {Unsynced}.",
            duty.Name, duty.TerritoryType, mode, useUnsynced);
        return true;
    }

    public void StopAutoDuty() => autoDuty.Stop();

    public void OnSessionStarted() => history.BeginSession();

    public void OnRunStarted(uint itemId) => history.RecordRun(itemId, DateTime.Now);

    public void OnItemObtained(uint itemId) => history.RecordObtained(itemId, DateTime.Now);

    public void RestoreSettings()
    {
        var restored = guard.RestoreAll(s => s.Owner == AutoDutyIpc.Owner && autoDuty.IsLoaded && autoDuty.SetConfig(s.Key, s.Value));
        if (restored > 0)
            log.Information("{Count} fremde Einstellungen zurückgesetzt.", restored);
    }

    private bool ApplyAutoDuty(string key, string value)
    {
        var current = autoDuty.GetConfig(key);
        if (string.IsNullOrEmpty(current))
        {
            log.Warning("AutoDuty-Einstellung {Key} nicht lesbar.", key);
            return false;
        }

        guard.Set(AutoDutyIpc.Owner, key, value, () => current, v => autoDuty.SetConfig(key, v));
        return true;
    }
}
