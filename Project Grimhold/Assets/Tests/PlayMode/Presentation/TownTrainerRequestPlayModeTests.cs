#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Presentation
{
    public sealed class TownTrainerRequestPlayModeTests
    {
        [UnityTest]
        public IEnumerator SuccessiveRequests_ReplaceAtomically_AndTrainingOnlyProfileCanPrepareExpedition()
        {
            using var profile = PlayModeLocalProfile.Create();
            var context = UnityEngine.Object.FindObjectsByType<ApplicationStashContext>(FindObjectsSortMode.None)
                .Single(candidate => candidate.ProfileId == profile.ProfileId);
            var store = context.Store;
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(store.ReconcileRemoteState(new InventoryData
            {
                inventorySchemaVersion = 3, stash = Array.Empty<InventoryItemData>(),
                loadout = Array.Empty<InventoryItemData>(), revision = 0
            }, null, catalog, null), Is.EqualTo(StashOperationResult.Success));
            int mutations = 0;
            var endpoint = new TownEquipmentMutationEndpoint(context.LoadoutService, profile.ProfileId, catalog,
                () => true, () => mutations++);
            TaskCompletionSource<(bool success, TrainingWeaponResult data, BackendError error)> pending = null;
            TrainingWeaponRequest intent = default;
            endpoint.BindTrainingBackend(store, request =>
            {
                intent = request;
                pending = new();
                return pending.Task;
            });
            var issued = new HashSet<string>();
            int revision = 0;
            foreach (string weapon in new[] { "training_short_sword", "training_dagger", "training_wand" })
            {
                var task = endpoint.RequestTrainingWeaponAsync(new LootId(weapon), EquipmentSlot.WeaponSetAMainHand);
                Assert.That(intent.expectedRevision, Is.EqualTo(revision));
                Assert.That(intent.destinationSlot, Is.EqualTo("weaponSlot1"));
                Assert.That(store.RemoteRevision, Is.EqualTo(revision), "An unacknowledged request cannot change local ownership.");
                Assert.That(endpoint.RequestTrainingWeaponAsync(new LootId(weapon)).Result, Is.EqualTo(StashOperationResult.InvalidInventory));
                yield return null;
                Assert.That(catalog.TryGet(weapon, out var definition), Is.True);
                string id = (++revision).ToString("x24");
                var equipment = default(PreparedEquipmentLoadout).With(EquipmentSlot.WeaponSetAMainHand,
                    new LootId(weapon), new EquipmentInstance(id, EquipmentInstance.PlainCache(definition).Modifiers));
                pending.SetResult((true, new TrainingWeaponResult
                {
                    preparedEquipment = PreparedEquipmentData.FromLoadout(equipment), revision = revision,
                    stash = Array.Empty<InventoryItemData>(), loadout = Array.Empty<InventoryItemData>()
                }, default));
                while (!task.IsCompleted) yield return null;
                Assert.That(task.Result, Is.EqualTo(StashOperationResult.Success));
                Assert.That(store.RemoteRevision, Is.EqualTo(revision));
                Assert.That(mutations, Is.EqualTo(revision));
                Assert.That(store.GetLoadout(), Is.Empty);
                Assert.That(store.GetStash(), Is.Empty);
                var held = store.GetPreparedEquipment();
                var heldIds = new HashSet<string>();
                int count = 0;
                foreach (var slot in EquipmentSlotRules.AllSlots)
                {
                    if (!held.Get(slot).IsValid) continue;
                    count++;
                    Assert.That(held.Get(slot).Value, Is.EqualTo(weapon));
                    Assert.That(heldIds.Add(held.GetInstance(slot).InstanceId), Is.True);
                    Assert.That(issued.Add(held.GetInstance(slot).InstanceId), Is.True);
                }
                Assert.That(count, Is.EqualTo(1));
                Assert.That(PreparedEquipmentLoadout.HasUsableWeapon(held, catalog), Is.True);
                Assert.That(store.TryPrepareExpeditionEquipment(), Is.EqualTo(ExpeditionPreparationResult.Success));
                Assert.That(context.LoadoutService.TryPrepareExpeditionLoadout(profile.ProfileId), Is.EqualTo(ExpeditionPreparationResult.Success));
            }
        }
    }
}
#endif
