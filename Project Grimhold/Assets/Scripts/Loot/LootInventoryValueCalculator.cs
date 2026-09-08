using System;
using System.Collections.Generic;

/// <summary>Derives economic sell value from an inventory snapshot and static loot definitions.</summary>
public static class LootInventoryValueCalculator
{
    public static bool TryCalculate(
        IReadOnlyList<LootEntry> content,
        LootDefinitionCatalog catalog,
        out long total)
    {
        total = 0;
        if (content == null || catalog == null)
        {
            return false;
        }

        try
        {
            for (int index = 0; index < content.Count; index++)
            {
                LootEntry entry = content[index];
                if (!entry.IsValid || !catalog.TryGet(entry.LootId.Value, out LootDefinition definition))
                {
                    total = 0;
                    return false;
                }

                total = checked(total + checked((long)entry.Amount * definition.SellValuePerUnit));
            }
        }
        catch (OverflowException)
        {
            total = 0;
            return false;
        }

        return true;
    }
}
