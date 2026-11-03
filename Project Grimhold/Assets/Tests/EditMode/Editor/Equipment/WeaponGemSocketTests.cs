#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Equipment
{
    public sealed class WeaponGemSocketTests
    {
        private const string GemId = "RES_ATTUNEMENT_GEM";
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
        public void DefaultSocket_IsEmpty()
        {
            WeaponGemSocket socket = default;

            Assert.That(socket.IsOccupied, Is.False);
            Assert.That(socket.GemLootId.IsValid, Is.False);
        }

        [Test]
        public void TryCreateOccupied_WithValidGem_StoresGemLootId()
        {
            Assert.That(
                WeaponGemSocket.TryCreateOccupied(new LootId(GemId), out WeaponGemSocket socket, out string error),
                Is.True,
                error);

            Assert.That(socket.IsOccupied, Is.True);
            Assert.That(socket.GemLootId, Is.EqualTo(new LootId(GemId)));
        }

        [Test]
        public void TryCreateOccupied_WithInvalidGem_IsRejected()
        {
            Assert.That(
                WeaponGemSocket.TryCreateOccupied(default, out WeaponGemSocket socket, out string error),
                Is.False);
            Assert.That(socket.IsOccupied, Is.False);
            Assert.That(error, Does.Contain("valid gem"));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TryCreate_AcceptsZeroToTwoGemSockets(int socketCount)
        {
            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    default,
                    default,
                    CharacterAttribute.Strength,
                    socketCount,
                    default,
                    default,
                    out WeaponInstanceModifiers modifiers,
                    out string error),
                Is.True,
                error);
            Assert.That(modifiers.GemSocketCount, Is.EqualTo(socketCount));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void TryCreate_WithGemSocketCountOutsideRange_IsRejected(int socketCount)
        {
            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    default,
                    default,
                    CharacterAttribute.Strength,
                    socketCount,
                    default,
                    default,
                    out WeaponInstanceModifiers modifiers,
                    out string error),
                Is.False);
            Assert.That(modifiers.GemSocketCount, Is.Zero);
            Assert.That(error, Does.Contain("between 0 and 2"));
        }

        [Test]
        public void TryCreate_WithGemBeyondSocketCount_IsRejected()
        {
            WeaponGemSocket gem = CreateOccupiedSocket();

            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    default,
                    default,
                    CharacterAttribute.Strength,
                    1,
                    default,
                    gem,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain("beyond"));
        }

        [Test]
        public void TryGetGemSocket_ExposesOnlyExistingSockets()
        {
            WeaponGemSocket gem = CreateOccupiedSocket();
            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    default,
                    default,
                    CharacterAttribute.Strength,
                    2,
                    default,
                    gem,
                    out WeaponInstanceModifiers modifiers,
                    out string error),
                Is.True,
                error);

            Assert.That(modifiers.TryGetGemSocket(0, out WeaponGemSocket first), Is.True);
            Assert.That(first.IsOccupied, Is.False);
            Assert.That(modifiers.TryGetGemSocket(1, out WeaponGemSocket second), Is.True);
            Assert.That(second.GemLootId, Is.EqualTo(new LootId(GemId)));
            Assert.That(modifiers.TryGetGemSocket(2, out _), Is.False);
            Assert.That(modifiers.TryGetGemSocket(-1, out _), Is.False);
        }

        [Test]
        public void LegacyTryCreate_HasNoGemSockets()
        {
            Assert.That(
                WeaponInstanceModifiers.TryCreate(
                    default,
                    default,
                    CharacterAttribute.Strength,
                    out WeaponInstanceModifiers modifiers,
                    out string error),
                Is.True,
                error);
            Assert.That(modifiers.GemSocketCount, Is.Zero);
        }

        [Test]
        public void NetworkData_RoundTripsScalingAndGemSockets()
        {
            LootDefinitionCatalog catalog = CreateCatalog(GemId, "RES_BONE");
            WeaponInstanceModifiers source = CreateModifiers(
                CharacterAttribute.Dexterity,
                WeaponScalingGrade.A,
                CharacterAttribute.Luck,
                WeaponScalingGrade.E,
                2,
                CreateOccupiedSocket(),
                default);

            Assert.That(
                WeaponInstanceModifiersNetworkData.TryCreate(
                    source,
                    CharacterAttribute.Dexterity,
                    catalog,
                    out WeaponInstanceModifiersNetworkData data,
                    out string createError),
                Is.True,
                createError);
            Assert.That(
                data.TryResolve(
                    catalog,
                    CharacterAttribute.Dexterity,
                    out WeaponInstanceModifiers resolved,
                    out string resolveError),
                Is.True,
                resolveError);

            Assert.That(resolved.Primary.Attribute, Is.EqualTo(CharacterAttribute.Dexterity));
            Assert.That(resolved.Primary.Grade, Is.EqualTo(WeaponScalingGrade.A));
            Assert.That(resolved.Secondary.Attribute, Is.EqualTo(CharacterAttribute.Luck));
            Assert.That(resolved.Secondary.Grade, Is.EqualTo(WeaponScalingGrade.E));
            Assert.That(resolved.GemSocketCount, Is.EqualTo(2));
            Assert.That(resolved.TryGetGemSocket(0, out WeaponGemSocket first), Is.True);
            Assert.That(first.GemLootId, Is.EqualTo(new LootId(GemId)));
            Assert.That(resolved.TryGetGemSocket(1, out WeaponGemSocket second), Is.True);
            Assert.That(second.IsOccupied, Is.False);
        }

        [Test]
        public void NetworkData_DefaultResolvesToUnmodifiedInstance()
        {
            LootDefinitionCatalog catalog = CreateCatalog(GemId);

            Assert.That(
                default(WeaponInstanceModifiersNetworkData).TryResolve(
                    catalog,
                    CharacterAttribute.Strength,
                    out WeaponInstanceModifiers resolved,
                    out string error),
                Is.True,
                error);
            Assert.That(resolved.HasPrimary, Is.False);
            Assert.That(resolved.HasSecondary, Is.False);
            Assert.That(resolved.GemSocketCount, Is.Zero);
        }

        [Test]
        public void NetworkData_WithGemMissingFromCatalog_IsRejected()
        {
            LootDefinitionCatalog catalog = CreateCatalog("RES_BONE");
            WeaponInstanceModifiers source = CreateModifiers(
                CharacterAttribute.Strength,
                WeaponScalingGrade.B,
                CharacterAttribute.Strength,
                WeaponScalingGrade.None,
                1,
                CreateOccupiedSocket(),
                default);

            Assert.That(
                WeaponInstanceModifiersNetworkData.TryCreate(
                    source,
                    CharacterAttribute.Strength,
                    catalog,
                    out _,
                    out string error),
                Is.False);
            Assert.That(error, Does.Contain(GemId));
        }

        [Test]
        public void NetworkData_WithPrimaryOutsideNaturalAttribute_IsRejectedOnResolve()
        {
            LootDefinitionCatalog catalog = CreateCatalog(GemId);
            WeaponInstanceModifiers source = CreateModifiers(
                CharacterAttribute.Strength,
                WeaponScalingGrade.B,
                CharacterAttribute.Strength,
                WeaponScalingGrade.None,
                0,
                default,
                default);
            Assert.That(
                WeaponInstanceModifiersNetworkData.TryCreate(
                    source,
                    CharacterAttribute.Strength,
                    catalog,
                    out WeaponInstanceModifiersNetworkData data,
                    out string createError),
                Is.True,
                createError);

            Assert.That(
                data.TryResolve(catalog, CharacterAttribute.Intelligence, out _, out string error),
                Is.False);
            Assert.That(error, Does.Contain("must match natural attribute"));
        }

        private static WeaponInstanceModifiers CreateModifiers(
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

        private static WeaponGemSocket CreateOccupiedSocket()
        {
            Assert.That(
                WeaponGemSocket.TryCreateOccupied(new LootId(GemId), out WeaponGemSocket socket, out string error),
                Is.True,
                error);
            return socket;
        }

        private LootDefinitionCatalog CreateCatalog(params string[] ids)
        {
            var definitions = new List<LootDefinition>();
            foreach (string id in ids)
            {
                LootDefinition definition = ScriptableObject.CreateInstance<LootDefinition>();
                _createdObjects.Add(definition);
                SetPrivateField(definition, "_id", id);
                definitions.Add(definition);
            }

            LootDefinitionCatalog catalog = ScriptableObject.CreateInstance<LootDefinitionCatalog>();
            _createdObjects.Add(catalog);
            SetPrivateField(catalog, "_definitions", definitions);
            return catalog;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
#endif
