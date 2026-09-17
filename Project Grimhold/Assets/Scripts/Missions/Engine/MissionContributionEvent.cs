using System;

/// <summary>
/// A pure, immutable event representing a contribution to a mission objective.
/// </summary>
public readonly struct MissionContributionEvent : IEquatable<MissionContributionEvent>
{
    public readonly ObjectiveFamily Family;
    public readonly int Amount;
    public readonly string TargetId;
    public readonly string ZoneId;
    public readonly ProfileId OwnerId;
    public readonly bool IsCompanionContribution;

    public MissionContributionEvent(
        ObjectiveFamily family,
        int amount,
        ProfileId ownerId,
        string targetId = null,
        string zoneId = null,
        bool isCompanionContribution = false)
    {
        Family = family;
        Amount = Math.Max(0, amount);
        OwnerId = ownerId;
        TargetId = string.IsNullOrWhiteSpace(targetId) ? null : targetId;
        ZoneId = string.IsNullOrWhiteSpace(zoneId) ? null : zoneId;
        IsCompanionContribution = isCompanionContribution;
    }

    public bool Equals(MissionContributionEvent other)
    {
        return Family == other.Family &&
               Amount == other.Amount &&
               string.Equals(TargetId, other.TargetId, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(ZoneId, other.ZoneId, StringComparison.OrdinalIgnoreCase) &&
               OwnerId.Equals(other.OwnerId) &&
               IsCompanionContribution == other.IsCompanionContribution;
    }

    public override bool Equals(object obj) => obj is MissionContributionEvent other && Equals(other);
    public override int GetHashCode() => HashCode.Combine((int)Family, Amount, TargetId?.ToLowerInvariant(), ZoneId?.ToLowerInvariant(), OwnerId, IsCompanionContribution);
}
