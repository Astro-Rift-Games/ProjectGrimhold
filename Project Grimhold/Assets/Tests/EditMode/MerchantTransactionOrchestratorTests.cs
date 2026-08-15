using NUnit.Framework;
using Fusion;
using System;
using UnityEngine;
using System.Collections.Generic;
using Assert = NUnit.Framework.Assert;

public class MerchantTransactionOrchestratorTests
{
    private MerchantTransactionOrchestrator _orchestrator;
    private MerchantRequestValidator _validator;
    private MockShopTransactionService _shopService;
    private MockRpcSender _rpcSender;
    private LootDefinitionCatalog _catalog;
    private ProfileId _profileId = new ProfileId("player1");
    
    private List<MerchantTransactionResult> _uiEvents;

    private class DummyInventoryHandler : IMerchantInventoryHandler
    {
        public bool ValidatePurchase(string lootId, int amount) => true;
        public void CommitPurchase(string lootId, int amount) { }
        public bool ValidateSale(string lootId, int amount) => true;
        public void CommitSale(string lootId, int amount) { }
    }

    private class MockShopTransactionService : IShopTransactionService
    {
        public bool ForceFailFunds;
        public bool ForceFailItems;
        public bool ForceAlreadyApplied;
        public int Executions;

        public StashOperationResult TryExecutePurchase(ProfileId profileId, LootId lootId, int amount, long declaredPrice, ShopTransactionId transactionId)
        {
            if (ForceAlreadyApplied) return StashOperationResult.AlreadyApplied;
            if (ForceFailFunds) return StashOperationResult.InvalidInventory;
            Executions++;
            return StashOperationResult.Success;
        }

        public StashOperationResult TryExecuteSale(ProfileId profileId, LootId lootId, int amount, long declaredSellValue, ShopTransactionId transactionId)
        {
            if (ForceAlreadyApplied) return StashOperationResult.AlreadyApplied;
            if (ForceFailItems) return StashOperationResult.InvalidInventory;
            Executions++;
            return StashOperationResult.Success;
        }
    }

    private class MockRpcSender : IMasterClientRpcSender
    {
        public Action<LootId, int, int> OnPurchaseRequested;
        public Action<LootId, int, int> OnSaleRequested;

        public void SendPurchaseRequest(LootId lootId, int amount, int clientSequence)
        {
            OnPurchaseRequested?.Invoke(lootId, amount, clientSequence);
        }

        public void SendSaleRequest(LootId lootId, int amount, int clientSequence)
        {
            OnSaleRequested?.Invoke(lootId, amount, clientSequence);
        }
    }

    [SetUp]
    public void Setup()
    {
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
            var initMethod = typeof(LootDefinitionCatalog).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            initMethod?.Invoke(_catalog, null);
        }

        _shopService = new MockShopTransactionService();
        _validator = new MerchantRequestValidator();
        _rpcSender = new MockRpcSender();
        
        _orchestrator = new MerchantTransactionOrchestrator(_shopService, _catalog, _rpcSender, _profileId);
        
        _uiEvents = new List<MerchantTransactionResult>();
        _orchestrator.TransactionCompleted += r => _uiEvents.Add(r);

        // Wire up the simulated network
        var simulatedPlayer = PlayerRef.FromEncoded(1);
        _rpcSender.OnPurchaseRequested = (lootId, amount, sequence) => 
        {
            if (_validator.TryProcessPurchaseRequest(simulatedPlayer, new DummyInventoryHandler(), sequence, lootId.Value, amount, _catalog, out bool isApproved, out ShopTransactionId txId))
            {
                _orchestrator.OnPurchaseResponseReceived(sequence, isApproved, txId);
            }
        };

        _rpcSender.OnSaleRequested = (lootId, amount, sequence) => 
        {
            if (_validator.TryProcessSaleRequest(simulatedPlayer, new DummyInventoryHandler(), sequence, lootId.Value, amount, _catalog, out bool isApproved, out ShopTransactionId txId))
            {
                _orchestrator.OnSaleResponseReceived(sequence, isApproved, txId);
            }
        };
    }

    [Test]
    public void PurchaseValid_AppliesStore()
    {
        _orchestrator.RequestPurchase(new LootId("healthpotion"), 2);

        Assert.AreEqual(1, _uiEvents.Count);
        Assert.AreEqual(MerchantTransactionResult.Success, _uiEvents[0]);
        Assert.AreEqual(1, _shopService.Executions);
    }

    [Test]
    public void PurchaseWithoutFunds_FailsInStore()
    {
        _shopService.ForceFailFunds = true;
        _orchestrator.RequestPurchase(new LootId("healthpotion"), 2);

        Assert.AreEqual(1, _uiEvents.Count);
        Assert.AreEqual(MerchantTransactionResult.InsufficientFunds, _uiEvents[0]);
        Assert.AreEqual(0, _shopService.Executions); // Execution was attempted but failed
    }

    [Test]
    public void PurchaseInvalidAmount_RejectedByMaster()
    {
        _orchestrator.RequestPurchase(new LootId("healthpotion"), 0); // Fails pre-validation on client

        Assert.AreEqual(1, _uiEvents.Count);
        Assert.AreEqual(MerchantTransactionResult.InvalidRequest, _uiEvents[0]);
        Assert.AreEqual(0, _shopService.Executions);
    }

    [Test]
    public void PurchaseNonExistentItem_RejectedByMaster()
    {
        _orchestrator.RequestPurchase(new LootId("unknown"), 1); // Fails pre-validation

        Assert.AreEqual(1, _uiEvents.Count);
        Assert.AreEqual(MerchantTransactionResult.InvalidRequest, _uiEvents[0]);
        Assert.AreEqual(0, _shopService.Executions);
    }

    [Test]
    public void SaleValid_AppliesStore()
    {
        _orchestrator.RequestSale(new LootId("healthpotion"), 5);

        Assert.AreEqual(1, _uiEvents.Count);
        Assert.AreEqual(MerchantTransactionResult.Success, _uiEvents[0]);
        Assert.AreEqual(1, _shopService.Executions);
    }

    [Test]
    public void SaleWithoutItems_FailsInStore()
    {
        _shopService.ForceFailItems = true;
        _orchestrator.RequestSale(new LootId("healthpotion"), 5);

        Assert.AreEqual(1, _uiEvents.Count);
        Assert.AreEqual(MerchantTransactionResult.MissingItems, _uiEvents[0]);
        Assert.AreEqual(0, _shopService.Executions);
    }

    [Test]
    public void ResponseLostAndRequestRetried_ExecutesOnlyOnce()
    {
        int lastSequenceSent = -1;
        var simulatedPlayer = PlayerRef.FromEncoded(1);

        // Break the automatic network wiring to simulate a lost response
        _rpcSender.OnPurchaseRequested = (lootId, amount, sequence) => 
        {
            lastSequenceSent = sequence;
            _validator.TryProcessPurchaseRequest(simulatedPlayer, new DummyInventoryHandler(), sequence, lootId.Value, amount, _catalog, out _, out _);
        };

        _orchestrator.RequestPurchase(new LootId("healthpotion"), 2);
        
        Assert.AreEqual(0, _uiEvents.Count, "No response should have been received by the orchestrator.");
        Assert.AreEqual(0, _shopService.Executions);

        // Retry the exact same request sequence manually
        _validator.TryProcessPurchaseRequest(simulatedPlayer, new DummyInventoryHandler(), lastSequenceSent, "healthpotion", 2, _catalog, out bool isApproved, out ShopTransactionId txId);
        
        // Deliver the response this time
        _orchestrator.OnPurchaseResponseReceived(lastSequenceSent, isApproved, txId);

        Assert.AreEqual(1, _uiEvents.Count);
        Assert.AreEqual(MerchantTransactionResult.Success, _uiEvents[0]);
        Assert.AreEqual(1, _shopService.Executions);
    }

    [Test]
    public void DuplicateResponse_DoesNotReexecuteLocalTransaction()
    {
        _orchestrator.RequestPurchase(new LootId("healthpotion"), 2);
        
        Assert.AreEqual(1, _shopService.Executions);

        // Assuming sequence was 1, and we manually forge a duplicate response
        _orchestrator.OnPurchaseResponseReceived(1, true, new ShopTransactionId(1000, Guid.NewGuid()));

        // The orchestrator should ignore it because it's no longer pending
        Assert.AreEqual(1, _shopService.Executions);
        Assert.AreEqual(1, _uiEvents.Count);
    }
}
