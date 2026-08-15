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
    public const int MaxAppliedShopTransactionReceipts = 256;
    public const long InitialCurrency = 0L;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ProfileId ProfileId { get; set; }
    public long Currency { get; set; } = InitialCurrency;
    public List<StashItem> Stash { get; } = new();
    public List<StashItem> Loadout { get; } = new();
    public PendingLoadoutReservation PendingReservation { get; set; }
    public List<ExtractionReceipt> AppliedExtractionReceipts { get; } = new();
    public long ShopIdempotencyWatermark { get; set; } = 0;
    public List<ShopTransactionReceipt> AppliedShopTransactionReceipts { get; } = new();

    public LocalProfileSnapshot Clone()
    {
        var clone = new LocalProfileSnapshot
        {
            SchemaVersion = SchemaVersion,
            ProfileId = ProfileId,
            Currency = Currency,
            PendingReservation = PendingReservation?.Clone(),
            ShopIdempotencyWatermark = ShopIdempotencyWatermark
        };

        clone.Stash.AddRange(Stash);
        clone.Loadout.AddRange(Loadout);
        clone.AppliedExtractionReceipts.AddRange(AppliedExtractionReceipts);
        clone.AppliedShopTransactionReceipts.AddRange(AppliedShopTransactionReceipts);
        return clone;
    }
}
