using System.Collections.Generic;

/// <summary>
/// Presentation messages of the merchant trade. It picks the main block of an already computed
/// <see cref="MerchantShopViewModel"/> and names it for the player; it decides no rule and the
/// messages are never read back as domain state.
/// </summary>
public static class MerchantTradeFeedback
{
    // Most actionable first: the in-flight state explains every other disabled control.
    private static readonly MerchantTradeBlockReason[] Priority =
    {
        MerchantTradeBlockReason.SubmissionInFlight,
        MerchantTradeBlockReason.InsufficientCurrency,
        MerchantTradeBlockReason.CapacityExceeded,
        MerchantTradeBlockReason.InsufficientStock,
        MerchantTradeBlockReason.InsufficientOwnedUnits,
        MerchantTradeBlockReason.UnknownItem,
        MerchantTradeBlockReason.NotPurchasable,
        MerchantTradeBlockReason.Overflow,
        MerchantTradeBlockReason.EmptyDraft
    };

    /// <summary>The block shown to the player, when the preview has any.</summary>
    public static bool TryGetPrimaryBlock(IReadOnlyList<MerchantTradeBlock> blocks, out MerchantTradeBlock primary)
    {
        primary = default;
        if (blocks == null)
        {
            return false;
        }

        foreach (MerchantTradeBlockReason reason in Priority)
        {
            foreach (MerchantTradeBlock block in blocks)
            {
                if (block.Reason == reason)
                {
                    primary = block;
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Message of the main block of the view model, or empty when Confirm is allowed.</summary>
    public static string DescribePrimaryBlock(MerchantShopViewModel viewModel)
    {
        if (viewModel == null || !viewModel.IsAvailable)
        {
            return "Comercio no disponible";
        }

        return TryGetPrimaryBlock(viewModel.Blocks, out MerchantTradeBlock block)
            ? Describe(block, viewModel)
            : string.Empty;
    }

    public static string Describe(MerchantTradeBlock block, MerchantShopViewModel viewModel)
    {
        string item = FindItemName(block, viewModel);
        return block.Reason switch
        {
            MerchantTradeBlockReason.SubmissionInFlight => "Transacción en curso...",
            MerchantTradeBlockReason.InsufficientCurrency =>
                $"Oro insuficiente: faltan {viewModel?.CurrencyShortfall ?? 0}",
            MerchantTradeBlockReason.CapacityExceeded => "Inventario lleno",
            MerchantTradeBlockReason.InsufficientStock => WithItem("Stock insuficiente", item),
            MerchantTradeBlockReason.InsufficientOwnedUnits => WithItem("Unidades no disponibles", item),
            MerchantTradeBlockReason.UnknownItem => WithItem("Objeto inválido", item),
            MerchantTradeBlockReason.NotPurchasable => WithItem("Objeto no disponible para compra", item),
            MerchantTradeBlockReason.Overflow => "Valores fuera de rango",
            MerchantTradeBlockReason.EmptyDraft => "Agrega objetos a la transacción",
            _ => "Transacción no disponible"
        };
    }

    public static string DescribeResult(MerchantTransactionResult result) => result switch
    {
        MerchantTransactionResult.Success => "Transacción completada",
        MerchantTransactionResult.AlreadyApplied => "Transacción ya aplicada",
        MerchantTransactionResult.RejectedByMerchant => "El comerciante rechazó la transacción",
        MerchantTransactionResult.RejectedByProfile => "Transacción rechazada: revisa Oro, objetos y espacio",
        MerchantTransactionResult.PersistenceFailed => "No se pudo guardar la transacción",
        MerchantTransactionResult.SubmissionFailed => "No se pudo enviar la transacción",
        MerchantTransactionResult.NoResponse => "Sin respuesta del comerciante; puedes reintentar",
        _ => string.Empty
    };

    private static string WithItem(string message, string item) =>
        string.IsNullOrEmpty(item) ? message : $"{message}: {item}";

    private static string FindItemName(MerchantTradeBlock block, MerchantShopViewModel viewModel)
    {
        if (viewModel == null || !block.LootId.IsValid)
        {
            return string.Empty;
        }

        // Purchase blocks name merchant rows and sale blocks Inventory rows.
        bool fromStock = block.Reason != MerchantTradeBlockReason.InsufficientOwnedUnits;
        IReadOnlyList<MerchantShopRowViewModel> rows = fromStock ? viewModel.MerchantRows : viewModel.InventoryRows;
        foreach (MerchantShopRowViewModel row in rows)
        {
            if (row.LootId == block.LootId)
            {
                return row.Definition != null && !string.IsNullOrWhiteSpace(row.Definition.DisplayName)
                    ? row.Definition.DisplayName
                    : row.LootId.Value;
            }
        }

        return block.LootId.Value;
    }
}
