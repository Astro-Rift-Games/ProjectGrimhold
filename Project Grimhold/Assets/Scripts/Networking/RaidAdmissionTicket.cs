using System;

/// <summary>
/// Lightweight, bounded connection ticket used for the initial UDP connection handshake.
/// Guaranteed to stay strictly under Photon Fusion's 128-byte ConnectionToken limit.
/// Full loadout and ability admission is submitted via reliable RPC once connected.
/// </summary>
public readonly struct RaidAdmissionTicket : IEquatable<RaidAdmissionTicket>
{
    public RaidCode RaidCode { get; }
    public ProfileId ProfileId { get; }
    public string ReservationId { get; }

    public bool IsValid =>
        RaidCode.IsValid &&
        ProfileId.IsValid &&
        !string.IsNullOrWhiteSpace(ReservationId);

    public RaidAdmissionTicket(
        RaidCode raidCode,
        ProfileId profileId,
        string reservationId)
    {
        RaidCode = raidCode;
        ProfileId = profileId;
        ReservationId = reservationId ?? string.Empty;
    }

    public static RaidAdmissionTicket FromAdmissionData(in RaidAdmissionData admissionData)
    {
        return new RaidAdmissionTicket(
            admissionData.RaidCode,
            admissionData.ProfileId,
            admissionData.ReservationId);
    }

    public PlayerJoinData ToPlayerJoinData()
    {
        return new PlayerJoinData(ProfileId);
    }

    public bool Equals(RaidAdmissionTicket other)
    {
        return RaidCode.Equals(other.RaidCode) &&
               ProfileId.Equals(other.ProfileId) &&
               string.Equals(ReservationId, other.ReservationId, StringComparison.Ordinal);
    }

    public override bool Equals(object obj)
    {
        return obj is RaidAdmissionTicket other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = RaidCode.GetHashCode();
            hash = (hash * 397) ^ ProfileId.GetHashCode();
            hash = (hash * 397) ^ (ReservationId != null ? StringComparer.Ordinal.GetHashCode(ReservationId) : 0);
            return hash;
        }
    }

    public static bool operator ==(RaidAdmissionTicket left, RaidAdmissionTicket right) => left.Equals(right);
    public static bool operator !=(RaidAdmissionTicket left, RaidAdmissionTicket right) => !left.Equals(right);

    public override string ToString() => $"[RaidAdmissionTicket RaidCode={RaidCode.Value} Profile={ProfileId.Value} Reservation={ReservationId}]";
}
