#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Equipment
{
    public sealed class WeaponGemInstallationTests
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
        public void TryInstallGem_WithEmptySocket_OccupiesItWithTheGemLootId()
        {
            LootDefinition ruby = CreateGem("RES_RUBY_GEM", 5);
            WeaponInstanceModifiers weapon = CreateWeapon(1, default, default);

            bool installed = weapon.TryInstallGem(
                ruby,
                out WeaponInstanceModifiers result,
                out WeaponGemInstallFailure failure);

            Assert.That(installed, Is.True);
            Assert.That(failure, Is.EqualTo(WeaponGemInstallFailure.None));
            Assert.That(result.GemSocketCount, Is.EqualTo(1));
            Assert.That(result.TryGetGemSocket(0, out WeaponGemSocket socket), Is.True);
            Assert.That(socket.GemLootId, Is.EqualTo(new LootId("RES_RUBY_GEM")));
            Assert.That(result.TryValidate(CharacterAttribute.Strength, out string error), Is.True, error);
        }

        [Test]
        public void TryInstallGem_WithFirstSocketOccupied_UsesTheNextEmptySocket()
        {
            LootDefinition sapphire = CreateGem("RES_SAPPHIRE_GEM", 5);
            WeaponInstanceModifiers weapon = CreateWeapon(2, CreateSocket("RES_RUBY_GEM"), default);

            Assert.That(weapon.TryInstallGem(sapphire, out WeaponInstanceModifiers result, out _), Is.True);

            Assert.That(result.TryGetGemSocket(0, out WeaponGemSocket first), Is.True);
            Assert.That(result.TryGetGemSocket(1, out WeaponGemSocket second), Is.True);
            Assert.That(first.GemLootId, Is.EqualTo(new LootId("RES_RUBY_GEM")));
            Assert.That(second.GemLootId, Is.EqualTo(new LootId("RES_SAPPHIRE_GEM")));
        }

        [Test]
        public void TryInstallGem_InstallationPreservesScalingModifiers()
        {
            LootDefinition ruby = CreateGem("RES_RUBY_GEM", 5);
            Assert.That(
                WeaponScalingModifier.TryCreate(
                    CharacterAttribute.Strength, WeaponScalingGrade.A, out WeaponScalingModifier primary, out _),
                Is.True);
            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    primary, default, CharacterAttribute.Strength, 1, default, default,
                    out WeaponInstanceModifiers weapon, out string error),
                Is.True,
                error);

            Assert.That(weapon.TryInstallGem(ruby, out WeaponInstanceModifiers result, out _), Is.True);

            Assert.That(result.Primary, Is.EqualTo(primary));
            Assert.That(result.HasSecondary, Is.False);
        }

        [Test]
        public void TryInstallGem_WithAllSocketsOccupied_IsRejectedWithoutChangingTheWeapon()
        {
            LootDefinition ruby = CreateGem("RES_RUBY_GEM", 5);
            WeaponInstanceModifiers weapon = CreateWeapon(
                2,
                CreateSocket("RES_ATTUNEMENT_GEM"),
                CreateSocket("RES_SAPPHIRE_GEM"));
            WeaponInstanceModifiers original = weapon;

            bool installed = weapon.TryInstallGem(
                ruby,
                out WeaponInstanceModifiers result,
                out WeaponGemInstallFailure failure);

            Assert.That(installed, Is.False);
            Assert.That(failure, Is.EqualTo(WeaponGemInstallFailure.NoEmptyGemSocket));
            AssertSameSockets(weapon, original);
            AssertSameSockets(result, original);
        }

        [Test]
        public void TryInstallGem_OnWeaponWithoutSockets_IsRejected()
        {
            LootDefinition ruby = CreateGem("RES_RUBY_GEM", 5);
            WeaponInstanceModifiers weapon = CreateWeapon(0, default, default);

            bool installed = weapon.TryInstallGem(
                ruby,
                out WeaponInstanceModifiers result,
                out WeaponGemInstallFailure failure);

            Assert.That(installed, Is.False);
            Assert.That(failure, Is.EqualTo(WeaponGemInstallFailure.NoGemSockets));
            Assert.That(result.GemSocketCount, Is.EqualTo(0));
        }

        [Test]
        public void TryInstallGem_WithLootThatIsNotAGem_IsRejected()
        {
            LootDefinition ore = Create<LootDefinition>();
            SetPrivateField(ore, "_id", "RES_BLACK_IRON");
            SetPrivateField(ore, "_category", LootCategory.Resource);
            WeaponInstanceModifiers weapon = CreateWeapon(1, default, default);

            bool installed = weapon.TryInstallGem(
                ore,
                out WeaponInstanceModifiers result,
                out WeaponGemInstallFailure failure);

            Assert.That(installed, Is.False);
            Assert.That(failure, Is.EqualTo(WeaponGemInstallFailure.NotAGem));
            Assert.That(result.TryGetGemSocket(0, out WeaponGemSocket socket), Is.True);
            Assert.That(socket.IsOccupied, Is.False);
        }

        [Test]
        public void TryInstallGem_WithNullGem_IsRejected()
        {
            WeaponInstanceModifiers weapon = CreateWeapon(1, default, default);

            Assert.That(
                weapon.TryInstallGem(null, out _, out WeaponGemInstallFailure failure),
                Is.False);
            Assert.That(failure, Is.EqualTo(WeaponGemInstallFailure.NotAGem));
        }

        [Test]
        public void DamageBonusResolver_SumsEveryInstalledGem()
        {
            LootDefinitionCatalog catalog = CreateCatalog(
                CreateGem("RES_RUBY_GEM", 5),
                CreateGem("RES_SAPPHIRE_GEM", 7));
            WeaponInstanceModifiers weapon = CreateWeapon(
                2,
                CreateSocket("RES_RUBY_GEM"),
                CreateSocket("RES_SAPPHIRE_GEM"));

            Assert.That(WeaponGemDamageBonusResolver.TryResolve(weapon, catalog, out int bonus), Is.True);
            Assert.That(bonus, Is.EqualTo(12));
        }

        [Test]
        public void DamageBonusResolver_WithEmptySocketsAndNoCatalog_ReturnsZero()
        {
            WeaponInstanceModifiers weapon = CreateWeapon(2, default, default);

            Assert.That(WeaponGemDamageBonusResolver.TryResolve(weapon, null, out int bonus), Is.True);
            Assert.That(bonus, Is.EqualTo(0));
        }

        [Test]
        public void DamageBonusResolver_WithGemMissingFromCatalog_IsRejected()
        {
            LootDefinitionCatalog catalog = CreateCatalog(CreateGem("RES_RUBY_GEM", 5));
            WeaponInstanceModifiers weapon = CreateWeapon(1, CreateSocket("RES_SAPPHIRE_GEM"), default);

            Assert.That(WeaponGemDamageBonusResolver.TryResolve(weapon, catalog, out int bonus), Is.False);
            Assert.That(bonus, Is.EqualTo(0));
        }

        [Test]
        public void DamageBonusResolver_WithInstalledLootThatIsNotAGem_IsRejected()
        {
            LootDefinition ore = Create<LootDefinition>();
            SetPrivateField(ore, "_id", "RES_BLACK_IRON");
            LootDefinitionCatalog catalog = CreateCatalog(ore);
            WeaponInstanceModifiers weapon = CreateWeapon(1, CreateSocket("RES_BLACK_IRON"), default);

            Assert.That(WeaponGemDamageBonusResolver.TryResolve(weapon, catalog, out _), Is.False);
        }

        private static void AssertSameSockets(in WeaponInstanceModifiers actual, in WeaponInstanceModifiers expected)
        {
            Assert.That(actual.GemSocketCount, Is.EqualTo(expected.GemSocketCount));
            for (int index = 0; index < expected.GemSocketCount; index++)
            {
                Assert.That(actual.TryGetGemSocket(index, out WeaponGemSocket actualSocket), Is.True);
                Assert.That(expected.TryGetGemSocket(index, out WeaponGemSocket expectedSocket), Is.True);
                Assert.That(actualSocket.GemLootId, Is.EqualTo(expectedSocket.GemLootId));
            }
        }

        private static WeaponInstanceModifiers CreateWeapon(
            int socketCount,
            WeaponGemSocket first,
            WeaponGemSocket second)
        {
            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    default, default, CharacterAttribute.Strength, socketCount, first, second,
                    out WeaponInstanceModifiers modifiers, out string error),
                Is.True,
                error);
            return modifiers;
        }

        private static WeaponGemSocket CreateSocket(string gemId)
        {
            Assert.That(
                WeaponGemSocket.TryCreateOccupied(new LootId(gemId), out WeaponGemSocket socket, out string error),
                Is.True,
                error);
            return socket;
        }

        private LootDefinition CreateGem(string id, int damageBonus)
        {
            GemDefinition gem = Create<GemDefinition>();
            SetPrivateField(gem, "_damageBonus", damageBonus);

            LootDefinition loot = Create<LootDefinition>();
            SetPrivateField(loot, "_id", id);
            SetPrivateField(loot, "_displayName", id);
            SetPrivateField(loot, "_category", LootCategory.Resource);
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

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _createdObjects.Add(instance);
            return instance;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }
    }
}
#endif
