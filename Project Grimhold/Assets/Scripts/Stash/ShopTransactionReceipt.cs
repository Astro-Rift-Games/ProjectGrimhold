using System;

/// <summary>
/// Idempotency key for a locally committed shop transaction result.
/// </summary>
public readonly struct ShopTransactionReceipt : IEquatable<ShopTransactionReceipt>
{
    public ShopTransactionId TransactionId { get; }
    public ProfileId ProfileId { get; }

    public ShopTransactionReceipt(ShopTransactionId transactionId, ProfileId profileId)
    {
        TransactionId = transactionId;
        ProfileId = profileId;
    }

    public bool IsValid => TransactionId.IsValid && ProfileId.IsValid;

    public bool Equals(ShopTransactionReceipt other)
    {
        return TransactionId.Equals(other.TransactionId) && ProfileId == other.ProfileId;
    }

    public override bool Equals(object obj)
    {
        return obj is ShopTransactionReceipt other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(TransactionId, ProfileId);
    }
}
