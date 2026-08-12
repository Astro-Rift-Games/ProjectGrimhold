using NUnit.Framework;
using Fusion;
using System;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public class MerchantRequestValidatorTests
{
    private MerchantRequestValidator _validator;
    private LootDefinitionCatalog _catalog;
    private int _guidGenerationCount;
    private long _fixedTimestamp = 1000L;

    [SetUp]
    public void Setup()
    {
        _guidGenerationCount = 0;
        _validator = new MerchantRequestValidator(
            timestampProvider: () => _fixedTimestamp,
            guidProvider: () => 
            {
                _guidGenerationCount++;
                return Guid.NewGuid();
            }
        );

        _catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
        var item = ScriptableObject.CreateInstance<LootDefinition>();
        var idField = typeof(LootDefinition).GetField("_id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var extractField = typeof(LootDefinition).GetField("_extractionValuePerUnit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var sellField = typeof(LootDefinition).GetField("_sellValuePerUnit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        idField?.SetValue(item, "healthpotion");
        extractField?.SetValue(item, 100);
        sellField?.SetValue(item, 20);
        var itemsField = typeof(LootDefinitionCatalog).GetField("_items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (itemsField != null)
        {
            itemsField.SetValue(_catalog, new[] { item });
            // Re-initialize dictionary
            var initMethod = typeof(LootDefinitionCatalog).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initMethod?.Invoke(_catalog, null);
        }
    }

    [Test]
    public void SameRequestReceivedTwice_ReturnsSameTransactionId()
    {
        var player = PlayerRef.FromEncoded(1);
        
        bool success1 = _validator.TryProcessPurchaseRequest(player, 42, "healthpotion", 2, _catalog, out bool approved1, out ShopTransactionId id1);
        bool success2 = _validator.TryProcessPurchaseRequest(player, 42, "healthpotion", 2, _catalog, out bool approved2, out ShopTransactionId id2);

        Assert.IsTrue(success1);
        Assert.IsTrue(success2);
        Assert.IsTrue(approved1);
        Assert.IsTrue(approved2);
        Assert.AreEqual(id1.Timestamp, id2.Timestamp);
        Assert.AreEqual(id1.Value, id2.Value);
    }

    [Test]
    public void SameRequestReceivedTwice_DoesNotCreateSecondTransaction()
    {
        var player = PlayerRef.FromEncoded(1);
        
        _validator.TryProcessPurchaseRequest(player, 42, "healthpotion", 2, _catalog, out _, out _);
        
        int generationCountAfterFirst = _guidGenerationCount;
        
        _validator.TryProcessPurchaseRequest(player, 42, "healthpotion", 2, _catalog, out _, out _);
        
        Assert.AreEqual(1, generationCountAfterFirst, "Guid should have been generated exactly once on the first request.");
        Assert.AreEqual(1, _guidGenerationCount, "Guid should not be generated again for a duplicate request.");
    }

    [Test]
    public void SameSequenceWithDifferentPayload_IsRejected()
    {
        var player = PlayerRef.FromEncoded(1);
        
        _validator.TryProcessPurchaseRequest(player, 42, "healthpotion", 2, _catalog, out bool approved1, out ShopTransactionId id1);
        
        bool success2 = _validator.TryProcessPurchaseRequest(player, 42, "healthpotion", 3, _catalog, out bool approved2, out ShopTransactionId id2);

        Assert.IsTrue(approved1);
        Assert.IsFalse(success2, "Second request with same sequence but different payload should be rejected as a conflict.");
        Assert.IsFalse(approved2);
        Assert.AreEqual(Guid.Empty, id2.Value);
    }

    [Test]
    public void DifferentPlayersWithSameSequence_AreIndependent()
    {
        var player1 = PlayerRef.FromEncoded(1);
        var player2 = PlayerRef.FromEncoded(2);
        
        _validator.TryProcessPurchaseRequest(player1, 42, "healthpotion", 2, _catalog, out bool approved1, out ShopTransactionId id1);
        _validator.TryProcessPurchaseRequest(player2, 42, "healthpotion", 2, _catalog, out bool approved2, out ShopTransactionId id2);

        Assert.IsTrue(approved1);
        Assert.IsTrue(approved2);
        Assert.AreNotEqual(id1.Value, id2.Value, "Different players should receive different transaction IDs even with the same sequence.");
        Assert.AreEqual(2, _guidGenerationCount);
    }

    [Test]
    public void PurchaseInvalidAmount_RejectedByMaster()
    {
        var player = PlayerRef.FromEncoded(1);
        
        _validator.TryProcessPurchaseRequest(player, 42, "healthpotion", 0, _catalog, out bool approved, out ShopTransactionId id);

        Assert.IsFalse(approved);
        Assert.AreEqual(Guid.Empty, id.Value);
    }

    [Test]
    public void PurchaseNonExistentItem_RejectedByMaster()
    {
        var player = PlayerRef.FromEncoded(1);
        
        _validator.TryProcessPurchaseRequest(player, 42, "unknown", 1, _catalog, out bool approved, out ShopTransactionId id);

        Assert.IsFalse(approved);
        Assert.AreEqual(Guid.Empty, id.Value);
    }
}
