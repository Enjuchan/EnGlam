namespace EnGlam;

/// <summary>EnGlams Abhängigkeiten für den Dependencies-Tab.</summary>
public static class EnGlamDependencies
{
    /// <summary>Repository-Adressen wie in Questionable 15.756.3.56.</summary>
    public static readonly IReadOnlyList<DependencyGroup> Groups =
    [
        new("Required", DependencyKind.Required,
        [
            new("AutoDuty", "AutoDuty", "Runs the duties for EnGlam.", "https://github.com/erdelf/AutoDuty",
                ["https://puni.sh/api/repository/erdelf"]),
            new("vnavmesh", "vnavmesh", "Navigation that AutoDuty needs to move through duties.", "https://github.com/awgil/ffxiv_navmesh",
                ["https://puni.sh/api/repository/veyn"]),
        ]),
        new("Rotation", DependencyKind.AnyOf,
        [
            new("Boss Mod", "BossMod", "Rotation and mechanics. AutoDuty switches it on while farming.", "https://github.com/awgil/ffxiv_bossmod",
                ["https://puni.sh/api/repository/veyn"]),
            new("Wrath Combo", "WrathCombo", "Rotation with auto-rotation. AutoDuty switches it on while farming.", "https://github.com/PunishXIV/WrathCombo",
                ["https://love.puni.sh/ment.json", "https://puni.sh/api/plugins"]),
            new("Rotation Solver Reborn", "RotationSolver", "Rotation. AutoDuty switches it on while farming.", "https://github.com/FFXIV-CombatReborn/RotationSolverReborn",
                ["https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json"]),
        ]),
        new("Conflicts", DependencyKind.Conflict,
        [
            new("LazyLoot", "LazyLoot", "Also rolls on loot. With both loaded, two plugins roll at the same time.", "https://github.com/PunishXIV/LazyLoot", []),
        ]),
    ];
}
