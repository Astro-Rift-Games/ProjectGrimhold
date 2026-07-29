using System;

/// <summary>
/// Integration-layer confirmation retaining its primitive envelope even when local loot metadata is unavailable.
/// </summary>
public readonly struct LootTransferConfirmation
{
    public uint RequestSequence { get; }
    public EntityId SourceId { get; }
    public EntityId DestinationId { get; }
    public int CatalogIndex { get; }
    public int SimulationTick { get; }
    public LootTransferResult Result { get; }
    public LootId? ResolvedLootId { get; }

    public LootTransferConfirmation(
        uint requestSequence,
        EntityId sourceId,
        EntityId destinationId,
        int catalogIndex,
        int simulationTick,
        in LootTransferResult result,
        LootId? resolvedLootId)
    {
        RequestSequence = requestSequence;
        SourceId = sourceId;
        DestinationId = destinationId;
        CatalogIndex = catalogIndex;
        SimulationTick = simulationTick;
        Result = result;
        ResolvedLootId = resolvedLootId;
    }

    /// <summary>
    /// Validates a primitive transport envelope and reconstructs adapter/domain values when possible.
    /// </summary>
    public static bool TryReconstruct(
        uint sequence,
        int sourceIdValue,
        int destinationIdValue,
        int catalogIndex,
        int transferredAmount,
        bool success,
        int failureReasonValue,
        int simulationTick,
        in LootTransferRequestIdentity expected,
        EntityId localPlayerId,
        LootDefinitionCatalog catalog,
        out LootTransferConfirmation confirmation,
        out string error)
    {
        confirmation = default;
        error = null;
        EntityId sourceId = new EntityId(sourceIdValue);
        EntityId destinationId = new EntityId(destinationIdValue);

        if (sourceId.Value == 0 || destinationId.Value == 0 || sourceId == destinationId ||
            sourceId != expected.SourceId || destinationId != expected.DestinationId ||
            catalogIndex != expected.CatalogIndex ||
            (sourceId != localPlayerId && destinationId != localPlayerId))
        {
            error = "Envelope identity does not match the local request.";
            return false;
        }

        LootId? resolvedLootId = null;
        if (catalog != null && catalog.TryGetByIndex(catalogIndex, out LootDefinition definition))
        {
            resolvedLootId = definition.LootId;
        }

        LootTransferResult result;
        if (success)
        {
            if (transferredAmount <= 0 || failureReasonValue != (int)LootTransferFailureReason.None || !resolvedLootId.HasValue)
            {
                error = "A success requires positive amount, None and resolvable loot metadata.";
                return false;
            }

            var request = new LootTransferRequest(
                sourceId,
                destinationId,
                resolvedLootId.Value,
                transferredAmount,
                simulationTick);
            result = LootTransferResult.Succeeded(request);
        }
        else
        {
            if (transferredAmount != 0 || !Enum.IsDefined(typeof(LootTransferFailureReason), failureReasonValue))
            {
                error = "A rejection requires zero amount and a known failure reason.";
                return false;
            }

            try
            {
                result = LootTransferResult.Rejected((LootTransferFailureReason)failureReasonValue);
            }
            catch (ArgumentOutOfRangeException)
            {
                error = "The failure reason cannot represent a domain rejection.";
                return false;
            }
        }

        confirmation = new LootTransferConfirmation(
            sequence,
            sourceId,
            destinationId,
            catalogIndex,
            simulationTick,
            result,
            resolvedLootId);
        return true;
    }
}
