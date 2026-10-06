#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class TownAbilityMutationEndpointTests
    {
        private static readonly ProfileId Profile = new("94949494949494949494949494949494");
        private static readonly AbilityId Charge = new("charge");
        private static readonly AbilityId Trap = new("trap");
        private static readonly AbilityId Arcane = new("arcane_projectile");

        private LootDefinitionCatalog _lootCatalog;
        private AbilityDefinition[] _definitions;
        private AbilityDefinitionCatalog _abilityCatalog;
        private LocalProfileStore _store;
        private bool _canMutate;
        private int _mutated;
        private TownAbilityMutationEndpoint _endpoint;

        [SetUp]
        public void SetUp()
        {
            _lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(_lootCatalog, Is.Not.Null);

            _definitions = new[]
            {
                AbilityTestFactory.CreateDefinition(
                    "charge",
                    requirements: new CharacterAttributeRequirement(CharacterAttribute.Strength, 10)),
                AbilityTestFactory.CreateDefinition("trap"),
                AbilityTestFactory.CreateDefinition(
                    "arcane_projectile",
                    requirements: new CharacterAttributeRequirement(CharacterAttribute.Intelligence, 10))
            };
            _abilityCatalog = AbilityTestFactory.CreateCatalog(_definitions);

            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(Profile, _lootCatalog), Is.True);
            _store = new LocalProfileStore(
                repository,
                Profile,
                lootCatalog: _lootCatalog,
                abilityCatalog: _abilityCatalog);
            Assert.That(_store.TryUnlockAbility(Charge), Is.EqualTo(AbilityUnlockResult.Success));
            Assert.That(_store.TryUnlockAbility(Trap), Is.EqualTo(AbilityUnlockResult.Success));
            _store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 10));

            _canMutate = true;
            _mutated = 0;
            _endpoint = new TownAbilityMutationEndpoint(_store, _abilityCatalog, () => _canMutate, () => _mutated++);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_abilityCatalog);
            foreach (AbilityDefinition definition in _definitions)
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TryEquip_WhenReady_PersistsAndNotifiesOnce()
        {
            TownAbilityMutationResult result = _endpoint.TryEquip(UniversalAbilitySlot.Slot1, Charge);

            Assert.That(result.Outcome, Is.EqualTo(TownAbilityMutationOutcome.Success));
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(_store.GetPreparedAbilities().Slot1, Is.EqualTo(Charge));
            Assert.That(_mutated, Is.EqualTo(1));
        }

        [Test]
        public void TryEquip_WhenBlockedByReadyState_DoesNotTouchTheStoreOrNotify()
        {
            _canMutate = false;
            int commits = 0;
            _store.ProfileCommitted += _ => commits++;

            TownAbilityMutationResult result = _endpoint.TryEquip(UniversalAbilitySlot.Slot1, Charge);

            Assert.That(result.Outcome, Is.EqualTo(TownAbilityMutationOutcome.BlockedByReadyState));
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(_store.GetPreparedAbilities().Slot1.IsValid, Is.False);
            Assert.That(commits, Is.Zero);
            Assert.That(_mutated, Is.Zero);
        }

        [Test]
        public void TryClear_WhenBlockedByReadyState_LeavesPreparedAbilityUntouched()
        {
            Assert.That(
                _endpoint.TryEquip(UniversalAbilitySlot.Slot2, Trap).IsSuccess, Is.True);
            _mutated = 0;
            _canMutate = false;

            TownAbilityMutationResult result = _endpoint.TryClear(UniversalAbilitySlot.Slot2);

            Assert.That(result.Outcome, Is.EqualTo(TownAbilityMutationOutcome.BlockedByReadyState));
            Assert.That(_store.GetPreparedAbilities().Slot2, Is.EqualTo(Trap));
            Assert.That(_mutated, Is.Zero);
        }

        [Test]
        public void TryEquip_DuplicateInOtherSlot_IsRejectedWithoutNotification()
        {
            Assert.That(_endpoint.TryEquip(UniversalAbilitySlot.Slot1, Charge).IsSuccess, Is.True);
            _mutated = 0;

            TownAbilityMutationResult result = _endpoint.TryEquip(UniversalAbilitySlot.Slot2, Charge);

            Assert.That(result.Outcome, Is.EqualTo(TownAbilityMutationOutcome.Rejected));
            Assert.That(result.Preparation, Is.EqualTo(AbilityPreparationResult.DuplicateAbility));
            Assert.That(_store.GetPreparedAbilities().Slot2.IsValid, Is.False);
            Assert.That(_mutated, Is.Zero);
        }

        [Test]
        public void TryEquip_RequirementsNotMet_IsRejected()
        {
            Assert.That(_store.TryUnlockAbility(Arcane), Is.EqualTo(AbilityUnlockResult.Success));

            TownAbilityMutationResult result = _endpoint.TryEquip(UniversalAbilitySlot.Slot1, Arcane);

            Assert.That(result.Outcome, Is.EqualTo(TownAbilityMutationOutcome.Rejected));
            Assert.That(result.Preparation, Is.EqualTo(AbilityPreparationResult.AttributeRequirementsNotMet));
            Assert.That(_store.GetPreparedAbilities().Slot1.IsValid, Is.False);
            Assert.That(_mutated, Is.Zero);
        }

        [Test]
        public void TryEquip_NotUnlocked_IsRejected()
        {
            TownAbilityMutationResult result = _endpoint.TryEquip(UniversalAbilitySlot.Slot1, Arcane);

            Assert.That(result.Outcome, Is.EqualTo(TownAbilityMutationOutcome.Rejected));
            Assert.That(result.Preparation, Is.EqualTo(AbilityPreparationResult.AbilityNotUnlocked));
            Assert.That(_mutated, Is.Zero);
        }

        [Test]
        public void TryClear_FilledSlot_ClearsAndNotifiesOnce()
        {
            Assert.That(_endpoint.TryEquip(UniversalAbilitySlot.Slot1, Charge).IsSuccess, Is.True);
            _mutated = 0;

            TownAbilityMutationResult result = _endpoint.TryClear(UniversalAbilitySlot.Slot1);

            Assert.That(result.Outcome, Is.EqualTo(TownAbilityMutationOutcome.Success));
            Assert.That(_store.GetPreparedAbilities().Slot1.IsValid, Is.False);
            Assert.That(_mutated, Is.EqualTo(1));
        }

        [Test]
        public void TryClear_EmptySlot_Succeeds()
        {
            TownAbilityMutationResult result = _endpoint.TryClear(UniversalAbilitySlot.Slot2);

            Assert.That(result.Outcome, Is.EqualTo(TownAbilityMutationOutcome.Success));
            Assert.That(_store.GetPreparedAbilities().Slot2.IsValid, Is.False);
        }

        [Test]
        public void CanEquip_ReflectsReadyGateAndPreparationRulesWithoutMutating()
        {
            Assert.That(_endpoint.CanMutate, Is.True);
            Assert.That(_endpoint.CanEquip(Charge, UniversalAbilitySlot.Slot1), Is.True);
            Assert.That(_endpoint.CanEquip(Arcane, UniversalAbilitySlot.Slot1), Is.False, "Not unlocked.");

            Assert.That(_endpoint.TryEquip(UniversalAbilitySlot.Slot1, Charge).IsSuccess, Is.True);
            Assert.That(
                _endpoint.CanEquip(Charge, UniversalAbilitySlot.Slot2), Is.False, "Already in the other slot.");
            Assert.That(_endpoint.CanEquip(Trap, UniversalAbilitySlot.Slot2), Is.True);

            _store.ForceCharacterAttributeState(AbilityTestFactory.CreateAttributes(strength: 9));
            Assert.That(_endpoint.CanEquip(Trap, UniversalAbilitySlot.Slot2), Is.True);
            Assert.That(
                _endpoint.CanEquip(Charge, UniversalAbilitySlot.Slot1), Is.False, "Requirements no longer met.");

            _canMutate = false;
            Assert.That(_endpoint.CanMutate, Is.False);
            Assert.That(_endpoint.CanEquip(Trap, UniversalAbilitySlot.Slot2), Is.False);
            Assert.That(_store.GetPreparedAbilities().Slot2.IsValid, Is.False);
        }
    }
}
#endif
