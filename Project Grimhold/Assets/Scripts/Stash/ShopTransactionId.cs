using System;

/// <summary>
/// A strongly-typed identity for shop transactions, generated deterministically by State Authority.
/// The timestamp allows for efficient O(1) low-watermark pruning of applied transactions,
/// ensuring absolute idempotency even when old receipts are evicted from limited history.
/// <see cref="ToString"/> is also the backend wire format (<c>{unix ms}_{32 hex}</c>): the backend
/// rejects an id at or below its retention watermark with <c>TRANSACTION_RETENTION_EXCEEDED</c>
/// instead of applying an evicted transaction a second time.
/// </summary>
public readonly struct ShopTransactionId : IEquatable<ShopTransactionId>
{
    public readonly long Timestamp; // Unix time milliseconds
    public readonly Guid Value;

    public ShopTransactionId(long timestamp, Guid value)
    {
        Timestamp = timestamp;
        Value = value;
    }

    public bool IsValid => Timestamp > 0 && Value != Guid.Empty;

    public bool Equals(ShopTransactionId other)
    {
        return Timestamp == other.Timestamp && Value.Equals(other.Value);
    }

    public override bool Equals(object obj)
    {
        return obj is ShopTransactionId other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Timestamp, Value);
    }
    
    public override string ToString()
    {
        return $"{Timestamp}_{Value:N}";
    }
}
