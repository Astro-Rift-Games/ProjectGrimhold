using System;

/// <summary>Immutable logical origin of one Raid loot quantity.</summary>
public readonly struct RaidLootOrigin : IEquatable<RaidLootOrigin>, IComparable<RaidLootOrigin>
{
    private RaidLootOrigin(bool isDungeon, RaidParticipantId playerParticipantId)
    {
        IsDungeon = isDungeon;
        PlayerParticipantId = playerParticipantId;
    }

    public bool IsDungeon { get; }
    public bool IsPlayer => !IsDungeon && PlayerParticipantId.IsValid;
    public RaidParticipantId PlayerParticipantId { get; }
    public bool IsValid => IsDungeon || IsPlayer;

    public static RaidLootOrigin Dungeon => new(true, default);

    public static bool TryCreatePlayer(RaidParticipantId participantId, out RaidLootOrigin origin)
    {
        origin = default;
        if (!participantId.IsValid)
        {
            return false;
        }

        origin = new RaidLootOrigin(false, participantId);
        return true;
    }

    public int CompareTo(RaidLootOrigin other)
    {
        if (IsDungeon != other.IsDungeon)
        {
            return IsDungeon ? -1 : 1;
        }

        return IsDungeon
            ? 0
            : PlayerParticipantId.CompareTo(other.PlayerParticipantId);
    }

    public bool Equals(RaidLootOrigin other) =>
        IsDungeon == other.IsDungeon && PlayerParticipantId == other.PlayerParticipantId;

    public override bool Equals(object obj) => obj is RaidLootOrigin other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(IsDungeon, PlayerParticipantId);
    public static bool operator ==(RaidLootOrigin left, RaidLootOrigin right) => left.Equals(right);
    public static bool operator !=(RaidLootOrigin left, RaidLootOrigin right) => !left.Equals(right);
}
