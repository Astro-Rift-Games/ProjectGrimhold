using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Loot
{
    public sealed class PlayerLootReceiverTests
    {
        [Test]
        public void LootEntry_ConservesImmutableReadData()
        {
            var lootId = new LootId("coin");
            var entry = new LootEntry(lootId, 12);

            Assert.That(entry.LootId, Is.EqualTo(lootId));
            Assert.That(entry.Amount, Is.EqualTo(12));
            Assert.That(entry.IsValid, Is.True);
        }

        [Test]
        public void RaidDistinctLootCapacity_IsExplicitlySixteen()
        {
            Assert.That(PlayerLootReceiver.MaxDistinctLootTypes, Is.EqualTo(16));
            Assert.That(PlayerLootReceiver.MaxCatalogEntries, Is.EqualTo(64));
        }

        [Test]
        public void DefaultSlotCapacity_IsExplicitGameplayConfiguration()
        {
            var holder = new GameObject(nameof(DefaultSlotCapacity_IsExplicitGameplayConfiguration));
            var receiver = holder.AddComponent<PlayerLootReceiver>();

            Assert.That(receiver.SlotCapacity, Is.EqualTo(16));

            Object.DestroyImmediate(holder);
        }

        [Test]
        public void LootGrantPresentationEvent_ConservesAuthoritativeDeliveryData()
        {
            var sourceId = new EntityId(10);
            var receiverId = new EntityId(20);
            var lootId = new LootId("coin");

            var presentationEvent = new LootGrantPresentationEvent(
                4,
                sourceId,
                receiverId,
                lootId,
                3,
                120);

            Assert.That(presentationEvent.Sequence, Is.EqualTo(4));
            Assert.That(presentationEvent.SourceId, Is.EqualTo(sourceId));
            Assert.That(presentationEvent.ReceiverId, Is.EqualTo(receiverId));
            Assert.That(presentationEvent.LootId, Is.EqualTo(lootId));
            Assert.That(presentationEvent.Amount, Is.EqualTo(3));
            Assert.That(presentationEvent.SimulationTick, Is.EqualTo(120));
        }

        [Test]
        public void InteractionPresentationEvent_ConservesSequenceAndTypedFailure()
        {
            var presentationEvent = new InteractionPresentationEvent(
                7,
                new EntityId(1),
                default,
                121,
                false,
                false,
                InteractionFailureReason.LootRejected,
                LootTransferFailureReason.InventoryFull);

            Assert.That(presentationEvent.Sequence, Is.EqualTo(7));
            Assert.That(presentationEvent.Success, Is.False);
            Assert.That(presentationEvent.FailureReason, Is.EqualTo(InteractionFailureReason.LootRejected));
            Assert.That(
                presentationEvent.LootFailureReason,
                Is.EqualTo(LootTransferFailureReason.InventoryFull));
        }

        [Test]
        public void CombatPresentationEvent_ConservesConfirmedAppliedDamageAndOrdering()
        {
            var presentationEvent = new CombatPresentationEvent(
                9,
                CombatFeedbackKind.ConfirmedImpact,
                new EntityId(14),
                new UnityEngine.Vector2(2f, 3f),
                7.5f,
                122,
                AttackFailureReason.None);

            Assert.That(presentationEvent.Sequence, Is.EqualTo(9));
            Assert.That(presentationEvent.Kind, Is.EqualTo(CombatFeedbackKind.ConfirmedImpact));
            Assert.That(presentationEvent.AppliedDamage, Is.EqualTo(7.5f));
            Assert.That(presentationEvent.SimulationTick, Is.EqualTo(122));
            Assert.That(presentationEvent.AttackFailureReason, Is.EqualTo(AttackFailureReason.None));
        }
    }
}
