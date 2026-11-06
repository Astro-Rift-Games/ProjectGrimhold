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

        [Test]
        public void NetworkData_UsesExactlyOneFusionWord()
        {
            Assert.That(Fusion.NetworkStructUtils.GetWordCount<WeaponInstanceModifiersNetworkData>(), Is.EqualTo(1));
        }

        [Test]
        public void NetworkData_RoundTripsEveryAttributeGradeAndSocketBoundary()
        {
            LootDefinitionCatalog catalog = CreateCatalog(GemId);
            foreach (CharacterAttribute natural in new[] { CharacterAttribute.Strength, CharacterAttribute.Dexterity, CharacterAttribute.Intelligence })
            foreach (CharacterAttribute secondary in System.Enum.GetValues(typeof(CharacterAttribute)))
            foreach (WeaponScalingGrade grade in new[] { WeaponScalingGrade.E, WeaponScalingGrade.D, WeaponScalingGrade.C, WeaponScalingGrade.B, WeaponScalingGrade.A, WeaponScalingGrade.S })
            foreach (int count in new[] { 0, 1, 2 })
            {
                if (natural == secondary) continue;
                WeaponInstanceModifiers source = CreateModifiers(natural, grade, secondary, grade, count,
                    count > 0 ? CreateOccupiedSocket() : default, count > 1 ? CreateOccupiedSocket() : default);
                Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(source, natural, catalog, out var data, out string error), Is.True, error);
                Assert.That(data.TryResolve(catalog, natural, out var resolved, out error), Is.True, error);
                Assert.That(resolved.Primary.Attribute, Is.EqualTo(natural));
                Assert.That(resolved.Primary.Grade, Is.EqualTo(grade));
                Assert.That(resolved.Secondary.Attribute, Is.EqualTo(secondary));
                Assert.That(resolved.Secondary.Grade, Is.EqualTo(grade));
                Assert.That(resolved.GemSocketCount, Is.EqualTo(count));
                for (int i = 0; i < count; i++)
                {
                    Assert.That(resolved.TryGetGemSocket(i, out var gem), Is.True);
                    Assert.That(gem.GemLootId, Is.EqualTo(new LootId(GemId)));
                }
            }
        }

        [TestCase(127, true)]
        [TestCase(128, false)]
        public void NetworkData_CatalogBoundPreventsGemIndexTruncation(int count, bool accepted)
        {
            var ids = new string[count];
            for (int i = 0; i < count - 1; i++) ids[i] = $"AAA_{i:D3}";
            ids[count - 1] = GemId;
            LootDefinitionCatalog catalog = CreateCatalog(ids);
            var source = CreateModifiers(CharacterAttribute.Strength, WeaponScalingGrade.E,
                CharacterAttribute.Luck, WeaponScalingGrade.None, 2, CreateOccupiedSocket(), CreateOccupiedSocket());
            Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(source, CharacterAttribute.Strength,
                catalog, out var data, out _), Is.EqualTo(accepted));
            if (!accepted)
            {
                Assert.That(data, Is.EqualTo(default(WeaponInstanceModifiersNetworkData)));
                return;
            }
            Assert.That(data.TryResolve(catalog, CharacterAttribute.Strength, out var resolved, out _), Is.True);
            Assert.That(resolved.TryGetGemSocket(1, out var gem), Is.True);
            Assert.That(gem.GemLootId, Is.EqualTo(new LootId(GemId)));
        }

        [TestCase(7, 1)]
        [TestCase(2, 7)]
        [TestCase(-1, 1)]
        public void NetworkData_RejectsInvalidPresentAttributeOrGradeWithoutOutput(int attribute, int grade)
        {
            object scaling = default(WeaponScalingModifier);
            SetPrivateField(scaling, "_attribute", (CharacterAttribute)attribute);
            SetPrivateField(scaling, "_grade", (WeaponScalingGrade)grade);
            object source = default(WeaponInstanceModifiers);
            SetPrivateField(source, "_primary", (WeaponScalingModifier)scaling);
            Assert.That(WeaponInstanceModifiersNetworkData.TryCreate((WeaponInstanceModifiers)source,
                CharacterAttribute.Strength, CreateCatalog(GemId), out var data, out _), Is.False);
            Assert.That(data, Is.EqualTo(default(WeaponInstanceModifiersNetworkData)));
        }

        [TestCase(7 | (1 << 3))] // unsupported attribute
        [TestCase(2 | (7 << 3))] // unsupported grade
        [TestCase(2)] // absent grade with noncanonical attribute
        [TestCase(1 << 28)] // reserved bits
        [TestCase(3 << 12)] // socket count three
        [TestCase(127 << 14)] // catalog reference outside this catalog
        [TestCase((1 << 14))] // gem beyond last socket
        public void NetworkData_RejectsMalformedPackedWordWithoutOutput(int word)
        {
            var data = WeaponInstanceModifiersNetworkData.FromPacked(word);
            Assert.That(data.TryResolve(CreateCatalog(GemId), CharacterAttribute.Strength,
                out var modifiers, out string error), Is.False);
            Assert.That(modifiers.GemSocketCount, Is.Zero);
            Assert.That(modifiers.HasPrimary, Is.False);
            Assert.That(modifiers.HasSecondary, Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            Assert.That(data.PackedValue, Is.EqualTo(word));
        }

        [Test]
        public void NetworkData_RejectsCatalogEntryThatIsNotAGem()
        {
            LootDefinitionCatalog catalog = CreateCatalog("RES_BONE");
            WeaponGemSocket.TryCreateOccupied(new LootId("RES_BONE"), out var socket, out _);
            Assert.That(WeaponInstanceModifiers.TryCreate(default, default, CharacterAttribute.Strength,
                1, socket, default, out var source, out _), Is.True);
            Assert.That(WeaponInstanceModifiersNetworkData.TryCreate(source, CharacterAttribute.Strength,
                catalog, out var encoded, out _), Is.False);
            Assert.That(encoded, Is.EqualTo(default(WeaponInstanceModifiersNetworkData)));
            Assert.That(WeaponInstanceModifiersNetworkData.FromPacked((1 << 12) | (1 << 14))
                .TryResolve(catalog, CharacterAttribute.Strength, out _, out _), Is.False);
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
                if (id == GemId)
                {
                    GemDefinition gem = ScriptableObject.CreateInstance<GemDefinition>();
                    _createdObjects.Add(gem);
                    SetPrivateField(definition, "_gemDefinition", gem);
                }
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
