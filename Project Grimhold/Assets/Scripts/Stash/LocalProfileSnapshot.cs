using System.Collections.Generic;

/// <summary>
/// Complete mutable-in-memory representation of one local profile.
/// Instances are cloned before a domain mutation is committed.
/// </summary>
public sealed class LocalProfileSnapshot
{
    public const int CurrentSchemaVersion = 1;
    public const int MaxLoadoutSlots = 16;
    public const int MaxAppliedExtractionReceipts = 256;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ProfileId ProfileId { get; set; }
    public List<StashItem> Stash { get; } = new();
    public List<StashItem> Loadout { get; } = new();
    public PendingLoadoutReservation PendingReservation { get; set; }
    public List<ExtractionReceipt> AppliedExtractionReceipts { get; } = new();

    public LocalProfileSnapshot Clone()
    {
        var clone = new LocalProfileSnapshot
        {
            SchemaVersion = SchemaVersion,
            ProfileId = ProfileId,
            PendingReservation = PendingReservation?.Clone()
        };

        clone.Stash.AddRange(Stash);
        clone.Loadout.AddRange(Loadout);
        clone.AppliedExtractionReceipts.AddRange(AppliedExtractionReceipts);
        return clone;
    }
}
