using System.Collections.Generic;

/// <summary>
/// Complete mutable-in-memory representation of one local profile.
/// Instances are cloned before a domain mutation is committed.
/// </summary>
public sealed class LocalProfileSnapshot
{
    public const int CurrentSchemaVersion = 2;
    public const int MaxLoadoutSlots = 16;
    public const int MaxAppliedExtractionReceipts = 256;
    public const int MaxAppliedShopTransactionReceipts = 256;
    public const int MaxAppliedProgressionReceipts = 256;
    public const long InitialCurrency = 0L;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public ProfileId ProfileId { get; set; }
    public long Currency { get; set; } = InitialCurrency;
    public int Level { get; set; } = ExperienceCurve.InitialLevel;
    public long CurrentExperience { get; set; }
    public CharacterAttributeState CharacterAttributes { get; set; } =
        ProgressionBalanceDefaults.InitialCharacterAttributeState;
    public int LastAppliedProgressionResultSequence { get; set; }
    public ProgressionReceipt? LastProgressionReceipt { get; set; }
    public List<StashItem> Stash { get; } = new();
    public List<StashItem> Loadout { get; } = new();
    public PreparedEquipmentLoadout PreparedEquipment { get; set; }
    public PendingLoadoutReservation PendingReservation { get; set; }
    public PendingExtractionCommit PendingExtractionCommit { get; set; }
    public List<ExtractionReceipt> AppliedExtractionReceipts { get; } = new();
    public long ShopIdempotencyWatermark { get; set; } = 0;
    public List<ShopTransactionReceipt> AppliedShopTransactionReceipts { get; } = new();
    public List<ProgressionReceipt> AppliedProgressionReceipts { get; } = new();

    public LocalProfileSnapshot Clone()
    {
        var clone = new LocalProfileSnapshot
        {
            SchemaVersion = SchemaVersion,
            ProfileId = ProfileId,
            Currency = Currency,
            Level = Level,
            CurrentExperience = CurrentExperience,
            CharacterAttributes = CharacterAttributes,
            LastAppliedProgressionResultSequence = LastAppliedProgressionResultSequence,
            LastProgressionReceipt = LastProgressionReceipt,
            PreparedEquipment = PreparedEquipment,
            PendingReservation = PendingReservation?.Clone(),
            PendingExtractionCommit = PendingExtractionCommit,
            ShopIdempotencyWatermark = ShopIdempotencyWatermark
        };

        clone.Stash.AddRange(Stash);
        clone.Loadout.AddRange(Loadout);
        clone.AppliedExtractionReceipts.AddRange(AppliedExtractionReceipts);
        clone.AppliedShopTransactionReceipts.AddRange(AppliedShopTransactionReceipts);
        clone.AppliedProgressionReceipts.AddRange(AppliedProgressionReceipts);
        return clone;
    }
}
