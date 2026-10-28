#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Utilities
{
    [TestFixture]
    public sealed class UtilityExpeditionRulesTests
    {
        [Test]
        public void CardinalDirectionHelper_CalculatesAllEightDirectionsCorrectly()
        {
            Vector2 origin = Vector2.zero;

            // Cardinal
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(0f, 10f)), Is.EqualTo(CardinalDirection.North));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(10f, 0f)), Is.EqualTo(CardinalDirection.East));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(0f, -10f)), Is.EqualTo(CardinalDirection.South));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(-10f, 0f)), Is.EqualTo(CardinalDirection.West));

            // Intercardinal
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(10f, 10f)), Is.EqualTo(CardinalDirection.NorthEast));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(10f, -10f)), Is.EqualTo(CardinalDirection.SouthEast));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(-10f, -10f)), Is.EqualTo(CardinalDirection.SouthWest));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(-10f, 10f)), Is.EqualTo(CardinalDirection.NorthWest));

            // Coincident positions
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, origin), Is.EqualTo(CardinalDirection.None));
        }

        [Test]
        public void CardinalDirectionHelper_LocalizedNamesAndAbbreviations_AreAccurate()
        {
            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.North), Is.EqualTo("Norte"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.North), Is.EqualTo("N"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.NorthEast), Is.EqualTo("Noreste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.NorthEast), Is.EqualTo("NE"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.East), Is.EqualTo("Este"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.East), Is.EqualTo("E"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.SouthEast), Is.EqualTo("Sureste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.SouthEast), Is.EqualTo("SE"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.South), Is.EqualTo("Sur"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.South), Is.EqualTo("S"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.SouthWest), Is.EqualTo("Suroeste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.SouthWest), Is.EqualTo("SW"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.West), Is.EqualTo("Oeste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.West), Is.EqualTo("W"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.NorthWest), Is.EqualTo("Noroeste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.NorthWest), Is.EqualTo("NW"));
        }

        [Test]
        public void ArcaneElementDetectionRules_IdentifiesArcaneDefinitionsCorrectly()
        {
            // Null check
            Assert.That(ArcaneElementDetectionRules.IsArcaneDefinition(null), Is.False);

            // Asset tests with real ScriptableObject
            var arcaneDef = ScriptableObject.CreateInstance<LootDefinition>();
            try
            {
                // Set family via reflection
                typeof(LootDefinition).GetField("_family", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(arcaneDef, "ArcaneComponents");
                Assert.That(ArcaneElementDetectionRules.IsArcaneDefinition(arcaneDef), Is.True);

                // Set tags via reflection
                typeof(LootDefinition).GetField("_family", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(arcaneDef, "Material");
                typeof(LootDefinition).GetField("_tags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(arcaneDef, new string[] { "crafting", "arcane" });
                Assert.That(ArcaneElementDetectionRules.IsArcaneDefinition(arcaneDef), Is.True);

                // Non-arcane
                typeof(LootDefinition).GetField("_tags", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(arcaneDef, new string[] { "crafting", "iron" });
                Assert.That(ArcaneElementDetectionRules.IsArcaneDefinition(arcaneDef), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(arcaneDef);
            }
        }

        [Test]
        public void LootTransferResult_MaxTransportableExceeded_IsRecognizedAsRejectionReason()
        {
            var result = LootTransferResult.Rejected(LootTransferFailureReason.MaxTransportableExceeded);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.MaxTransportableExceeded));
            Assert.That(result.IsValid, Is.True);
        }
    }
}
#endif
