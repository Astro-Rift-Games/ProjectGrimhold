using System;
using Fusion;
using UnityEngine;

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

        IRaidLootOriginSource originSource = source as IRaidLootOriginSource;
        IRaidLootOriginReceiver originDestination = destination as IRaidLootOriginReceiver;
        RaidLootOriginTransfer originTransfer = null;
        bool sourceIsRaidAware = originSource?.IsRaidLootOriginAware == true;
        bool destinationIsRaidAware = originDestination?.IsRaidLootOriginAware == true;
        if (sourceIsRaidAware != destinationIsRaidAware)
        {
            throw new InvalidOperationException(
                "Raid loot provenance requires both transfer endpoints to expose the Raid origin contract.");
        }

        if (sourceIsRaidAware)
        {
            if (!originSource.TryResolveRaidLootOriginTransfer(request, out originTransfer) ||
                originTransfer == null || !originTransfer.TryGetTotal(out int originTotal) ||
                originTotal != request.RequestedAmount)
            {
                throw new InvalidOperationException(
                    "Raid loot provenance could not resolve the already validated extraction.");
            }

            failure = originDestination.ValidateRaidLootOriginReceive(request, originTransfer);
            if (failure != LootTransferFailureReason.None)
            {
                return LootTransferResult.Rejected(failure);
            }
        }

        if (sourceIsRaidAware)
        {
            originSource.CommitRaidLootExtraction(request, originTransfer);
            originDestination.CommitRaidLootReceive(request, originTransfer);
        }
        else
        {
            source.CommitExtraction(request);
            destination.CommitReceive(request);
        }

        TransferUtilityCooldownIfApplicable(source, destination, in request);
        firstAcquisition = resolvedAcquisition;
        return LootTransferResult.Succeeded(request);
    }

    private static void TransferUtilityCooldownIfApplicable(
        ILootExtractor source,
        ILootReceiver destination,
        in LootTransferRequest request)
    {
        IUtilityCooldownProvider sourceProvider = ResolveUtilityCooldownProvider(source);
        if (sourceProvider == null)
        {
            return;
        }

        TickTimer cooldown = sourceProvider.GetUtilityCooldown(request.LootId);
        NetworkRunner runner = ResolveRunner(source, destination);
        bool hasActiveCooldown = runner != null
            ? !cooldown.ExpiredOrNotRunning(runner)
            : cooldown.IsRunning;

        if (hasActiveCooldown)
        {
            IUtilityCooldownReceiver destinationReceiver = ResolveUtilityCooldownReceiver(destination);
            destinationReceiver?.SetUtilityCooldown(request.LootId, cooldown);
        }

        if (source is ILootQuantityReader quantityReader &&
            quantityReader.GetLootAmount(request.LootId) <= 0)
        {
            sourceProvider.ClearUtilityCooldown(request.LootId);
        }
    }

    private static IUtilityCooldownProvider ResolveUtilityCooldownProvider(ILootExtractor source)
    {
        if (source is IUtilityCooldownProvider directProvider)
        {
            return directProvider;
        }

        if (source is Component component && component != null &&
            component.TryGetComponent(out IUtilityCooldownProvider siblingProvider))
        {
            return siblingProvider;
        }

        return null;
    }

    private static IUtilityCooldownReceiver ResolveUtilityCooldownReceiver(ILootReceiver destination)
    {
        if (destination is IUtilityCooldownReceiver directReceiver)
        {
            return directReceiver;
        }

        if (destination is Component component && component != null &&
            component.TryGetComponent(out IUtilityCooldownReceiver siblingReceiver))
        {
            return siblingReceiver;
        }

        return null;
    }

    private static NetworkRunner ResolveRunner(ILootExtractor source, ILootReceiver destination)
    {
        if (source is NetworkBehaviour sourceBehaviour && sourceBehaviour != null && sourceBehaviour.Runner != null)
        {
            return sourceBehaviour.Runner;
        }

        if (destination is NetworkBehaviour destinationBehaviour && destinationBehaviour != null && destinationBehaviour.Runner != null)
        {
            return destinationBehaviour.Runner;
        }

        return null;
    }
}
