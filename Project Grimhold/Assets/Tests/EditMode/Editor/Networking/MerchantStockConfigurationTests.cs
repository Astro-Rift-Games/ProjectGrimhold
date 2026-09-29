using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public class MerchantStockConfigurationTests
{
    private const string MerchantPrefabPath = "Assets/Prefabs/Merchant.prefab";

    [Test]
    public void MerchantStock_EveryItemHasValidDefinitionAndPositiveBuyValue()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefabPath);
        Assert.That(prefab, Is.Not.Null, MerchantPrefabPath);

        TownMerchantNetworkController merchant = prefab.GetComponent<TownMerchantNetworkController>();
        Assert.That(merchant, Is.Not.Null);
        Assert.That(merchant.Stock, Is.Not.Null.And.Not.Empty);
        Assert.That(
            MerchantRequestValidator.TryValidateStockConfiguration(merchant.Stock, TownMerchantNetworkController.MaxStockSlots, out string configurationError),
            Is.True,
            configurationError);

        foreach (MerchantStockItem stockItem in merchant.Stock)
        {
            Assert.That(stockItem.Item, Is.Not.Null, "Merchant stock contains an empty item reference.");
            Assert.That(stockItem.Item.TryValidate(out string error), Is.True, error);
            Assert.That(stockItem.Item.BuyValuePerUnit, Is.GreaterThan(0), stockItem.Item.Id);
            Assert.That(merchant.Catalog.TryGet(stockItem.Item.Id, out _), Is.True, stockItem.Item.Id);
        }
    }

    [Test]
    public void MerchantStock_KeepsSerializedInitialQuantitiesAfterRename()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MerchantPrefabPath);
        TownMerchantNetworkController merchant = prefab.GetComponent<TownMerchantNetworkController>();

        int[] expected = { 5, 3, 5, 5, 5, 5 };
        Assert.That(merchant.Stock.Count, Is.EqualTo(expected.Length));
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.That(merchant.Stock[i].InitialQuantity, Is.EqualTo(expected[i]), merchant.Stock[i].Item.Id);
        }
    }
}
