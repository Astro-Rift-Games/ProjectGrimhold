using System;
using System.Collections.Generic;
using Fusion;

public enum MerchantTradeRecordStatus
{
    Free = 0,

    /// <summary>
    /// Ticket issued; its purchase units stay reserved until the requester reports an outcome.
    /// Time or departure never release it, because a persisted outcome may still arrive.
    /// </summary>
    Pending = 1,

    /// <summary>The requester persisted the ticket. Kept only to deduplicate late retries.</summary>
    Committed = 2,

    /// <summary>
    /// The requester reported the ticket as not persisted, which guarantees it is not persisted
    /// later without a new reservation: an identical retry reserves again under the same
    /// transaction id.
    /// </summary>
    Released = 3
}

/// <summary>
/// Replicated State Authority record of one trade request, keyed by <c>ProfileId + RequestId</c>.
/// It keeps the authority-minted transaction id and a hash of the requested lines so a retry can
/// be recognized and answered with the same ticket; priced lines are rebuilt from static economy
/// configuration and stock effects live in <see cref="MerchantTradeStockLine"/>.
/// </summary>
public struct MerchantTradeRecord : INetworkStruct
{
    public int Status;
    public long ProfileKey;
    public long RequestIdLow;
    public long RequestIdHigh;
    public long PayloadHash;
    public long TransactionTimestamp;
    public long TransactionGuidLow;
    public long TransactionGuidHigh;

    public MerchantTradeRecordStatus RecordStatus => (MerchantTradeRecordStatus)Status;
    public bool IsOccupied => RecordStatus != MerchantTradeRecordStatus.Free;
    public bool IsPending => RecordStatus == MerchantTradeRecordStatus.Pending;

    public Guid RequestId => JoinGuid(RequestIdLow, RequestIdHigh);
    public ShopTransactionId TransactionId => new ShopTransactionId(TransactionTimestamp, JoinGuid(TransactionGuidLow, TransactionGuidHigh));

    public bool IsPendingFor(long profileKey) => IsPending && ProfileKey == profileKey;

    public bool Matches(long profileKey, Guid requestId)
    {
        SplitGuid(requestId, out long low, out long high);
        return IsOccupied && ProfileKey == profileKey && RequestIdLow == low && RequestIdHigh == high;
    }

    public MerchantTradeRecord WithStatus(MerchantTradeRecordStatus status)
    {
        MerchantTradeRecord copy = this;
        copy.Status = (int)status;
        return copy;
    }

    public static MerchantTradeRecord CreatePending(
        long profileKey,
        Guid requestId,
        long payloadHash,
        ShopTransactionId transactionId)
    {
        SplitGuid(requestId, out long requestLow, out long requestHigh);
        SplitGuid(transactionId.Value, out long transactionLow, out long transactionHigh);
        return new MerchantTradeRecord
        {
            Status = (int)MerchantTradeRecordStatus.Pending,
            ProfileKey = profileKey,
            RequestIdLow = requestLow,
            RequestIdHigh = requestHigh,
            PayloadHash = payloadHash,
            TransactionTimestamp = transactionId.Timestamp,
            TransactionGuidLow = transactionLow,
            TransactionGuidHigh = transactionHigh
        };
    }

    /// <summary>Deterministic 64-bit key of a profile, identical on every peer.</summary>
    public static long HashProfile(ProfileId profileId)
    {
        ulong hash = FnvOffset;
        hash = Mix(hash, profileId.Value);
        return (long)hash;
    }

    /// <summary>Deterministic hash of the requested lines, in order and per side.</summary>
    public static long HashPayload(IReadOnlyList<MerchantTradeLine> purchases, IReadOnlyList<MerchantTradeLine> sales)
    {
        ulong hash = FnvOffset;
        hash = MixLines(hash, purchases);
        hash = MixLines(hash, sales);
        return (long)hash;
    }

    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    private static ulong MixLines(ulong hash, IReadOnlyList<MerchantTradeLine> lines)
    {
        hash = Mix(hash, lines.Count);
        foreach (MerchantTradeLine line in lines)
        {
            hash = Mix(hash, line.LootId.Value);
            hash = Mix(hash, line.Amount);
        }

        return hash;
    }

    private static ulong Mix(ulong hash, string value)
    {
        string text = value ?? string.Empty;
        hash = Mix(hash, text.Length);
        foreach (char character in text)
        {
            hash = (hash ^ character) * FnvPrime;
        }

        return hash;
    }

    private static ulong Mix(ulong hash, int value)
    {
        for (int shift = 0; shift < 32; shift += 8)
        {
            hash = (hash ^ (byte)(value >> shift)) * FnvPrime;
        }

        return hash;
    }

    private static void SplitGuid(Guid value, out long low, out long high)
    {
        byte[] bytes = value.ToByteArray();
        low = BitConverter.ToInt64(bytes, 0);
        high = BitConverter.ToInt64(bytes, 8);
    }

    private static Guid JoinGuid(long low, long high)
    {
        var bytes = new byte[16];
        BitConverter.TryWriteBytes(new Span<byte>(bytes, 0, 8), low);
        BitConverter.TryWriteBytes(new Span<byte>(bytes, 8, 8), high);
        return new Guid(bytes);
    }
}
