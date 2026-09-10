using System;

/// <summary>Immutable character statistics derived from attributes and external resource modifiers.</summary>
public readonly struct CharacterDerivedStatistics : IEquatable<CharacterDerivedStatistics>
{
    public int MaximumHealth { get; }
    public int MaximumStamina { get; }
    public int MaximumMana { get; }
    public int AdditionalLootChanceBasisPoints { get; }

    internal CharacterDerivedStatistics(
        int maximumHealth,
        int maximumStamina,
        int maximumMana,
        int additionalLootChanceBasisPoints)
    {
        MaximumHealth = maximumHealth;
        MaximumStamina = maximumStamina;
        MaximumMana = maximumMana;
        AdditionalLootChanceBasisPoints = additionalLootChanceBasisPoints;
    }

    public bool Equals(CharacterDerivedStatistics other) =>
        MaximumHealth == other.MaximumHealth &&
        MaximumStamina == other.MaximumStamina &&
        MaximumMana == other.MaximumMana &&
        AdditionalLootChanceBasisPoints == other.AdditionalLootChanceBasisPoints;

    public override bool Equals(object obj) =>
        obj is CharacterDerivedStatistics other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = MaximumHealth;
            hash = (hash * 397) ^ MaximumStamina;
            hash = (hash * 397) ^ MaximumMana;
            return (hash * 397) ^ AdditionalLootChanceBasisPoints;
        }
    }
}
