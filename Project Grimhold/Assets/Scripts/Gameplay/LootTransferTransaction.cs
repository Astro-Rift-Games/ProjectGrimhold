using System;

/// <summary>
/// Executes an atomic, prevalidated full-stack transfer between two loot capabilities.
/// Resolution, range checks, presentation and callbacks remain outside this transaction.
/// </summary>
public static class LootTransferTransaction
{
    /// <summary>
    /// Validates both endpoints and commits them in extraction-then-reception order.
    /// Contract exceptions raised by commits are intentionally not converted to gameplay rejections.
    /// </summary>
    public static LootTransferResult Execute(
        ILootExtractor source,
        ILootReceiver destination,
        in LootTransferRequest request)
    {
        return Execute(source, destination, request, out _);
    }

    /// <summary>
    /// Executes the existing atomic transfer while exposing authoritative first-acquisition
    /// provenance separately from the unchanged request.
    /// </summary>
    public static LootTransferResult Execute(
        ILootExtractor source,
        ILootReceiver destination,
        in LootTransferRequest request,
        out LootFirstAcquisitionResult firstAcquisition)
    {
        firstAcquisition = LootFirstAcquisitionResult.None;
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (destination == null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        LootTransferFailureReason failure = source.ValidateExtraction(request);
        if (failure != LootTransferFailureReason.None)
        {
            return LootTransferResult.Rejected(failure);
        }

        LootFirstAcquisitionResult resolvedAcquisition = source is ILootFirstAcquisitionSource provenanceSource
            ? provenanceSource.ResolveFirstAcquisition(request)
            : LootFirstAcquisitionResult.None;
        if (!resolvedAcquisition.IsValidFor(request))
        {
            throw new InvalidOperationException(
                $"{nameof(ILootFirstAcquisitionSource)} returned an amount outside the validated request range.");
        }

        failure = destination.ValidateReceive(request);
        if (failure != LootTransferFailureReason.None)
        {
            return LootTransferResult.Rejected(failure);
        }

        source.CommitExtraction(request);
        destination.CommitReceive(request);
        firstAcquisition = resolvedAcquisition;
        return LootTransferResult.Succeeded(request);
    }
}
