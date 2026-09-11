using System;

/// <summary>Immutable effective player statistics derived from attributes and equipped armor.</summary>
public readonly struct PlayerRuntimeStatistics : IEquatable<PlayerRuntimeStatistics>
{
    public CharacterDerivedStatistics CharacterStatistics { get; }
    public int PhysicalDefense { get; }
    public int MagicalDefense { get; }

    public int MaximumHealth => CharacterStatistics.MaximumHealth;
    public int MaximumStamina => CharacterStatistics.MaximumStamina;
    public int MaximumMana => CharacterStatistics.MaximumMana;
    public int AdditionalLootChanceBasisPoints => CharacterStatistics.AdditionalLootChanceBasisPoints;

    internal PlayerRuntimeStatistics(
        in CharacterDerivedStatistics characterStatistics,
        int physicalDefense,
        int magicalDefense)
    {
        CharacterStatistics = characterStatistics;
        PhysicalDefense = physicalDefense;
        MagicalDefense = magicalDefense;
    }

    public bool Equals(PlayerRuntimeStatistics other) =>
        CharacterStatistics.Equals(other.CharacterStatistics) &&
        PhysicalDefense == other.PhysicalDefense &&
        MagicalDefense == other.MagicalDefense;

    public override bool Equals(object obj) =>
        obj is PlayerRuntimeStatistics other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = CharacterStatistics.GetHashCode();
            hash = (hash * 397) ^ PhysicalDefense;
            return (hash * 397) ^ MagicalDefense;
        }
    }
}
