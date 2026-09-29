using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Immutable, advisory projection of one merchant trade: confirmed Inventory and Currency plus the
/// draft lines and the currently available stock, priced from the static economy configuration.
/// It never mutates its inputs and grants no authority; State Authority and the persistent
/// transaction revalidate everything.
/// </summary>
public sealed class MerchantTradePreview
{
    /// <summary>Sum of buy value per unit times amount over the priced purchase lines.</summary>
    public long PurchaseTotal { get; }

    /// <summary>Sum of sell value per unit times amount over the priced sale lines.</summary>
    public long SaleTotal { get; }

    /// <summary>Currency change of the whole trade: <see cref="SaleTotal"/> minus <see cref="PurchaseTotal"/>.</summary>
    public long Balance { get; }

    /// <summary>Confirmed Currency plus <see cref="Balance"/>.</summary>
    public long ProjectedCurrency { get; }

    /// <summary>
    /// Inventory after removing the covered sale lines and adding the priced purchase lines.
    /// </summary>
    public IReadOnlyList<LootEntry> ProjectedInventory { get; }

    /// <summary>Slots occupied by the confirmed Inventory, one per stacked loot.</summary>
    public int ConfirmedOccupiedSlots { get; }

    /// <summary>Slots occupied by <see cref="ProjectedInventory"/>, also when it exceeds the capacity.</summary>
    public int ProjectedOccupiedSlots => ProjectedInventory.Count;

    public int SlotCapacity { get; }
    public IReadOnlyList<MerchantTradeBlock> Blocks { get; }
    public bool CanConfirm => Blocks.Count == 0;

    private MerchantTradePreview(
        long purchaseTotal,
        long saleTotal,
        long balance,
        long projectedCurrency,
        IReadOnlyList<LootEntry> projectedInventory,
        int confirmedOccupiedSlots,
        int slotCapacity,
        IReadOnlyList<MerchantTradeBlock> blocks)
    {
        PurchaseTotal = purchaseTotal;
        SaleTotal = saleTotal;
        Balance = balance;
        ProjectedCurrency = projectedCurrency;
        ProjectedInventory = projectedInventory;
        ConfirmedOccupiedSlots = confirmedOccupiedSlots;
        SlotCapacity = slotCapacity;
        Blocks = blocks;
    }

    public bool HasBlock(MerchantTradeBlockReason reason)
    {
        foreach (MerchantTradeBlock block in Blocks)
        {
            if (block.Reason == reason)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Projects the trade. Sale lines are checked against the confirmed owned units and purchase
    /// lines against <paramref name="availableStock"/> (units, or
    /// <see cref="MerchantStockItem.UnlimitedQuantity"/>); units sold in this trade do not become
    /// purchasable in it. Currency and capacity are evaluated on the final state of all lines
    /// together, with the aggregate's <see cref="ProfileInventoryRules"/>. When a Currency value
    /// overflows, every value total is reported as zero with an <see cref="MerchantTradeBlockReason.Overflow"/> block.
    /// </summary>
    public static MerchantTradePreview Calculate(
        IReadOnlyList<LootEntry> confirmedInventory,
        int slotCapacity,
        long confirmedCurrency,
        MerchantTradeDraft draft,
        Func<LootId, int> availableStock,
        LootDefinitionCatalog catalog)
    {
        if (confirmedInventory == null) throw new ArgumentNullException(nameof(confirmedInventory));
        if (draft == null) throw new ArgumentNullException(nameof(draft));
        if (availableStock == null) throw new ArgumentNullException(nameof(availableStock));
        if (catalog == null) throw new ArgumentNullException(nameof(catalog));
        if (slotCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(slotCapacity));
        if (confirmedCurrency < 0) throw new ArgumentOutOfRangeException(nameof(confirmedCurrency));

        var blocks = new List<MerchantTradeBlock>();
        if (draft.IsEmpty) blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.EmptyDraft));
        if (draft.IsInFlight) blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.SubmissionInFlight));

        List<StashItem> confirmed = CopyInventory(confirmedInventory);
        var projected = new List<StashItem>(confirmed);
        bool overflow = false;

        long saleTotal = 0;
        foreach (MerchantTradeLine line in draft.Sales)
        {
            if (!TryResolve(catalog, line.LootId, blocks, out LootDefinition definition)) continue;

            if (line.Amount > ProfileInventoryRules.FindAmount(confirmed, line.LootId))
            {
                blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.InsufficientOwnedUnits, line.LootId));
                continue;
            }

            ProfileInventoryRules.TryRemove(projected, line.LootId, line.Amount);
            overflow |= !TryAdd(saleTotal, (long)definition.SellValuePerUnit * line.Amount, out saleTotal);
        }

        long purchaseTotal = 0;
        var purchased = new List<StashItem>();
        foreach (MerchantTradeLine line in draft.Purchases)
        {
            if (!TryResolve(catalog, line.LootId, blocks, out LootDefinition definition)) continue;

            if (definition.BuyValuePerUnit <= 0)
            {
                blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.NotPurchasable, line.LootId));
                continue;
            }

            int stock = availableStock(line.LootId);
            if (stock != MerchantStockItem.UnlimitedQuantity && line.Amount > stock)
            {
                blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.InsufficientStock, line.LootId));
            }

            purchased.Add(new StashItem(line.LootId, line.Amount));
            overflow |= !TryAdd(purchaseTotal, (long)definition.BuyValuePerUnit * line.Amount, out purchaseTotal);
        }

        bool exceedsCapacity = ProfileInventoryRules.ExceedsCapacity(projected, purchased, slotCapacity);
        foreach (StashItem item in purchased)
        {
            if (!ProfileInventoryRules.TryMerge(projected, new[] { item }))
            {
                blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.Overflow, item.LootId));
            }
        }

        long balance = saleTotal - purchaseTotal;
        overflow |= !TryAdd(confirmedCurrency, balance, out long projectedCurrency);

        if (overflow)
        {
            blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.Overflow));
            purchaseTotal = saleTotal = balance = projectedCurrency = 0;
        }
        else if (projectedCurrency < 0)
        {
            blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.InsufficientCurrency));
        }

        if (exceedsCapacity) blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.CapacityExceeded));

        return new MerchantTradePreview(
            purchaseTotal,
            saleTotal,
            balance,
            projectedCurrency,
            ToEntries(projected),
            confirmed.Count,
            slotCapacity,
            new ReadOnlyCollection<MerchantTradeBlock>(blocks));
    }

    private static bool TryResolve(
        LootDefinitionCatalog catalog,
        LootId lootId,
        List<MerchantTradeBlock> blocks,
        out LootDefinition definition)
    {
        if (catalog.TryGet(lootId.Value, out definition) && definition != null)
        {
            return true;
        }

        blocks.Add(new MerchantTradeBlock(MerchantTradeBlockReason.UnknownItem, lootId));
        return false;
    }

    private static List<StashItem> CopyInventory(IReadOnlyList<LootEntry> inventory)
    {
        var items = new List<StashItem>(inventory.Count);
        foreach (LootEntry entry in inventory)
        {
            if (!entry.IsValid ||
                !ProfileInventoryRules.TryMerge(items, new[] { new StashItem(entry.LootId, entry.Amount) }))
            {
                throw new ArgumentException("The confirmed Inventory contains an invalid entry.", nameof(inventory));
            }
        }

        return items;
    }

    private static IReadOnlyList<LootEntry> ToEntries(List<StashItem> items)
    {
        var entries = new LootEntry[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            entries[i] = new LootEntry(items[i].LootId, items[i].Amount);
        }

        return Array.AsReadOnly(entries);
    }

    private static bool TryAdd(long left, long right, out long sum)
    {
        if ((right > 0 && left > long.MaxValue - right) || (right < 0 && left < long.MinValue - right))
        {
            sum = 0;
            return false;
        }

        sum = left + right;
        return true;
    }
}
