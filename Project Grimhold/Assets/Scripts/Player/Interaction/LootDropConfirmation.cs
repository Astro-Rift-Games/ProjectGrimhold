using System;

/// <summary>
/// Confirmed authoritative outcome reconstructed on the requesting peer.
/// </summary>
public readonly struct LootDropConfirmation
{
    public uint RequestSequence { get; }
    public int CatalogIndex { get; }
    public int SimulationTick { get; }
    public LootDropResult Result { get; }
    public LootId? ResolvedLootId { get; }

    public LootDropConfirmation(
        uint requestSequence,
        int catalogIndex,
        int simulationTick,
        in LootDropResult result,
        LootId? resolvedLootId)
    {
        RequestSequence = requestSequence;
        CatalogIndex = catalogIndex;
        SimulationTick = simulationTick;
        Result = result;
        ResolvedLootId = resolvedLootId;
    }

    public static bool TryReconstruct(
        uint requestSequence,
        int catalogIndex,
        int droppedAmount,
        bool success,
        int failureReasonValue,
        int simulationTick,
        in LootDropRequestIdentity expected,
        LootDefinitionCatalog catalog,
        out LootDropConfirmation confirmation,
        out string error)
    {
        confirmation = default;
        error = null;
        if (requestSequence != expected.RequestSequence || catalogIndex != expected.CatalogIndex)
        {
            error = "Envelope identity does not match the local drop request.";
            return false;
        }

        LootId? resolvedLootId = null;
        if (catalog != null && catalog.TryGetByIndex(catalogIndex, out LootDefinition definition))
        {
            resolvedLootId = definition.LootId;
        }

        LootDropResult result;
        if (success)
        {
            if (droppedAmount <= 0 || failureReasonValue != (int)LootDropFailureReason.None ||
                !resolvedLootId.HasValue ||
                expected.QuantityMode == LootTransferQuantityMode.SingleUnit && droppedAmount != 1)
            {
                error = "A successful drop has an invalid amount, reason or loot definition.";
                return false;
            }

            result = LootDropResult.Succeeded(droppedAmount);
        }
        else
        {
            if (droppedAmount != 0 ||
                !Enum.IsDefined(typeof(LootDropFailureReason), failureReasonValue))
            {
                error = "A rejected drop has a malformed reason or amount.";
                return false;
            }

            try
            {
                result = LootDropResult.Rejected((LootDropFailureReason)failureReasonValue);
            }
            catch (ArgumentOutOfRangeException)
            {
                error = "The drop failure reason cannot represent a rejection.";
                return false;
            }
        }

        confirmation = new LootDropConfirmation(
            requestSequence,
            catalogIndex,
            simulationTick,
            result,
            resolvedLootId);
        return true;
    }
}
