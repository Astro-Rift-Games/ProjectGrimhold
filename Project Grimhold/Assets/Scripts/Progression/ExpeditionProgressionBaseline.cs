using System;

/// <summary>Immutable admitted Level and Experience for one Raid participation.</summary>
public readonly struct ExpeditionProgressionBaseline : IEquatable<ExpeditionProgressionBaseline>
{
    public int Level { get; }
    public long Experience { get; }

    public ExpeditionProgressionBaseline(int level, long experience)
    {
        Level = level;
        Experience = experience;
    }

    public bool Equals(ExpeditionProgressionBaseline other) =>
        Level == other.Level && Experience == other.Experience;

    public override bool Equals(object obj) =>
        obj is ExpeditionProgressionBaseline other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Level, Experience);
}
