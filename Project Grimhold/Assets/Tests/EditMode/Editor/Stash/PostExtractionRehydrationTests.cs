#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

public sealed class PostExtractionRehydrationTests
{
    private static readonly ProfileId Profile = new("57575757575757575757575757575757");
    private LootDefinitionCatalog Catalog => AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
        "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

    [TestCase("success")]
    [TestCase("write-failure")]
    [TestCase("missing-instance")]
    [TestCase("revision-mismatch")]
    [TestCase("wrong-receipt")]
    [TestCase("already-completed")]
    [TestCase("other-local-receipt")]
    [TestCase("backend-reservation")]
    [TestCase("stale-ack")]
    public void SuccessfulExtraction_AuthoritativeInstancesAllowNextReservation(string outcome)
    {
        var files = new MemoryFiles();
        var repository = new LocalProfileRepository(files, ".");
        Assert.That(repository.Initialize(Profile, Catalog), Is.True, repository.LastError);
        var store = new LocalProfileStore(repository, Profile, Catalog);
        Assert.That(store.ReconcileRemoteState(Inventory(), null, Catalog, null), Is.EqualTo(StashOperationResult.Success));
        var equipment = Equipment(false);
        Assert.That(equipment.TryToLoadout(Catalog, false, out var prepared, out var error), Is.True, error);
        var receipt = new ExtractionReceipt("raid-post-extraction", Profile, 1);
        Assert.That(store.TryCommitExtraction(receipt,
            new[] { new StashItem(new LootId("rapier"), 1, 0, new EquipmentInstance(null, default(WeaponInstanceModifiers))) },
            prepared, 0, 1, 0), Is.EqualTo(StashOperationResult.Success));
        var authoritative = Inventory();
        authoritative.revision = 8;
        authoritative.loadout = new[] { new InventoryItemData { slot = 0, lootId = "rapier", amount = 1,
            instance = new EquipmentInstanceData { instanceId = "333333333333333333333333" } } };
        authoritative.preparedEquipment = Equipment(true);
        Assert.That(store.ReconcileRemoteState(authoritative, null, Catalog, null), Is.EqualTo(StashOperationResult.Success));
        // Ordinary reconciliation must preserve the unsent payload, including ID-free equipment.
        Assert.That(store.PendingExtractionCommit, Is.Not.Null);
        Assert.That(store.GetPreparedEquipment().GetInstance(EquipmentSlot.WeaponSetAMainHand).HasIdentity, Is.False);
        authoritative.lastAppliedExtractionReceipt = new ExtractionReceiptData { raidId = receipt.RaidId, resultSequence = 1 };
        var progression = new ProgressionData
        {
            revision = 8, level = 2, experience = 17, lastAppliedProgressionResultSequence = 1,
            characterAttributes = new CharacterAttributesData
            { vitality = 10, resistance = 10, strength = 10, dexterity = 10, intelligence = 10, luck = 10 }
        };
        if (outcome == "write-failure") files.RejectWrites = true;
        if (outcome == "missing-instance") authoritative.preparedEquipment = Equipment(false);
        if (outcome == "revision-mismatch") progression.revision++;
        if (outcome == "wrong-receipt") authoritative.lastAppliedExtractionReceipt.raidId = "other-raid";
        if (outcome == "backend-reservation") authoritative.pendingReservation = new PendingReservationData { reservationId = "r1" };
        if (outcome == "already-completed")
            Assert.That(store.TryCompleteExtractionCommit(receipt, 8, authoritative, progression), Is.EqualTo(StashOperationResult.Success));
        var pending = store.PendingExtractionCommit;
        var beforeLoadout = store.GetLoadout().ToArray();
        var beforeEquipment = store.GetPreparedEquipment();
        int revision = store.RemoteRevision, writes = files.Writes, commits = 0;
        store.ProfileCommitted += _ => commits++;
        StashOperationResult apply;
        if (outcome == "write-failure")
            LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\[LocalProfileStore\] Commit failed\..*Injected write rejection"));
        var applyReceipt = outcome == "other-local-receipt" ? new ExtractionReceipt("raid-other", Profile, 1) : receipt;
        apply = store.TryCompleteExtractionCommit(applyReceipt, outcome == "stale-ack" ? 9 : 8, authoritative, progression);
        if (outcome != "success")
        {
            Assert.That(apply, Is.EqualTo(outcome == "write-failure" ? StashOperationResult.PersistenceFailed : StashOperationResult.HydrationFailed));
            if (outcome == "already-completed") Assert.That(store.PendingExtractionCommit, Is.Null);
            else Assert.That(store.PendingExtractionCommit.Receipt, Is.EqualTo(pending.Receipt));
            Assert.That(store.GetLoadout(), Is.EqualTo(beforeLoadout));
            Assert.That(store.GetPreparedEquipment(), Is.EqualTo(beforeEquipment));
            Assert.That(store.RemoteRevision, Is.EqualTo(revision));
            Assert.That(files.Writes, Is.EqualTo(writes));
            Assert.That(commits, Is.Zero);
            return;
        }
        Assert.That(apply, Is.EqualTo(StashOperationResult.Success));
        Assert.That(files.Writes, Is.EqualTo(writes + 1));
        Assert.That(commits, Is.EqualTo(1));
        Assert.That(store.RemoteRevision, Is.EqualTo(8));
        Assert.That(store.GetLevel(), Is.EqualTo(2));
        Assert.That(store.GetCurrentExperience(), Is.EqualTo(17));
        Assert.That(store.TryGetCharacterAttributeState(out var attributes), Is.True);
        Assert.That(attributes.Vitality, Is.EqualTo(10));
        Assert.That(store.TryCreateLoadoutReservation("next-raid", out _), Is.EqualTo(StashOperationResult.Success));
        Assert.That(store.PendingExtractionCommit, Is.Null);
        Assert.That(store.PendingReservation.Items.Single().Instance.InstanceId, Is.EqualTo("333333333333333333333333"));
        Assert.That(store.PendingReservation.PreparedEquipment.GetInstance(EquipmentSlot.WeaponSetAMainHand).InstanceId,
            Is.EqualTo("111111111111111111111111"));
        Assert.That(store.PendingReservation.PreparedEquipment.GetInstance(EquipmentSlot.Helmet).InstanceId,
            Is.EqualTo("222222222222222222222222"));
    }

    private PreparedEquipmentData Equipment(bool identified)
    {
        string helmet = null;
        for (int i = 0; i < Catalog.DefinitionCount; i++)
            if (Catalog.TryGetByIndex(i, out var definition) && definition.Category == LootCategory.Helmet)
            { helmet = definition.Id; break; }
        Assert.That(helmet, Is.Not.Null);
        var equipment = new PreparedEquipmentData();
        equipment.Set(EquipmentSlot.WeaponSetAMainHand, "arming_sword",
            new EquipmentInstanceData { instanceId = identified ? "111111111111111111111111" : null }, true);
        equipment.Set(EquipmentSlot.Helmet, helmet,
            new EquipmentInstanceData { instanceId = identified ? "222222222222222222222222" : null }, true);
        return equipment;
    }

    private static InventoryData Inventory() => new()
    {
        inventorySchemaVersion = 3, revision = 7, currency = 10,
        stash = Array.Empty<InventoryItemData>(), loadout = Array.Empty<InventoryItemData>()
    };

    private sealed class MemoryFiles : ILocalProfileFileStore
    {
        private readonly Dictionary<string, string> _files = new();
        public int Writes { get; private set; }
        public bool RejectWrites { get; set; }
        public bool Exists(string path) => _files.ContainsKey(path);
        public bool TryRead(string path, out string contents, out string error)
        { error = _files.TryGetValue(path, out contents) ? null : "Missing"; return error == null; }
        public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
        {
            if (RejectWrites) { error = "Injected write rejection"; return false; }
            if (_files.TryGetValue(mainPath, out var old)) _files[backupPath] = old;
            _files[mainPath] = contents; Writes++; error = null; return true;
        }
        public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
        { error = "Not used"; return false; }
    }
}
#endif
