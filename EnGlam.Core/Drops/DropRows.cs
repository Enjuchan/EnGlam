namespace EnGlam.Core.Drops;

/// <summary>Direkter Drop eines Dungeon-Bosses.</summary>
public readonly record struct BossDropRow(uint ContentFinderConditionId, uint ItemId);

/// <summary>Inhalt der Truhe, die nach einem Boss erscheint.</summary>
public readonly record struct BossChestRow(uint ContentFinderConditionId, uint ItemId);

/// <summary>Drop ohne nähere Angabe, woher im Dungeon.</summary>
public readonly record struct DungeonDropRow(uint ContentFinderConditionId, uint ItemId);

/// <summary>Eine Schatztruhe im Dungeon.</summary>
public readonly record struct ChestRow(uint ChestId, uint ContentFinderConditionId);

/// <summary>Ein mögliches Item einer Schatztruhe. Probability ist die Anzahl pro 100 Öffnungen.</summary>
public readonly record struct ChestItemRow(uint ChestId, uint ItemId, decimal? Probability);
