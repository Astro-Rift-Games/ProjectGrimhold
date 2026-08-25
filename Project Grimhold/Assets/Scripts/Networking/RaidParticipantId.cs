using System;
using Fusion;

/// <summary>
/// Stable Raid-scoped identity assigned by State Authority from the frozen admission cohort.
/// Zero is invalid; values 1..16 identify participants for the lifetime of the expedition.
/// </summary>
public struct RaidParticipantId : INetworkStruct, IEquatable<RaidParticipantId>, IComparable<RaidParticipantId>
{
    private byte _value;

    private RaidParticipantId(byte value) => _value = value;

    public int Value => _value;
    public bool IsValid => _value >= 1 && _value <= RaidSessionRules.MaxParticipants;

    public static bool TryCreate(int value, out RaidParticipantId participantId)
    {
        participantId = default;
        if (value < 1 || value > RaidSessionRules.MaxParticipants)
        {
            return false;
        }

        participantId = new RaidParticipantId((byte)value);
        return true;
    }

    public int CompareTo(RaidParticipantId other) => _value.CompareTo(other._value);
    public bool Equals(RaidParticipantId other) => _value == other._value;
    public override bool Equals(object obj) => obj is RaidParticipantId other && Equals(other);
    public override int GetHashCode() => _value;
    public override string ToString() => IsValid ? _value.ToString() : "Invalid";
    public static bool operator ==(RaidParticipantId left, RaidParticipantId right) => left.Equals(right);
    public static bool operator !=(RaidParticipantId left, RaidParticipantId right) => !left.Equals(right);
}
