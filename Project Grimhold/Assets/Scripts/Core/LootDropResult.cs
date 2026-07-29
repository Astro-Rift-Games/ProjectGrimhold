using System;

/// <summary>
/// Complete authoritative outcome of one inventory-to-world drop request.
/// </summary>
public readonly struct LootDropResult
{
    public bool Success { get; }
    public int DroppedAmount { get; }
    public LootDropFailureReason FailureReason { get; }

    private LootDropResult(bool success, int droppedAmount, LootDropFailureReason failureReason)
    {
        Success = success;
        DroppedAmount = droppedAmount;
        FailureReason = failureReason;
    }

    public static LootDropResult Succeeded(int droppedAmount)
    {
        if (droppedAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(droppedAmount));
        }

        return new LootDropResult(true, droppedAmount, LootDropFailureReason.None);
    }

    public static LootDropResult Rejected(LootDropFailureReason reason)
    {
        if (reason == LootDropFailureReason.None ||
            reason == LootDropFailureReason.Uninitialized ||
            !Enum.IsDefined(typeof(LootDropFailureReason), reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        return new LootDropResult(false, 0, reason);
    }
}
