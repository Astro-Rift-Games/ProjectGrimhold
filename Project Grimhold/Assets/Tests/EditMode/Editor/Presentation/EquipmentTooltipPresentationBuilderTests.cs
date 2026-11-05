#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class EquipmentTooltipPresentationBuilderTests
    {
        private readonly List<Object> _createdObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _createdObjects.Count - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(_createdObjects[index]);
            }
            _createdObjects.Clear();
        }

        [Test]
        public void StashPresenter_ConfirmedInstancesReachStashLoadoutAndPreparedEquipment()
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0f);
            SetPrivateField(loot, "_id", "sword");
            LootDefinition gem = CreateGemLoot("RES_RUBY_GEM", "Ruby", 5);
            LootDefinitionCatalog catalog = CreateCatalog(loot, gem);
            var modifiers = CreateInstance(CharacterAttribute.Strength, WeaponScalingGrade.S,
                CharacterAttribute.Strength, WeaponScalingGrade.None, 1, CreateGemSocket("RES_RUBY_GEM"), default);
            var instance = new EquipmentInstance("0123456789abcdef01234567", modifiers);
            var profile = new ProfileId("11111111111111111111111111111111");
            var repository = new InMemoryLocalProfileRepository();
            Assert.That(repository.Initialize(profile, catalog), Is.True);
            LocalProfileSnapshot snapshot = repository.Snapshot.Clone();
            snapshot.InventorySchemaVersion = 3;
            snapshot.CharacterAttributes = CreateAttributes(20, 0);
            snapshot.Stash.Add(new StashItem(loot.LootId, 1, 2,
                new EquipmentInstance("0123456789abcdef01234568", modifiers)));
            snapshot.Loadout.Add(new StashItem(loot.LootId, 1, 7,
                new EquipmentInstance("0123456789abcdef01234569", modifiers)));
            snapshot.PreparedEquipment = new PreparedEquipmentLoadout(loot.LootId, default, mainAInstance: instance);
            Assert.That(repository.TrySave(snapshot, out _), Is.True);
            var store = new LocalProfileStore(repository, profile, catalog);
            var contextObject = new GameObject("Stash instance test context");
            var context = contextObject.AddComponent<ApplicationStashContext>();
            var stash = contextObject.AddComponent<InMemoryPlayerStashService>();
            stash.Initialize(store);
            var loadout = contextObject.AddComponent<InMemoryPlayerLoadoutService>();
            loadout.Initialize(store);
            context.Initialize(store, stash, loadout, null, null);
            var parent = new GameObject("Stash instance test");
            parent.SetActive(false);
            LocalProfileProvider.SetRemoteCharacterId(profile);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/StashInventory.prefab");
                Assert.That(prefab, Is.Not.Null);
                var screen = Object.Instantiate(prefab, parent.transform);
                var presenter = screen.GetComponent<LobbyStashPresenter>();
                SetPrivateField(presenter, "_lootCatalog", catalog);
                parent.SetActive(true);
                screen.SetActive(true);
                var ui = screen.GetComponent<LobbyStashUI>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                // EditMode does not run ordinary MonoBehaviour lifecycle callbacks.
                typeof(LobbyStashUI).GetMethod("Awake", flags).Invoke(ui, null);
                typeof(LobbyStashPresenter).GetMethod("OnEnable", flags).Invoke(presenter, null);
                var stashPanel = (RaidLootPanelView)typeof(LobbyStashUI).GetField("_stashPanel", flags).GetValue(ui);
                var loadoutPanel = (RaidLootPanelView)typeof(LobbyStashUI).GetField("_loadoutPanel", flags).GetValue(ui);
                var prepared = (RaidInventorySlotView)typeof(LobbyStashUI).GetField("_weaponSetAMainHandView", flags).GetValue(ui);
                var cells = new[] { stashPanel.GetComponentsInChildren<RaidInventorySlotView>(true)[2],
                    loadoutPanel.GetComponentsInChildren<RaidInventorySlotView>(true)[7], prepared };
                foreach (var cell in cells)
                {
                    EquipmentTooltipPresentation shown = default;
                    cell.TooltipRequested += (tooltip, _) => shown = tooltip;
                    cell.OnPointerEnter(null);
                    Assert.That(shown.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
                    Assert.That(shown.Body, Does.Contain("Ranuras: 1 (Ruby)"));
                    Assert.That(shown.Body, Does.Contain("Daño efectivo: 42"));
                    cell.OnPointerExit(null);
                }
                Assert.That(store.GetStash()[0], Is.EqualTo(snapshot.Stash[0]));
                Assert.That(store.GetLoadout()[0], Is.EqualTo(snapshot.Loadout[0]));
                Assert.That(store.GetPreparedEquipment(), Is.EqualTo(snapshot.PreparedEquipment));
                Assert.That(store.RemoteRevision, Is.EqualTo(snapshot.RemoteRevision));
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(contextObject);
                LocalProfileProvider.ClearRemoteCharacterId();
            }
        }

        [Test]
        public void TownSlot_KeepsInstanceAndTooltipThroughRenameWithoutInventingRaidMetadata()
        {
            var loot = CreateWeaponLoot("Sword", 0f);
            SetPrivateField(loot, "_id", "sword");
            var modifiers = CreateInstance(CharacterAttribute.Strength, WeaponScalingGrade.S,
                CharacterAttribute.Strength, WeaponScalingGrade.None, 0, default, default);
            var instance = new EquipmentInstance("0123456789abcdef01234567", modifiers);
            var data = RaidInventorySlotData.Create(new LootEntry(loot.LootId, 1), loot, null,
                instance, CreateAttributes(20, 0), null);
            var renamed = data.WithDisplayName("Selected Sword");
            Assert.That(data.Instance, Is.SameAs(instance));
            Assert.That(renamed.Instance, Is.SameAs(instance));
            Assert.That(renamed.Tooltip, Is.EqualTo(data.Tooltip));
            Assert.That(renamed.Tooltip.Body, Does.Contain("Daño efectivo: 36"));
            Assert.That(renamed.HasInventorySlotAddress, Is.False);
            Assert.That(renamed.Origin, Is.Null);
            Assert.That(renamed.Amount, Is.EqualTo(1));
        }

        [Test]
        public void TownSlot_WithoutConfirmedAttributesDoesNotInventEffectiveDamage()
        {
            var loot = CreateWeaponLoot("Sword", 0f);
            SetPrivateField(loot, "_id", "sword");
            var instance = EquipmentInstance.PlainCache(loot);
            var data = RaidInventorySlotData.Create(new LootEntry(loot.LootId, 1), loot, null, instance);
            Assert.That(data.Instance, Is.SameAs(instance));
            Assert.That(data.Tooltip.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.InvalidEquipmentConfiguration));
            Assert.That(data.Tooltip.Body, Does.Not.Contain("Daño efectivo"));
        }

        [Test]
        public void Build_DefinitionOnlyMerchantOfferStillHasNoInstanceDamageOrSockets()
        {
            var loot = CreateWeaponLoot("Oferta", 0.70f);
            var offer = EquipmentTooltipPresentationBuilder.Build(loot);
            Assert.That(offer.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(offer.Body, Does.Contain("Escalado: FUE B"));
            Assert.That(offer.Body, Does.Not.Contain("Daño efectivo"));
            Assert.That(offer.Body, Does.Not.Contain("Ranuras"));
        }

        [TestCase(0.25f, "FUE E")]
        [TestCase(0.40f, "FUE D")]
        [TestCase(0.55f, "FUE C")]
        [TestCase(0.70f, "FUE B")]
        [TestCase(0.85f, "FUE A")]
        [TestCase(1.00f, "FUE S")]
        public void Build_ExactLegacyCoefficientUsesCanonicalGrade(float coefficient, string expected)
        {
            LootDefinition loot = CreateWeaponLoot("Espada", coefficient);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(presentation.Title, Is.EqualTo("Espada"));
            Assert.That(presentation.Body, Does.Contain($"Escalado: {expected}"));
        }

        [Test]
        public void Build_NonCanonicalLegacyCoefficientPreservesNumericValue()
        {
            LootDefinition loot = CreateWeaponLoot("Espada irregular", 0.63f);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Body, Does.Contain("Escalado: FUE ×0,63"));
            Assert.That(presentation.Body, Does.Not.Contain("FUE B"));
            Assert.That(presentation.Body, Does.Not.Contain("FUE C"));
        }

        [Test]
        public void Build_WeaponIncludesDefinitionOwnedFunctionalStatistics()
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0f);
            WeaponDefinition weapon = loot.WeaponDefinition;
            SetPrivateField(weapon, "_attributeRequirements", new WeaponAttributeRequirements(10, 0, 5));

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Body, Does.Contain("Daño base: 30 (Físico)"));
            Assert.That(presentation.Body, Does.Contain("Intervalo: 0,75 s"));
            Assert.That(presentation.Body, Does.Contain("Alcance: 1,5"));
            Assert.That(presentation.Body, Does.Contain("Costo de Stamina: 8"));
            Assert.That(presentation.Body, Does.Contain("Manos: 1"));
            Assert.That(presentation.Body, Does.Contain("Requisito: FUE 10 · INT 5"));
            Assert.That(
                presentation.Body,
                Does.Contain($"Requisito: FUE 10 · INT 5{System.Environment.NewLine}Escalado: Sin escalado"));
            Assert.That(presentation.Body, Does.Contain("Escalado: Sin escalado"));
        }

        [TestCase(MaximumResourceType.Health, "Bono: +5 Vida máxima")]
        [TestCase(MaximumResourceType.Stamina, "Bono: +5 Stamina máxima")]
        [TestCase(MaximumResourceType.Mana, "Bono: +5 Mana máximo")]
        public void Build_ArmorIncludesDefenseAndDirectResourceBonus(
            MaximumResourceType resource,
            string expectedBonus)
        {
            ArmorDefinition armor = Create<ArmorDefinition>();
            SetPrivateField(armor, "_physicalDefense", 16);
            SetPrivateField(armor, "_magicalDefense", 4);
            SetPrivateField(armor, "_maximumResourceModifier", new MaximumResourceModifier(resource, 5));
            LootDefinition loot = CreateLoot("Casco", LootCategory.Helmet);
            SetPrivateField(loot, "_armorDefinition", armor);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(presentation.Body, Does.Contain("Defensa Física: 16"));
            Assert.That(presentation.Body, Does.Contain("Defensa Mágica: 4"));
            Assert.That(presentation.Body, Does.Contain(expectedBonus));
        }

        [Test]
        public void Build_CurrentNonEquipmentCategoryShowsNoStatisticsMessage()
        {
            LootDefinition loot = CreateLoot("Mineral", LootCategory.Material);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Title, Is.EqualTo("Mineral"));
            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.NoEquipmentStatistics));
            Assert.That(presentation.Body, Is.EqualTo("Sin estadísticas de equipamiento"));
        }

        [TestCase(LootCategory.Weapon)]
        [TestCase(LootCategory.Shield)]
        [TestCase(LootCategory.Armor)]
        public void Build_MissingFunctionalDefinitionKeepsTitleAndReportsInvalidConfiguration(
            LootCategory category)
        {
            LootDefinition loot = CreateLoot("Objeto roto", category);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Title, Is.EqualTo("Objeto roto"));
            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.InvalidEquipmentConfiguration));
            Assert.That(presentation.Body, Is.EqualTo("Estadísticas no disponibles por configuración inválida"));
        }

        [Test]
        public void Build_ShieldShowsReductionAndDefensiveCone()
        {
            ShieldDefinition shield = Create<ShieldDefinition>();
            LootDefinition loot = CreateLoot("Escudo de entrenamiento", LootCategory.Shield);
            SetPrivateField(loot, "_shieldDefinition", shield);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(presentation.Body, Does.Contain("Reducción al defender: 50%"));
            Assert.That(presentation.Body, Does.Contain("Cono frontal: 120°"));
        }

        [Test]
        public void Build_UnresolvedDefinitionCannotShowTooltip()
        {
            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(null);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.UnresolvedDefinition));
            Assert.That(presentation.CanShow, Is.False);
            Assert.That(presentation.Title, Is.Empty);
        }

        [Test]
        public void BuildInstance_ShowsDualScalingGemSocketsAndEffectiveDamage()
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0f);
            WeaponInstanceModifiers instance = CreateInstance(
                CharacterAttribute.Strength,
                WeaponScalingGrade.A,
                CharacterAttribute.Vitality,
                WeaponScalingGrade.D,
                2,
                default,
                default);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot, instance, CreateAttributes(strength: 20, vitality: 10), null);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(presentation.Title, Is.EqualTo("Espada"));
            Assert.That(presentation.Body, Does.Contain("Daño base: 30 (Físico)"));
            Assert.That(
                presentation.Body,
                Does.Contain(
                    $"Escalado: FUE A · VIT D{System.Environment.NewLine}" +
                    $"Ranuras: 2 (vacías){System.Environment.NewLine}" +
                    "Daño efectivo: 36"));
        }

        [Test]
        public void BuildInstance_WithoutModifiers_UsesBaseDamageAndIgnoresLegacyScaling()
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0.70f);
            WeaponInstanceModifiers instance = CreateInstance(
                CharacterAttribute.Strength,
                WeaponScalingGrade.None,
                CharacterAttribute.Strength,
                WeaponScalingGrade.None,
                0,
                default,
                default);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot, instance, CreateAttributes(strength: 30, vitality: 0), null);

            Assert.That(presentation.Body, Does.Contain("Escalado: Sin escalado"));
            Assert.That(presentation.Body, Does.Contain("Ranuras: 0"));
            Assert.That(presentation.Body, Does.Contain("Daño efectivo: 30"));
            Assert.That(presentation.Body, Does.Not.Contain("FUE B"));
            Assert.That(presentation.Body, Does.Not.Contain("Bono de gemas"));
        }

        [TestCase(1, true, false, "Ranuras: 1 (Attunement Gem)")]
        [TestCase(1, false, false, "Ranuras: 1 (vacía)")]
        [TestCase(2, false, false, "Ranuras: 2 (vacías)")]
        [TestCase(2, true, false, "Ranuras: 2 (Attunement Gem, vacía)")]
        [TestCase(2, true, true, "Ranuras: 2 (Attunement Gem, Attunement Gem)")]
        public void BuildInstance_ShowsGemSocketOccupancy(
            int socketCount,
            bool firstOccupied,
            bool secondOccupied,
            string expected)
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0f);
            WeaponInstanceModifiers instance = CreateInstance(
                CharacterAttribute.Strength,
                WeaponScalingGrade.C,
                CharacterAttribute.Strength,
                WeaponScalingGrade.None,
                socketCount,
                firstOccupied ? CreateGemSocket() : default,
                secondOccupied ? CreateGemSocket() : default);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(
                    loot,
                    instance,
                    CreateAttributes(strength: 0, vitality: 0),
                    CreateCatalog(CreateGemLoot("RES_ATTUNEMENT_GEM", "Attunement Gem", 5)));

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(presentation.Body, Does.Contain(expected));
        }

        [Test]
        public void BuildInstance_WithInstalledGems_ShowsGemNamesAndAddsBonusToEffectiveDamage()
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0f);
            WeaponInstanceModifiers instance = CreateInstance(
                CharacterAttribute.Strength,
                WeaponScalingGrade.A,
                CharacterAttribute.Strength,
                WeaponScalingGrade.None,
                2,
                CreateGemSocket("RES_RUBY_GEM"),
                CreateGemSocket("RES_SAPPHIRE_GEM"));
            LootDefinitionCatalog catalog = CreateCatalog(
                CreateGemLoot("RES_RUBY_GEM", "Ruby Gem", 5),
                CreateGemLoot("RES_SAPPHIRE_GEM", "Sapphire Gem", 5));
            CharacterAttributeState attributes = CreateAttributes(strength: 20, vitality: 0);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot, instance, attributes, catalog);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.FunctionalStatistics));
            Assert.That(
                presentation.Body,
                Does.Contain(
                    $"Escalado: FUE A{System.Environment.NewLine}" +
                    $"Ranuras: 2 (Ruby Gem, Sapphire Gem){System.Environment.NewLine}" +
                    $"Bono de gemas: +10{System.Environment.NewLine}" +
                    "Daño efectivo: 46"));

            Assert.That(WeaponGemDamageBonusResolver.TryResolve(instance, catalog, out int bonus), Is.True);
            Assert.That(
                WeaponScalingContributionsResolver.TryResolve(
                    instance, CharacterAttribute.Strength, out WeaponScalingContributions contributions),
                Is.True);
            Assert.That(
                WeaponDamageCalculator.TryCalculate(30f, bonus, attributes, contributions, out float combatDamage),
                Is.True);
            Assert.That(presentation.Body, Does.Contain($"Daño efectivo: {combatDamage}"));
        }

        [Test]
        public void BuildInstance_WithUnresolvableInstalledGem_ReportsInvalidConfiguration()
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0f);
            WeaponInstanceModifiers instance = CreateInstance(
                CharacterAttribute.Strength,
                WeaponScalingGrade.None,
                CharacterAttribute.Strength,
                WeaponScalingGrade.None,
                1,
                CreateGemSocket("RES_RUBY_GEM"),
                default);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(
                    loot, instance, CreateAttributes(strength: 0, vitality: 0), null);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.InvalidEquipmentConfiguration));
            Assert.That(presentation.Body, Is.EqualTo("Estadísticas no disponibles por configuración inválida"));
        }

        [Test]
        public void BuildInstance_WithPrimaryOutsideNaturalAttribute_ReportsInvalidConfiguration()
        {
            LootDefinition loot = CreateWeaponLoot("Espada", 0f);
            WeaponInstanceModifiers instance = CreateInstance(
                CharacterAttribute.Dexterity,
                WeaponScalingGrade.B,
                CharacterAttribute.Dexterity,
                WeaponScalingGrade.None,
                0,
                default,
                default);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot, instance, CreateAttributes(strength: 10, vitality: 0), null);

            Assert.That(presentation.Title, Is.EqualTo("Espada"));
            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.InvalidEquipmentConfiguration));
            Assert.That(presentation.Body, Is.EqualTo("Estadísticas no disponibles por configuración inválida"));
        }

        [Test]
        public void BuildInstance_NonWeaponDefinition_MatchesDefinitionOnlyPresentation()
        {
            LootDefinition loot = CreateLoot("Mineral", LootCategory.Resource);

            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(loot, default, CreateAttributes(strength: 0, vitality: 0), null);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.NoEquipmentStatistics));
            Assert.That(presentation.Body, Is.EqualTo("Sin estadísticas de equipamiento"));
        }

        [Test]
        public void BuildInstance_NullDefinition_CannotShowTooltip()
        {
            EquipmentTooltipPresentation presentation =
                EquipmentTooltipPresentationBuilder.Build(null, default, CreateAttributes(strength: 0, vitality: 0), null);

            Assert.That(presentation.Status, Is.EqualTo(EquipmentTooltipPresentationStatus.UnresolvedDefinition));
        }

        private static WeaponInstanceModifiers CreateInstance(
            CharacterAttribute primaryAttribute,
            WeaponScalingGrade primaryGrade,
            CharacterAttribute secondaryAttribute,
            WeaponScalingGrade secondaryGrade,
            int socketCount,
            WeaponGemSocket first,
            WeaponGemSocket second)
        {
            WeaponScalingModifier primary = default;
            WeaponScalingModifier secondary = default;
            if (primaryGrade != WeaponScalingGrade.None)
            {
                Assert.That(WeaponScalingModifier.TryCreate(primaryAttribute, primaryGrade, out primary, out _), Is.True);
            }

            if (secondaryGrade != WeaponScalingGrade.None)
            {
                Assert.That(WeaponScalingModifier.TryCreate(secondaryAttribute, secondaryGrade, out secondary, out _), Is.True);
            }

            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    primary,
                    secondary,
                    primaryAttribute,
                    socketCount,
                    first,
                    second,
                    out WeaponInstanceModifiers modifiers,
                    out string error),
                Is.True,
                error);
            return modifiers;
        }

        private static WeaponGemSocket CreateGemSocket(string gemId = "RES_ATTUNEMENT_GEM")
        {
            Assert.That(
                WeaponGemSocket.TryCreateOccupied(new LootId(gemId), out WeaponGemSocket socket, out string error),
                Is.True,
                error);
            return socket;
        }

        private LootDefinition CreateGemLoot(string id, string displayName, int damageBonus)
        {
            GemDefinition gem = Create<GemDefinition>();
            SetPrivateField(gem, "_damageBonus", damageBonus);

            LootDefinition loot = CreateLoot(displayName, LootCategory.Resource);
            SetPrivateField(loot, "_id", id);
            SetPrivateField(loot, "_gemDefinition", gem);
            return loot;
        }

        private LootDefinitionCatalog CreateCatalog(params LootDefinition[] definitions)
        {
            LootDefinitionCatalog catalog = Create<LootDefinitionCatalog>();
            SetPrivateField(catalog, "_definitions", new List<LootDefinition>(definitions));
            SetPrivateField(catalog, "_isCacheDirty", true);
            return catalog;
        }

        private static CharacterAttributeState CreateAttributes(int strength, int vitality)
        {
            Assert.That(
                CharacterAttributeState.TryCreate(vitality, 0, strength, 0, 0, 0, 0, out CharacterAttributeState state),
                Is.True);
            return state;
        }

        private LootDefinition CreateWeaponLoot(string displayName, float coefficient)
        {
            MeleeAttackConfig attack = Create<MeleeAttackConfig>();
            SetPrivateField(attack, "_radius", 0.5f);
            SetPrivateField(attack, "_maximumTargets", 1);
            SetPrivateField(attack, "_targetLayerMask", new LayerMask { value = 1 });

            WeaponDefinition weapon = Create<WeaponDefinition>();
            SetPrivateField(weapon, "_baseDamage", 30f);
            SetPrivateField(weapon, "_attackIntervalSeconds", 0.75f);
            SetPrivateField(weapon, "_range", 1.5f);
            SetPrivateField(weapon, "_staminaCost", 8f);
            SetPrivateField(weapon, "_damageType", DamageType.Physical);
            SetPrivateField(weapon, "_knockbackForce", 0f);
            SetPrivateField(weapon, "_handedness", WeaponHandedness.OneHanded);
            SetPrivateField(weapon, "_primaryAttack", attack);
            SetPrivateField(weapon, "_naturalScalingAttribute", CharacterAttribute.Strength);
            SetPrivateField(
                weapon,
                "_offensiveScaling",
                new WeaponOffensiveScaling(CharacterAttribute.Strength, coefficient));

            LootDefinition loot = CreateLoot(displayName, LootCategory.Weapon);
            SetPrivateField(loot, "_weaponDefinition", weapon);
            return loot;
        }

        private LootDefinition CreateLoot(string displayName, LootCategory category)
        {
            LootDefinition loot = Create<LootDefinition>();
            SetPrivateField(loot, "_displayName", displayName);
            SetPrivateField(loot, "_category", category);
            return loot;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Type type = target.GetType();
            FieldInfo field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
