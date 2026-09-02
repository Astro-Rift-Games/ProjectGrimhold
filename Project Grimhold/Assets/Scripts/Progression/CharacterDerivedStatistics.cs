using System;

/// <summary>Immutable statistics derived only from the current character attributes.</summary>
public readonly struct CharacterDerivedStatistics : IEquatable<CharacterDerivedStatistics>
{
    public int MaximumHealth { get; }
    public int MaximumStamina { get; }
    public int AdditionalLootChanceBasisPoints { get; }

    internal CharacterDerivedStatistics(
        int maximumHealth,
        int maximumStamina,
        int additionalLootChanceBasisPoints)
    {
        MaximumHealth = maximumHealth;
        MaximumStamina = maximumStamina;
        AdditionalLootChanceBasisPoints = additionalLootChanceBasisPoints;
    }

    public bool Equals(CharacterDerivedStatistics other) =>
        MaximumHealth == other.MaximumHealth &&
        MaximumStamina == other.MaximumStamina &&
        AdditionalLootChanceBasisPoints == other.AdditionalLootChanceBasisPoints;

    public override bool Equals(object obj) =>
        obj is CharacterDerivedStatistics other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = MaximumHealth;
            hash = (hash * 397) ^ MaximumStamina;
            return (hash * 397) ^ AdditionalLootChanceBasisPoints;
        }
    }
}
