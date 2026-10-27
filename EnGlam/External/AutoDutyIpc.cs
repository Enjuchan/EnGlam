using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace EnGlam.External;

/// <summary>
/// AutoDuty-IPC, Namen und Typen wie im installierten AutoDuty 0.0.0.390 und wie Questionable sie nutzt.
/// SetConfig akzeptiert nur Strings; Bool- und Enum-Werte gehen als Text.
/// </summary>
public sealed class AutoDutyIpc : IpcBase
{
    public const string Owner = "AutoDuty";
    public const string KeyDutyMode = "Meta.DutyModeEnum";
    public const string KeyUnsynced = "Meta.Unsynced";
    public const string KeyManageRotation = "DutyConfig.AutoManageRotationPluginState";
    public const string KeyAlternativeRotation = "DutyConfig.UsingAlternativeRotationPlugin";
    public const string KeyRotationPlugin = "DutyConfig.RotationPlugin";

    private readonly ICallGateSubscriber<uint, int, bool, object> run;
    private readonly ICallGateSubscriber<object> stop;
    private readonly ICallGateSubscriber<bool> isStopped;
    private readonly ICallGateSubscriber<uint, bool> contentHasPath;
    private readonly ICallGateSubscriber<string, string> getConfig;
    private readonly ICallGateSubscriber<string, string, object> setConfig;

    private readonly Dictionary<uint, bool> pathCache = [];

    public AutoDutyIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
        : base(pluginInterface, log, Owner)
    {
        run = pluginInterface.GetIpcSubscriber<uint, int, bool, object>("AutoDuty.Run");
        stop = pluginInterface.GetIpcSubscriber<object>("AutoDuty.Stop");
        isStopped = pluginInterface.GetIpcSubscriber<bool>("AutoDuty.IsStopped");
        contentHasPath = pluginInterface.GetIpcSubscriber<uint, bool>("AutoDuty.ContentHasPath");
        getConfig = pluginInterface.GetIpcSubscriber<string, string>("AutoDuty.GetConfig");
        setConfig = pluginInterface.GetIpcSubscriber<string, string, object>("AutoDuty.SetConfig");
    }

    public bool Run(uint territoryType, int loops, bool bareMode) =>
        Safe(nameof(Run), () => run.InvokeAction(territoryType, loops, bareMode));

    public bool Stop() => Safe(nameof(Stop), () => stop.InvokeAction());

    /// <summary>Ohne erreichbares AutoDuty gilt es als gestoppt.</summary>
    public bool IsStopped() => Safe(nameof(IsStopped), () => isStopped.InvokeFunc(), true);

    /// <summary>
    /// Pfade ändern sich nur mit AutoDuty-Updates; deshalb wird das Ergebnis gemerkt, aber nur bei geladenem AutoDuty,
    /// damit ein vorübergehend fehlendes AutoDuty keine falschen Einträge hinterlässt.
    /// </summary>
    public bool HasPath(uint territoryType)
    {
        if (pathCache.TryGetValue(territoryType, out var known))
            return known;

        if (!IsLoaded)
            return false;

        var result = Safe(nameof(HasPath), () => contentHasPath.InvokeFunc(territoryType), false);
        pathCache[territoryType] = result;
        return result;
    }

    public string? GetConfig(string key) => Safe(nameof(GetConfig), () => getConfig.InvokeFunc(key), (string?)null);

    public bool SetConfig(string key, string value) =>
        Safe($"{nameof(SetConfig)}({key})", () => setConfig.InvokeAction(key, value));
}
