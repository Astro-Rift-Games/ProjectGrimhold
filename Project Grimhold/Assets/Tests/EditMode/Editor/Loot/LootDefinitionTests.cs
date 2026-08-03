using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public class LootDefinitionTests
    {
        private LootDefinition _loot;
        private Sprite _testSprite;
        private Texture2D _testTexture;

        [SetUp]
        public void SetUp()
        {
            _loot = ScriptableObject.CreateInstance<LootDefinition>();
            _testTexture = new Texture2D(8, 8);
            _testSprite = Sprite.Create(_testTexture, new Rect(0, 0, 8, 8), Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            if (_loot != null)
            {
                Object.DestroyImmediate(_loot);
            }

            if (_testSprite != null)
            {
                Object.DestroyImmediate(_testSprite);
            }

            if (_testTexture != null)
            {
                Object.DestroyImmediate(_testTexture);
            }
        }

        private void SetField(string fieldName, object value)
        {
            FieldInfo field = typeof(LootDefinition).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {fieldName} not found on LootDefinition.");
            field.SetValue(_loot, value);
        }

        private void SetValidDefaults()
        {
            SetField("_id", "ancient_coin");
            SetField("_displayName", "Ancient Coin");
            SetField("_icon", _testSprite);
            SetField("_worldSprite", _testSprite);
            SetField("_category", LootCategory.Valuable);
            SetField("_rarity", LootRarity.Common);
            SetField("_extractionValuePerUnit", 10);
            SetField("_sellValuePerUnit", 25);
            SetField("_defaultPickupQuantity", 1);
        }

        [Test]
        public void ValidDefinition_PassesValidation()
        {
            SetValidDefaults();
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.True, $"Valid definition should pass but failed: {error}");
            Assert.That(error, Is.Null);
        }

        [TestCase("")]
        [TestCase(null)]
        public void EmptyOrNullId_FailsValidation(string invalidId)
        {
            SetValidDefaults();
            SetField("_id", invalidId);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("empty or null ID"));
        }

        [TestCase("Ancient_coin")]
        [TestCase("ancient coin")]
        [TestCase("ancient-coin")]
        [TestCase("ancient_coin!")]
        public void InvalidIdFormat_FailsValidation(string invalidId)
        {
            SetValidDefaults();
            SetField("_id", invalidId);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("invalid ID"));
        }

        [TestCase("ancient")]
        [TestCase("ancient_coin_123")]
        [TestCase("123_scrap")]
        public void ValidIdFormat_PassesValidation(string validId)
        {
            SetValidDefaults();
            SetField("_id", validId);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.True, $"ID '{validId}' should be valid but failed: {error}");
        }

        [TestCase("")]
        [TestCase(null)]
        public void EmptyOrNullDisplayName_FailsValidation(string invalidName)
        {
            SetValidDefaults();
            SetField("_displayName", invalidName);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("empty display name"));
        }

        [Test]
        public void CategoryNone_FailsValidation()
        {
            SetValidDefaults();
            SetField("_category", LootCategory.None);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("category other than None"));
        }

        [Test]
        public void NegativeExtractionValue_FailsValidation()
        {
            SetValidDefaults();
            // Bypass OnValidate by setting the field directly via reflection
            SetField("_extractionValuePerUnit", -5);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("negative extraction value"));
        }

        [Test]
        public void ZeroExtractionValue_PassesValidation()
        {
            SetValidDefaults();
            SetField("_extractionValuePerUnit", 0);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.True, $"Zero value should be valid but failed: {error}");
        }

        [Test]
        public void ExtractionAndSellValues_AreIndependent()
        {
            SetValidDefaults();
            SetField("_extractionValuePerUnit", 7);
            SetField("_sellValuePerUnit", 50);

            Assert.That(_loot.TryValidate(out string error), Is.True, error);
            Assert.That(_loot.ExtractionValuePerUnit, Is.EqualTo(7));
            Assert.That(_loot.SellValuePerUnit, Is.EqualTo(50));
        }

        [Test]
        public void NegativeSellValue_FailsValidation()
        {
            SetValidDefaults();
            SetField("_sellValuePerUnit", -1);

            Assert.That(_loot.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("negative sell value"));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidDefaultQuantity_FailsValidation(int invalidQty)
        {
            SetValidDefaults();
            // Bypass OnValidate by setting the field directly via reflection
            SetField("_defaultPickupQuantity", invalidQty);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("default pickup quantity less than 1"));
        }

        [Test]
        public void NullIcon_PassesValidationForPresentationFallback()
        {
            SetValidDefaults();
            SetField("_icon", null);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.True, error);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void NullWorldSprite_FailsValidation()
        {
            SetValidDefaults();
            SetField("_worldSprite", null);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("lacks a valid World Sprite reference"));
        }

        [Test]
        public void Getters_ReturnConfiguredValues()
        {
            SetValidDefaults();
            Assert.That(_loot.Id, Is.EqualTo("ancient_coin"));
            Assert.That(_loot.DisplayName, Is.EqualTo("Ancient Coin"));
            Assert.That(_loot.Icon, Is.EqualTo(_testSprite));
            Assert.That(_loot.WorldSprite, Is.EqualTo(_testSprite));
            Assert.That(_loot.Category, Is.EqualTo(LootCategory.Valuable));
            Assert.That(_loot.Rarity, Is.EqualTo(LootRarity.Common));
            Assert.That(_loot.ExtractionValuePerUnit, Is.EqualTo(10));
            Assert.That(_loot.SellValuePerUnit, Is.EqualTo(25));
            Assert.That(_loot.DefaultPickupQuantity, Is.EqualTo(1));
        }

        [Test]
        public void VerifyNoPublicSettersExist()
        {
            // Verify properties don't have public setters
            Assert.That(typeof(LootDefinition).GetProperty("Id").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("DisplayName").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("Icon").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("WorldSprite").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("Category").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("Rarity").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("ExtractionValuePerUnit").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("SellValuePerUnit").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("DefaultPickupQuantity").CanWrite, Is.False);
        }
    }
}
