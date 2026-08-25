using System;
using System.Collections.Generic;

/// <summary>Immutable origin breakdown for one transferred LootId quantity.</summary>
public sealed class RaidLootOriginTransfer
{
    private static readonly RaidLootOriginTransfer EmptyTransfer = new(Array.Empty<RaidLootOriginBucket>());
    private readonly RaidLootOriginBucket[] _buckets;

    public RaidLootOriginTransfer(IReadOnlyList<RaidLootOriginBucket> buckets)
    {
        if (buckets == null)
        {
            throw new ArgumentNullException(nameof(buckets));
        }

        _buckets = new RaidLootOriginBucket[buckets.Count];
        for (int index = 0; index < buckets.Count; index++)
        {
            _buckets[index] = buckets[index];
        }
    }

    public IReadOnlyList<RaidLootOriginBucket> Buckets => _buckets;
    public int Count => _buckets.Length;
    public static RaidLootOriginTransfer Empty => EmptyTransfer;

    public bool TryGetTotal(out int total)
    {
        total = 0;
        var origins = new HashSet<RaidLootOrigin>();
        try
        {
            for (int index = 0; index < _buckets.Length; index++)
            {
                RaidLootOriginBucket bucket = _buckets[index];
                if (!bucket.IsValid || !origins.Add(bucket.Origin) ||
                    index > 0 && _buckets[index - 1].Origin.CompareTo(bucket.Origin) >= 0)
                {
                    total = 0;
                    return false;
                }

                total = checked(total + bucket.Amount);
            }
        }
        catch (OverflowException)
        {
            total = 0;
            return false;
        }

        return _buckets.Length > 0;
    }

    public static RaidLootOriginTransfer Dungeon(int amount) =>
        new(new[] { new RaidLootOriginBucket(RaidLootOrigin.Dungeon, amount) });

    public static bool TryCreate(RaidLootOrigin origin, int amount, out RaidLootOriginTransfer transfer)
    {
        transfer = null;
        if (!origin.IsValid || amount <= 0)
        {
            return false;
        }

        transfer = new RaidLootOriginTransfer(new[] { new RaidLootOriginBucket(origin, amount) });
        return true;
    }

    public static bool TryCreatePlayer(RaidParticipantId participantId, int amount, out RaidLootOriginTransfer transfer)
    {
        transfer = null;
        if (amount <= 0 || !RaidLootOrigin.TryCreatePlayer(participantId, out RaidLootOrigin origin))
        {
            return false;
        }

        transfer = new RaidLootOriginTransfer(new[] { new RaidLootOriginBucket(origin, amount) });
        return true;
    }
}
