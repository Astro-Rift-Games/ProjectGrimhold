using System;

/// <summary>
/// Immutable result of a complete loot movement attempt.
/// Partial successes cannot be represented through its public factories.
/// </summary>
public readonly struct LootTransferResult
{
    public bool Success { get; }
    public int TransferredAmount { get; }
    public LootTransferFailureReason FailureReason { get; }

    public bool IsValid => Success
        ? FailureReason == LootTransferFailureReason.None && TransferredAmount > 0
        : IsRejectionReason(FailureReason) && TransferredAmount == 0;

    private LootTransferResult(
        bool success,
        int transferredAmount,
        LootTransferFailureReason failureReason)
    {
        Success = success;
        TransferredAmount = transferredAmount;
        FailureReason = failureReason;
    }

    /// <summary>
    /// Creates a successful result for the request's complete quantity.
    /// </summary>
    public static LootTransferResult Succeeded(in LootTransferRequest request)
    {
        if (!request.IsValid)
        {
            throw new ArgumentException("A successful loot transfer requires a valid request.", nameof(request));
        }

        return new LootTransferResult(
            true,
            request.RequestedAmount,
            LootTransferFailureReason.None);
    }

    /// <summary>
    /// Creates a rejected result with no transferred quantity.
    /// </summary>
    public static LootTransferResult Rejected(LootTransferFailureReason reason)
    {
        if (!IsRejectionReason(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "A rejection requires a defined failure reason.");
        }

        return new LootTransferResult(false, 0, reason);
    }

    private static bool IsRejectionReason(LootTransferFailureReason reason)
    {
        switch (reason)
        {
            case LootTransferFailureReason.InvalidLoot:
            case LootTransferFailureReason.InvalidAmount:
            case LootTransferFailureReason.SourceNotFound:
            case LootTransferFailureReason.DestinationNotFound:
            case LootTransferFailureReason.InsufficientAmount:
            case LootTransferFailureReason.InventoryFull:
            case LootTransferFailureReason.OutOfRange:
            case LootTransferFailureReason.MissingAuthority:
            case LootTransferFailureReason.ContainerUnavailable:
            case LootTransferFailureReason.Overflow:
            case LootTransferFailureReason.PlayerUnavailable:
                return true;
            default:
                return false;
        }
    }
}
