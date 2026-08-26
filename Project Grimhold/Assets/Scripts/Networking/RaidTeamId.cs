using System;

/// <summary>
/// Raid-scoped initial team identity. Only equality and inequality carry domain meaning;
/// the numeric value defines neither order nor priority between teams.
/// </summary>
public readonly struct RaidTeamId : IEquatable<RaidTeamId>
{
    private readonly byte _value;

    private RaidTeamId(byte value) => _value = value;

    public int Value => _value;
    public bool IsValid => _value >= 1 && _value <= RaidSessionRules.MaxParticipants;

    public static bool TryCreate(int value, out RaidTeamId teamId)
    {
        teamId = default;
        if (value < 1 || value > RaidSessionRules.MaxParticipants)
        {
            return false;
        }

        teamId = new RaidTeamId((byte)value);
        return true;
    }

    public bool Equals(RaidTeamId other) => _value == other._value;
    public override bool Equals(object obj) => obj is RaidTeamId other && Equals(other);
    public override int GetHashCode() => _value;
    public override string ToString() => IsValid ? _value.ToString() : "Invalid";
    public static bool operator ==(RaidTeamId left, RaidTeamId right) => left.Equals(right);
    public static bool operator !=(RaidTeamId left, RaidTeamId right) => !left.Equals(right);
}
