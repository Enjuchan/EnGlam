namespace EnGlam;

/// <summary>EnGlams Abhängigkeiten für den Dependencies-Tab.</summary>
public static class EnGlamDependencies
{
    /// <summary>Repository-Adressen wie in Questionable 15.756.3.56.</summary>
    public static readonly IReadOnlyList<DependencyGroup> Groups =
    [
        new(new("Required", "Erforderlich"), DependencyKind.Required,
        [
            new("AutoDuty", "AutoDuty", new("Runs the duties for EnGlam.", "Führt die Duties für EnGlam aus."), "https://github.com/erdelf/AutoDuty",
                ["https://puni.sh/api/repository/erdelf"]),
            new("vnavmesh", "vnavmesh", new("Navigation that AutoDuty needs to move through duties.", "Navigation, die AutoDuty braucht, um sich durch Duties zu bewegen."), "https://github.com/awgil/ffxiv_navmesh",
                ["https://puni.sh/api/repository/veyn"]),
        ]),
        new(new("Rotation", "Rotation"), DependencyKind.AnyOf,
        [
            new("Boss Mod", "BossMod", new("Rotation and mechanics. AutoDuty switches it on while farming.", "Rotation und Mechaniken. AutoDuty schaltet es beim Farmen ein."), "https://github.com/awgil/ffxiv_bossmod",
                ["https://puni.sh/api/repository/veyn"]),
            new("Wrath Combo", "WrathCombo", new("Rotation with auto-rotation. AutoDuty switches it on while farming.", "Rotation mit Auto-Rotation. AutoDuty schaltet sie beim Farmen ein."), "https://github.com/PunishXIV/WrathCombo",
                ["https://love.puni.sh/ment.json", "https://puni.sh/api/plugins"]),
            new("Rotation Solver Reborn", "RotationSolver", new("Rotation. AutoDuty switches it on while farming.", "Rotation. AutoDuty schaltet sie beim Farmen ein."), "https://github.com/FFXIV-CombatReborn/RotationSolverReborn",
                ["https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json"]),
        ]),
        new(new("Conflicts", "Konflikte"), DependencyKind.Conflict,
        [
            new("LazyLoot", "LazyLoot", new("Also rolls on loot. With both loaded, two plugins roll at the same time.", "Würfelt ebenfalls auf Loot. Sind beide geladen, würfeln zwei Plugins gleichzeitig."), "https://github.com/PunishXIV/LazyLoot", []),
        ]),
    ];
}
