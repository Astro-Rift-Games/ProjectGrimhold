using System.Collections.Generic;
using Fusion;

/// <summary>Requested line on the wire: catalog index and amount. Clients never send prices.</summary>
public struct MerchantTradeRequestLineMessage : INetworkStruct
{
    public int LootIndex;
    public int Amount;
}

/// <summary>Authority-priced ticket line on the wire.</summary>
public struct MerchantPricedTradeLineMessage : INetworkStruct
{
    public int LootIndex;
    public int Amount;
    public int UnitPrice;
}

/// <summary>
/// Converts trade lines to and from their RPC representation through the deterministic loot
/// catalog index. Decoding rejects unknown indices and more lines than <c>maxLines</c>.
/// </summary>
public static class MerchantTradeWireCodec
{
    public static bool TryEncodeRequest(
        LootDefinitionCatalog catalog,
        IReadOnlyList<MerchantTradeLine> lines,
        out MerchantTradeRequestLineMessage[] messages)
    {
        messages = null;
        if (catalog == null || lines == null)
        {
            return false;
        }

        var encoded = new MerchantTradeRequestLineMessage[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].LootId.IsValid || !catalog.TryGetIndex(lines[i].LootId, out int index))
            {
                return false;
            }

            encoded[i] = new MerchantTradeRequestLineMessage { LootIndex = index, Amount = lines[i].Amount };
        }

        messages = encoded;
        return true;
    }

    public static bool TryDecodeRequest(
        LootDefinitionCatalog catalog,
        MerchantTradeRequestLineMessage[] messages,
        int maxLines,
        out MerchantTradeLine[] lines)
    {
        lines = null;
        if (catalog == null || messages == null || messages.Length > maxLines)
        {
            return false;
        }

        var decoded = new MerchantTradeLine[messages.Length];
        for (int i = 0; i < messages.Length; i++)
        {
            if (!TryResolve(catalog, messages[i].LootIndex, out LootId lootId))
            {
                return false;
            }

            decoded[i] = new MerchantTradeLine(lootId, messages[i].Amount);
        }

        lines = decoded;
        return true;
    }

    public static bool TryEncodeTicketLines(
        LootDefinitionCatalog catalog,
        IReadOnlyList<MerchantPricedTradeLine> lines,
        out MerchantPricedTradeLineMessage[] messages)
    {
        messages = null;
        if (catalog == null || lines == null)
        {
            return false;
        }

        var encoded = new MerchantPricedTradeLineMessage[lines.Count];
        for (int i = 0; i < lines.Count; i++)
        {
            if (!lines[i].LootId.IsValid || !catalog.TryGetIndex(lines[i].LootId, out int index))
            {
                return false;
            }

            encoded[i] = new MerchantPricedTradeLineMessage
            {
                LootIndex = index,
                Amount = lines[i].Amount,
                UnitPrice = lines[i].UnitPrice
            };
        }

        messages = encoded;
        return true;
    }

    public static bool TryDecodeTicket(
        LootDefinitionCatalog catalog,
        ShopTransactionId transactionId,
        MerchantPricedTradeLineMessage[] purchases,
        MerchantPricedTradeLineMessage[] sales,
        int maxPurchaseLines,
        int maxSaleLines,
        out MerchantTradeTicket ticket)
    {
        ticket = null;
        if (!TryDecodeTicketLines(catalog, purchases, maxPurchaseLines, out MerchantPricedTradeLine[] decodedPurchases) ||
            !TryDecodeTicketLines(catalog, sales, maxSaleLines, out MerchantPricedTradeLine[] decodedSales))
        {
            return false;
        }

        var decoded = new MerchantTradeTicket(transactionId, decodedPurchases, decodedSales);
        if (!decoded.IsWellFormed)
        {
            return false;
        }

        ticket = decoded;
        return true;
    }

    private static bool TryDecodeTicketLines(
        LootDefinitionCatalog catalog,
        MerchantPricedTradeLineMessage[] messages,
        int maxLines,
        out MerchantPricedTradeLine[] lines)
    {
        lines = null;
        if (catalog == null || messages == null || messages.Length > maxLines)
        {
            return false;
        }

        var decoded = new MerchantPricedTradeLine[messages.Length];
        for (int i = 0; i < messages.Length; i++)
        {
            if (!TryResolve(catalog, messages[i].LootIndex, out LootId lootId))
            {
                return false;
            }

            decoded[i] = new MerchantPricedTradeLine(lootId, messages[i].Amount, messages[i].UnitPrice);
        }

        lines = decoded;
        return true;
    }

    private static bool TryResolve(LootDefinitionCatalog catalog, int index, out LootId lootId)
    {
        lootId = default;
        if (!catalog.TryGetByIndex(index, out LootDefinition definition) || definition == null ||
            string.IsNullOrWhiteSpace(definition.Id))
        {
            return false;
        }

        lootId = new LootId(definition.Id);
        return true;
    }
}
