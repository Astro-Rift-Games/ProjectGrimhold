using System;
using UnityEngine;

/// <summary>Serialized provisional progression Value for one loot definition.</summary>
[Serializable]
public sealed class RaidLootValueEntry
{
    [SerializeField]
    private LootDefinition _lootDefinition;

    [SerializeField]
    private long _valuePerUnit;

    public LootDefinition LootDefinition => _lootDefinition;
    public long ValuePerUnit => _valuePerUnit;
}
