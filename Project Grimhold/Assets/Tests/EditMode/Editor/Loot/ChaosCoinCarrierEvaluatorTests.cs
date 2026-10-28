using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class ChaosCoinCarrierEvaluatorTests
    {
        [Test]
        public void ChaosCoinLootId_IsFormallyRegisteredAsTreChaosCoin()
        {
            Assert.That(ChaosCoinCarrierEvaluator.ChaosCoinLootId.Value, Is.EqualTo("TRE_CHAOS_COIN"));
        }

        [Test]
        public void NullCharacter_ReturnsFalseGracefully()
        {
            Assert.That(ChaosCoinCarrierEvaluator.IsActiveCarrier(null), Is.False);
            Assert.That(ChaosCoinCarrierEvaluator.HasActiveChaosCoinCarrier(null), Is.False);
        }

        [Test]
        public void CharacterWithoutLootReceiver_ReturnsFalse()
        {
            var go = new GameObject("TestPlayer");
            try
            {
                // In EditMode, PlayerCharacter without network runner returns false
                var player = go.AddComponent<PlayerCharacter>();
                Assert.That(ChaosCoinCarrierEvaluator.IsActiveCarrier(player), Is.False);
                Assert.That(ChaosCoinCarrierEvaluator.HasActiveChaosCoinCarrier(player), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
