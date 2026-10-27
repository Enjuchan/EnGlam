using System.Reflection;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Reflection;

namespace EnGlam;

public enum DependencyGroup
{
    Required,
    Rotation,
    Conflict,
}

/// <param name="Repositories">Erste Adresse ist das Haupt-Repository, weitere sind Spiegel; leer bei Plugins ohne Install-Knopf.</param>
public sealed record Dependency(
    string DisplayName,
    string InternalName,
    string Purpose,
    string Website,
    DependencyGroup Group,
    string[] Repositories);

public enum InstallState { Missing, Installed, Loaded }

/// <summary>
/// Plugins, mit denen EnGlam zusammenarbeitet, und ihre Installation. Installiert wird wie in Questionable
/// über ECommons DalamudReflector: Repository in Dalamud eintragen, falls es fehlt, dann über Dalamuds internen
/// PluginManager installieren. Das greift per Reflection in Dalamud-Interna und kann nach Dalamud-Updates brechen.
/// </summary>
public sealed class Dependencies(IDalamudPluginInterface pluginInterface, IPluginLog log)
{
    /// <summary>Repository-Adressen wie in Questionable 15.756.3.56.</summary>
    public static readonly IReadOnlyList<Dependency> All =
    [
        new("AutoDuty", "AutoDuty", "Runs the duties for EnGlam.", "https://github.com/erdelf/AutoDuty",
            DependencyGroup.Required, ["https://puni.sh/api/repository/erdelf"]),
        new("vnavmesh", "vnavmesh", "Navigation that AutoDuty needs to move through duties.", "https://github.com/awgil/ffxiv_navmesh",
            DependencyGroup.Required, ["https://puni.sh/api/repository/veyn"]),
        new("Boss Mod", "BossMod", "Rotation and mechanics. AutoDuty switches it on while farming.", "https://github.com/awgil/ffxiv_bossmod",
            DependencyGroup.Rotation, ["https://puni.sh/api/repository/veyn"]),
        new("Wrath Combo", "WrathCombo", "Rotation with auto-rotation. AutoDuty switches it on while farming.", "https://github.com/PunishXIV/WrathCombo",
            DependencyGroup.Rotation, ["https://love.puni.sh/ment.json", "https://puni.sh/api/plugins"]),
        new("Rotation Solver Reborn", "RotationSolver", "Rotation. AutoDuty switches it on while farming.", "https://github.com/FFXIV-CombatReborn/RotationSolverReborn",
            DependencyGroup.Rotation, ["https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json"]),
        new("LazyLoot", "LazyLoot", "Also rolls on loot. With both loaded, two plugins roll at the same time.", "https://github.com/PunishXIV/LazyLoot",
            DependencyGroup.Conflict, []),
    ];

    // Threadsicher, weil die Installation im Hintergrund läuft, während das Fenster liest.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, bool> installing = new();

    /// <summary>Letzte Meldung pro Plugin für die Anzeige, etwa ein Fehler beim Installieren.</summary>
    public System.Collections.Concurrent.ConcurrentDictionary<string, string> Messages { get; } = new();

    public bool IsInstalling(Dependency dependency) => installing.ContainsKey(dependency.InternalName);

    public (InstallState State, string? Version) StateOf(Dependency dependency)
    {
        var plugin = pluginInterface.InstalledPlugins
            .FirstOrDefault(p => string.Equals(p.InternalName, dependency.InternalName, StringComparison.OrdinalIgnoreCase));

        if (plugin is null)
            return (InstallState.Missing, null);

        return (plugin.IsLoaded ? InstallState.Loaded : InstallState.Installed, plugin.Version.ToString());
    }

    /// <summary>Installiert ein fehlendes Plugin; ist es installiert, aber aus, öffnet es den Plugin-Installer.</summary>
    public void Install(Dependency dependency)
    {
        if (StateOf(dependency).State == InstallState.Installed)
        {
            pluginInterface.OpenPluginInstallerTo(Dalamud.Interface.PluginInstallerOpenKind.InstalledPlugins, dependency.DisplayName);
            return;
        }

        if (dependency.Repositories.Length == 0 || !installing.TryAdd(dependency.InternalName, true))
            return;

        Messages[dependency.InternalName] = "Installing…";
        _ = InstallAsync(dependency);
    }

    private async Task InstallAsync(Dependency dependency)
    {
        try
        {
            var loaded = await InstallFromRepositoryAsync(dependency).ConfigureAwait(false);
            Messages[dependency.InternalName] = loaded ? "Installed." : "Installed, but not loaded. Check the plugin installer.";
            log.Information("{Plugin} installiert, geladen: {Loaded}.", dependency.DisplayName, loaded);
        }
        catch (Exception e)
        {
            // Fehler beim Installieren gehören vor den Nutzer, nicht nur ins Log, weil er sonst nur einen hängenden Knopf sieht.
            Messages[dependency.InternalName] = $"Couldn't install: {e.GetBaseException().Message}";
            log.Error(e, "Installieren von {Plugin} fehlgeschlagen.", dependency.DisplayName);
        }
        finally
        {
            installing.TryRemove(dependency.InternalName, out _);
        }
    }

    private static async Task<bool> InstallFromRepositoryAsync(Dependency dependency)
    {
        var repoUrl = dependency.Repositories.FirstOrDefault(DalamudReflector.HasRepo) ?? dependency.Repositories[0];

        var manifests = await DalamudReflector.GetPluginMaster(repoUrl).ConfigureAwait(false);
        if (manifests is null || manifests.Count == 0)
            throw new InvalidOperationException($"No plugins found in {repoUrl}.");

        var manifest = manifests.FirstOrDefault(m => string.Equals((string)m.GetFoP("InternalName"), dependency.InternalName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"{dependency.DisplayName} isn't listed in {repoUrl}.");

        if (!DalamudReflector.HasRepo(repoUrl))
            DalamudReflector.AddRepo(repoUrl, enabled: true);

        DalamudReflector.SaveDalamudConfig();
        DalamudReflector.ReloadPluginMasters();

        var pluginManager = DalamudReflector.GetPluginManager();
        var install = pluginManager.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(m => m.Name == "InstallPluginAsync")
            .OrderBy(m => m.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new MissingMethodException("Dalamud's PluginManager.InstallPluginAsync wasn't found.");

        if (install.Invoke(pluginManager, BindArguments(install, manifest)) is not Task task)
            throw new InvalidOperationException("InstallPluginAsync didn't return a task.");

        await task.ConfigureAwait(false);
        var result = task.GetFoP("Result");
        return result is not null && (bool)result.GetFoP("IsLoaded");
    }

    /// <summary>Belegt die Parameter von InstallPluginAsync nach Namen, wie Questionable es tut.</summary>
    private static object?[] BindArguments(MethodInfo method, object manifest) =>
        [.. method.GetParameters().Select(p => p.Name switch
        {
            "repoManifest" => manifest,
            "useTesting" => false,
            "reason" => Dalamud.Plugin.PluginLoadReason.Installer,
            _ => p.HasDefaultValue ? p.DefaultValue : p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null,
        })];
}
