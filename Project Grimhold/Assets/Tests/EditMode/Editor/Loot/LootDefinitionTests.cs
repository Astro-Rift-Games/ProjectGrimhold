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

        [TestCase("ancient coin")]
        [TestCase("ancient-coin")]
        [TestCase("ancient_coin!")]
        [TestCase("con@potion")]
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
        [TestCase("CON_HEALTH_POTION")]
        [TestCase("RES_SLIME_GEL")]
        [TestCase("Ancient_coin")]
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

        [Test]
        public void NegativeBuyValue_FailsValidation()
        {
            SetValidDefaults();
            SetField("_buyValuePerUnit", -1);

            Assert.That(_loot.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("negative buy value"));
        }

        [Test]
        public void BuyValue_IsIndependentFromExtractionAndSellValues()
        {
            SetValidDefaults();
            SetField("_extractionValuePerUnit", 7);
            SetField("_sellValuePerUnit", 50);
            SetField("_buyValuePerUnit", 120);

            Assert.That(_loot.TryValidate(out string error), Is.True, error);
            Assert.That(_loot.BuyValuePerUnit, Is.EqualTo(120));
            Assert.That(_loot.ExtractionValuePerUnit, Is.EqualTo(7));
            Assert.That(_loot.SellValuePerUnit, Is.EqualTo(50));
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
            Assert.That(typeof(LootDefinition).GetProperty("BuyValuePerUnit").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("DefaultPickupQuantity").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("ArmorDefinition").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("ObjectValue").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("StackMax").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("MaxTransportable").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("Family").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("Tags").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("Tradable").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("CanBeTransferred").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("CanEnterDungeon").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("CanBeFoundInDungeon").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("CanBeDiscarded").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("CanBeUsed").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("MissionCompatible").CanWrite, Is.False);
            Assert.That(typeof(LootDefinition).GetProperty("UseMode").CanWrite, Is.False);
        }

        [Test]
        public void NegativeObjectValue_FailsValidation()
        {
            SetValidDefaults();
            SetField("_objectValue", -1);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("negative ObjectValue"));
        }

        [Test]
        public void ZeroObjectValue_PassesValidation()
        {
            SetValidDefaults();
            SetField("_objectValue", 0);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.True, error);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidStackMax_FailsValidation(int invalidStackMax)
        {
            SetValidDefaults();
            SetField("_stackMax", invalidStackMax);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("StackMax less than 1"));
        }

        [Test]
        public void NegativeMaxTransportable_FailsValidation()
        {
            SetValidDefaults();
            SetField("_maxTransportable", -1);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("negative MaxTransportable"));
        }

        [Test]
        public void DeprecatedQuestCategory_FailsValidation()
        {
            SetValidDefaults();
            SetField("_category", (LootCategory)11);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("deprecated category Quest"));
        }

        [Test]
        public void Treasure_WithStackMaxGreaterThanOne_FailsValidation()
        {
            SetValidDefaults();
            SetField("_category", LootCategory.Treasure);
            SetField("_stackMax", 2);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.False);
            Assert.That(error, Does.Contain("must not be stackable"));
        }

        [Test]
        public void Treasure_WithStackMaxOne_PassesValidation()
        {
            SetValidDefaults();
            SetField("_category", LootCategory.Treasure);
            SetField("_stackMax", 1);
            bool result = _loot.TryValidate(out string error);
            Assert.That(result, Is.True, error);
        }

        [Test]
        public void CanonicalProperties_ReturnConfiguredValues()
        {
            SetValidDefaults();
            SetField("_objectValue", 150);
            SetField("_stackMax", 20);
            SetField("_maxTransportable", 40);
            SetField("_family", "Alchemy");
            SetField("_tags", new string[] { "potion", "healing" });
            SetField("_tradable", true);
            SetField("_canBeTransferred", true);
            SetField("_canEnterDungeon", true);
            SetField("_canBeFoundInDungeon", true);
            SetField("_canBeDiscarded", true);
            SetField("_canBeUsed", true);
            SetField("_missionCompatible", false);
            SetField("_useMode", ItemUseMode.Instant);

            Assert.That(_loot.ObjectValue, Is.EqualTo(150));
            Assert.That(_loot.StackMax, Is.EqualTo(20));
            Assert.That(_loot.MaxTransportable, Is.EqualTo(40));
            Assert.That(_loot.Family, Is.EqualTo("Alchemy"));
            Assert.That(_loot.Tags, Is.EquivalentTo(new[] { "potion", "healing" }));
            Assert.That(_loot.Tradable, Is.True);
            Assert.That(_loot.CanBeTransferred, Is.True);
            Assert.That(_loot.CanEnterDungeon, Is.True);
            Assert.That(_loot.CanBeFoundInDungeon, Is.True);
            Assert.That(_loot.CanBeDiscarded, Is.True);
            Assert.That(_loot.CanBeUsed, Is.True);
            Assert.That(_loot.MissionCompatible, Is.False);
            Assert.That(_loot.UseMode, Is.EqualTo(ItemUseMode.Instant));
        }

        [Test]
        public void ArmorCategory_WithoutArmorDefinition_FailsValidation()
        {
            SetValidDefaults();
            SetField("_category", LootCategory.Helmet);

            Assert.That(_loot.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("no ArmorDefinition"));
        }

        [Test]
        public void NonEquipmentCategory_WithArmorDefinition_FailsValidation()
        {
            ArmorDefinition armor = CreateValidArmorDefinition();
            try
            {
                SetValidDefaults();
                SetField("_armorDefinition", armor);

                Assert.That(_loot.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("has an ArmorDefinition"));
            }
            finally
            {
                Object.DestroyImmediate(armor);
            }
        }

        [Test]
        public void ArmorCategory_WithValidArmorDefinition_PassesValidation()
        {
            ArmorDefinition armor = CreateValidArmorDefinition();
            try
            {
                SetValidDefaults();
                SetField("_category", LootCategory.Helmet);
                SetField("_armorDefinition", armor);

                Assert.That(_loot.TryValidate(out string error), Is.True, error);
            }
            finally
            {
                Object.DestroyImmediate(armor);
            }
        }

        private static ArmorDefinition CreateValidArmorDefinition()
        {
            ArmorDefinition armor = ScriptableObject.CreateInstance<ArmorDefinition>();
            SetPrivateField(armor, "_physicalDefense", 1);
            SetPrivateField(
                armor,
                "_maximumResourceModifier",
                new MaximumResourceModifier(MaximumResourceType.Health, 1));
            return armor;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Field {target.GetType().Name}.{fieldName} was not found.");
            field.SetValue(target, value);
        }
    }
}
