#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class TownGemInstallSessionTests
    {
        private LocalProfileStore _store;
        private InMemoryLocalProfileRepository _repository;
        private LootDefinitionCatalog _catalog;
        private TownGemInstallSession _session;
        private GameObject _services;
        private readonly LootId _sword = new("rapier");
        private LootId _gem;
        private EquipmentInstance _instance;
        private int _commits;
        private FakeRemote _remote;
        private StashTransferCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>("Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            _gem = Enumerable.Range(0, _catalog.DefinitionCount).Select(i => _catalog.TryGetByIndex(i, out var d) ? d : null)
                .First(d => d != null && d.GemDefinition != null).LootId;
            var profile = new ProfileId("49494949494949494949494949494949");
            _repository = new InMemoryLocalProfileRepository();
            Assert.That(_repository.Initialize(profile, _catalog), Is.True);
            _catalog.TryGet(_sword.Value, out var weapon);
            Assert.That(WeaponInstanceModifiers.TryCreate(default, default, weapon.WeaponDefinition.NaturalScalingAttribute,
                1, default, default, out var modifiers, out _), Is.True);
            _instance = new EquipmentInstance("123456789012345678901234", modifiers);
            var next = _repository.Snapshot.Clone();
            next.InventorySchemaVersion = 3;
            next.RemoteRevision = 7;
            next.Stash.Add(new StashItem(_gem, 2, 19));
            next.Loadout.Add(new StashItem(_sword, 1, 4, _instance));
            Assert.That(_repository.TrySave(next, out _), Is.True);
            _store = new LocalProfileStore(_repository, profile, _catalog);
            _services = new GameObject("Gem session test services");
            var stash = _services.AddComponent<InMemoryPlayerStashService>(); stash.Initialize(_store);
            var loadout = _services.AddComponent<InMemoryPlayerLoadoutService>(); loadout.Initialize(_store);
            _remote = new FakeRemote();
            _coordinator = new StashTransferCoordinator(profile, stash, loadout, _remote, null);
            _session = new TownGemInstallSession(_store, _coordinator, _catalog);
            _store.ProfileCommitted += _ => _commits++;
            _commits = 0;
        }
        [TearDown] public void TearDown() { _session?.Dispose(); UnityEngine.Object.DestroyImmediate(_services); }

        [Test]
        public void ContextSelection_HighlightsOnlyStoredWeapons_ChoosesExactAddressesAndCancelsWithoutMutation()
        {
            Assert.That(_session.Begin(InventoryList.Stash, 19), Is.True);
            Assert.That(_session.IsSelecting, Is.True);
            Assert.That(_session.CanTarget(InventoryList.Loadout, 4), Is.True);
            Assert.That(_session.CanTarget(InventoryList.Stash, 19), Is.False);
            Assert.That(_session.CanTarget((InventoryList)2, 4), Is.False, "Equipment is never addressable.");
            Assert.That(_session.Choose(InventoryList.Loadout, 4), Is.True);
            Assert.That(_session.IsConfirming, Is.True);
            var request = _session.Request;
            Assert.That(request.weaponList, Is.EqualTo(InventoryList.Loadout));
            Assert.That(request.weaponSlot, Is.EqualTo(4));
            Assert.That(request.instanceId, Is.EqualTo(_instance.InstanceId));
            Assert.That(request.gemList, Is.EqualTo(InventoryList.Stash));
            Assert.That(request.gemSlot, Is.EqualTo(19));
            Assert.That(request.gemLootId, Is.EqualTo(_gem.Value));
            Assert.That(request.expectedRevision, Is.EqualTo(7));
            Assert.That(request.transactionId, Is.Not.Null.And.Not.Empty);
            _catalog.TryGet(_gem.Value, out var gem); _catalog.TryGet(_sword.Value, out var weapon);
            Assert.That(_session.Confirmation, Is.EqualTo($"¿Incrustar {gem.DisplayName} en {weapon.DisplayName}? Es permanente."));
            _session.Cancel();
            Assert.That(_session.IsConfirming || _session.IsSelecting || _session.LocksMutations, Is.False);
            AssertUnchanged();
        }

        [TestCase(false)] [TestCase(true)]
        public void Drop_NoSocketsOrFull_HandlesWithoutConfirmationAndShowsExactNotice(bool full)
        {
            var next = _repository.Snapshot.Clone();
            _catalog.TryGet(_gem.Value, out var gem);
            var modifiers = full ? _instance.Modifiers.Value : default;
            if (full) Assert.That(modifiers.TryInstallGem(gem, out modifiers, out _), Is.True);
            next.Loadout[0] = new StashItem(_sword, 1, 4, new EquipmentInstance(_instance.InstanceId, modifiers));
            Assert.That(_repository.TrySave(next, out _), Is.True);
            Assert.That(_session.Drop(InventoryList.Stash, 19, InventoryList.Loadout, 4), Is.True);
            Assert.That(_session.IsConfirming || _session.IsSelecting, Is.False);
            Assert.That(_session.Message, Is.EqualTo("Esta arma no tiene ranuras libres"));
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(2));
            Assert.That(_store.GetLoadout(), Is.EqualTo(next.Loadout));
            Assert.That(_store.RemoteRevision, Is.EqualTo(7)); Assert.That(_commits, Is.Zero);
        }

        [Test]
        public void Drop_NonWeaponFallsThrough_ValidWeaponConfirms_StaleSnapshotCancels()
        {
            Assert.That(_session.Drop(InventoryList.Stash, 19, InventoryList.Stash, 0), Is.False);
            Assert.That(_session.Drop(InventoryList.Loadout, 4, InventoryList.Stash, 19), Is.False);
            Assert.That(_session.Drop(InventoryList.Stash, 19, InventoryList.Loadout, 4), Is.True);
            Assert.That(_session.IsConfirming, Is.True);
            Assert.That(_commits, Is.Zero);
            _store.SetRemoteRevision(8);
            Assert.That(_session.IsConfirming || _session.IsSelecting, Is.False);
            Assert.That(_session.Message, Is.EqualTo(TownGemInstallSession.FailureMessage("REVISION_CONFLICT")));
        }

        [Test]
        public void HydrationGuard_DisallowsSelectionAndLeavesEverythingUnchanged()
        {
            var next = _repository.Snapshot.Clone(); next.RequiresEquipmentHydration = true;
            Assert.That(_repository.TrySave(next, out _), Is.True);
            Assert.That(_session.Begin(InventoryList.Stash, 19), Is.False);
            Assert.That(_session.Message, Is.EqualTo("El inventario debe sincronizarse antes de incrustar gemas"));
            AssertUnchanged();
        }

        [TestCase("INSTANCE_MISMATCH", "El arma ya no está disponible")]
        [TestCase("GEM_NOT_AVAILABLE", "La gema ya no está disponible")]
        [TestCase("GEM_NO_SOCKETS", "Esta arma no tiene ranuras libres")]
        [TestCase("GEM_NO_EMPTY_SOCKET", "Esta arma no tiene ranuras libres")]
        [TestCase("REVISION_CONFLICT", "El inventario cambió. Vuelve a intentarlo")]
        [TestCase("RESERVATION_PENDING", "No se puede modificar el inventario mientras hay una reserva de Raid pendiente")]
        [TestCase("INVENTORY_MIGRATION_REQUIRED", "El inventario debe sincronizarse antes de incrustar gemas")]
        public void RejectionFeedback_IsSpanish(string code, string message) =>
            Assert.That(TownGemInstallSession.FailureMessage(code), Is.EqualTo(message));

        [Test]
        public async Task Confirm_PendingLocksInputs_AppliesAuthoritativeSnapshotOnce_UpdatesTooltip()
        {
            var aggregate = _repository.Snapshot.Clone();
            aggregate.Currency = 143;
            aggregate.CurrentExperience = 17;
            aggregate.ShopIdempotencyWatermark = 123;
            aggregate.AppliedExtractionReceipts.Add(new ExtractionReceipt("gem-test-raid", _store.ProfileId, 2));
            Assert.That(_repository.TrySave(aggregate, out _), Is.True);
            Assert.That(_session.Drop(InventoryList.Stash, 19, InventoryList.Loadout, 4), Is.True);
            var captured = _session.Request;
            _catalog.TryGet(_gem.Value, out var gem);
            Assert.That(_instance.Modifiers.Value.TryInstallGem(gem, out var installed, out _), Is.True);
            _remote.Result = new MoveItemResult {
                revision = 8,
                stash = new[] { new InventoryItemData { slot = 19, lootId = _gem.Value, amount = 1 } },
                loadout = new[] { new InventoryItemData { slot = 4, lootId = _sword.Value, amount = 1,
                    instance = EquipmentInstanceData.FromInstance(new EquipmentInstance(_instance.InstanceId, installed)) } }
            };
            var pending = _session.ConfirmAsync();
            Assert.That(_session.IsPending && _session.LocksMutations, Is.True);
            Assert.That(_remote.Calls, Is.EqualTo(1));
            Assert.That(_remote.Request, Is.EqualTo(captured));
            _session.Cancel();
            Assert.That(_session.IsPending, Is.True);
            Assert.That(_session.Begin(InventoryList.Stash, 19), Is.False);
            await _session.ConfirmAsync();
            Assert.That(_remote.Calls, Is.EqualTo(1));
            AssertUnchanged();
            _remote.Gate.SetResult(true);
            await pending;
            Assert.That(_session.IsPending || _session.LocksMutations, Is.False);
            Assert.That(_store.GetStash().Single().Amount, Is.EqualTo(1));
            Assert.That(_store.GetLoadout().Single().Instance.Modifiers, Is.EqualTo(installed));
            Assert.That(_store.RemoteRevision, Is.EqualTo(8)); Assert.That(_commits, Is.EqualTo(1));
            Assert.That(_repository.Snapshot.Currency, Is.EqualTo(143));
            Assert.That(_repository.Snapshot.CurrentExperience, Is.EqualTo(17));
            Assert.That(_repository.Snapshot.ShopIdempotencyWatermark, Is.EqualTo(123));
            Assert.That(_repository.Snapshot.AppliedExtractionReceipts, Is.EqualTo(aggregate.AppliedExtractionReceipts));
            Assert.That(_repository.Snapshot.PreparedEquipment, Is.EqualTo(aggregate.PreparedEquipment));
            Assert.That(_repository.Snapshot.PreparedAbilities, Is.EqualTo(aggregate.PreparedAbilities));
            Assert.That(_repository.Snapshot.QuickSlots, Is.EqualTo(aggregate.QuickSlots));
            _catalog.TryGet(_sword.Value, out var definition);
            var tooltip = EquipmentTooltipPresentationBuilder.Build(definition, _store.GetLoadout().Single().Instance,
                ProgressionBalanceDefaults.InitialCharacterAttributeState, _catalog);
            Assert.That(tooltip, Is.Not.EqualTo(EquipmentTooltipPresentationBuilder.Build(definition, _instance,
                ProgressionBalanceDefaults.InitialCharacterAttributeState, _catalog)));
        }

        [TestCase("INSTANCE_MISMATCH")] [TestCase("GEM_NOT_AVAILABLE")]
        [TestCase("GEM_NO_SOCKETS")] [TestCase("GEM_NO_EMPTY_SOCKET")]
        [TestCase("REVISION_CONFLICT")] [TestCase("RESERVATION_PENDING")]
        [TestCase("INVENTORY_MIGRATION_REQUIRED")]
        public async Task Confirm_RejectionHasNoLocalEffectAndReleasesPending(string code)
        {
            _session.Drop(InventoryList.Stash, 19, InventoryList.Loadout, 4);
            _remote.Error = new BackendError { error = code };
            var pending = _session.ConfirmAsync();
            _remote.Gate.SetResult(false);
            await pending;
            Assert.That(_remote.Calls, Is.EqualTo(1));
            Assert.That(_session.LocksMutations, Is.False);
            Assert.That(_session.Message, Is.EqualTo(TownGemInstallSession.FailureMessage(code)));
            AssertUnchanged();
        }

        [Test]
        public async Task PendingInstall_LocksSiblingAndReopenedScreen_RejectsDuplicateIntent()
        {
            using var sibling = new TownGemInstallSession(_store, _coordinator, _catalog);
            int notifications = 0;
            sibling.Changed += () => notifications++;
            _session.Drop(InventoryList.Stash, 19, InventoryList.Loadout, 4);
            _remote.Error = new BackendError { error = "REVISION_CONFLICT" };
            var pending = _session.ConfirmAsync();
            using var reopened = new TownGemInstallSession(_store, _coordinator, _catalog);
            Assert.That(sibling.IsPending && sibling.LocksMutations && reopened.LocksMutations, Is.True);
            Assert.That(sibling.Begin(InventoryList.Stash, 19), Is.False);
            Assert.That(notifications, Is.EqualTo(1));
            _remote.Gate.SetResult(false);
            await pending;
            Assert.That(sibling.LocksMutations || reopened.LocksMutations, Is.False);
            Assert.That(notifications, Is.EqualTo(2));
            Assert.That(_remote.Calls, Is.EqualTo(1));
            AssertUnchanged();
        }

        private sealed class FakeRemote : IRemoteInventoryService, IRemoteGemInstallService
        {
            public int Calls;
            public InstallGemRequest Request;
            public MoveItemResult Result;
            public BackendError Error;
            public readonly TaskCompletionSource<bool> Gate = new();
            public async Task<(bool success, MoveItemResult data, BackendError error)> InstallGemAsync(InstallGemRequest request)
            { Calls++; Request = request; return (await Gate.Task, Result, Error); }
            public Task<(bool success, BackendError error)> OrderAsync(InventoryList list, IReadOnlyList<LootId> ids) => throw new NotSupportedException();
            public Task<(bool success, BackendError error)> MoveToLoadoutAsync(LootId id, int amount) => throw new NotSupportedException();
            public Task<(bool success, BackendError error)> MoveToStashAsync(LootId id, int amount) => throw new NotSupportedException();
            public Task<(bool success, BackendError error)> UpdatePreparedEquipmentAsync(PreparedEquipmentLoadout equipment) => throw new NotSupportedException();
        }

        private void AssertUnchanged()
        {
            Assert.That(_store.GetStash(), Is.EqualTo(new[] { new StashItem(_gem, 2, 19) }));
            Assert.That(_store.GetLoadout(), Is.EqualTo(new[] { new StashItem(_sword, 1, 4, _instance) }));
            Assert.That(_store.RemoteRevision, Is.EqualTo(7)); Assert.That(_commits, Is.Zero);
        }
    }
}
#endif
