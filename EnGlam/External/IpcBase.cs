using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc.Exceptions;
using Dalamud.Plugin.Services;

namespace EnGlam.External;

/// <summary>
/// Gemeinsame Grundlage für die Anbindung fremder Plugins.
/// IPC-Fehler werden hier abgefangen und protokolliert, weil ein fehlendes oder geändertes Fremd-Plugin
/// den Ablauf geordnet stoppen soll, statt EnGlam abstürzen zu lassen.
/// </summary>
public abstract class IpcBase(IDalamudPluginInterface pluginInterface, IPluginLog log, string internalName)
{
    protected IDalamudPluginInterface PluginInterface { get; } = pluginInterface;

    public string InternalName { get; } = internalName;

    public bool IsLoaded => PluginInterface.InstalledPlugins.Any(p => p.InternalName == InternalName && p.IsLoaded);

    protected T Safe<T>(string call, Func<T> func, T fallback)
    {
        try
        {
            return func();
        }
        catch (Exception e) when (e is IpcError or TargetInvocationException)
        {
            log.Warning(e, "IPC {Plugin}.{Call} fehlgeschlagen.", InternalName, call);
            return fallback;
        }
    }

    /// <summary>Gibt zurück, ob der Aufruf ohne IPC-Fehler durchlief.</summary>
    protected bool Safe(string call, Action action) => Safe(call, () => { action(); return true; }, false);
}
