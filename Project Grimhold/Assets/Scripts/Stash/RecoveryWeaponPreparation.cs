/// <summary>Immutable inspection bound to one store and aggregate revision, never a global consent flag.</summary>
public sealed class RecoveryWeaponPreparation
{
    public enum Source { Prepared, Loadout, Stash, NewGrant }
    private readonly LocalProfileStore _owner;
    private readonly LocalProfileSnapshot _snapshot;
    public ProfileId ProfileId { get; }
    public int RemoteRevision { get; }
    public Source WeaponSource { get; }
    public bool RequiresConsent => WeaponSource == Source.NewGrant;

    internal RecoveryWeaponPreparation(LocalProfileStore owner, LocalProfileSnapshot snapshot, Source source)
    {
        _owner = owner;
        _snapshot = snapshot;
        ProfileId = snapshot.ProfileId;
        RemoteRevision = snapshot.RemoteRevision;
        WeaponSource = source;
    }

    internal bool Matches(LocalProfileStore owner, LocalProfileSnapshot snapshot) =>
        ReferenceEquals(_owner, owner) && ReferenceEquals(_snapshot, snapshot) &&
        snapshot != null && snapshot.ProfileId == ProfileId && snapshot.RemoteRevision == RemoteRevision;
}
