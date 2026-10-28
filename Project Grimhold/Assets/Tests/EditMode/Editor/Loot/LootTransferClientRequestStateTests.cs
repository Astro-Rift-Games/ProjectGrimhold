using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootTransferClientRequestStateTests
    {
        [Test]
        public void InFlightRequest_RejectsSecondCandidateWithoutAdvancingSequence()
        {
            var state = new LootTransferClientRequestState();
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, LootTransferQuantityMode.SingleUnit, out LootTransferRequestIdentity first), Is.True);
            state.MarkSent(first);

            Assert.That(state.TryCreateCandidate(new EntityId(11), new EntityId(20), 3, LootTransferQuantityMode.FullStack, out _), Is.False);
            Assert.That(state.TryRelease(first.RequestSequence, out _), Is.True);
            Assert.That(state.TryCreateCandidate(new EntityId(11), new EntityId(20), 3, LootTransferQuantityMode.FullStack, out LootTransferRequestIdentity second), Is.True);
            Assert.That(second.RequestSequence, Is.EqualTo(first.RequestSequence + 1));
        }

        [Test]
        public void UnacceptedCandidate_DoesNotAdvanceSequence()
        {
            var state = new LootTransferClientRequestState();
            state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, LootTransferQuantityMode.SingleUnit, out LootTransferRequestIdentity rejected);

            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, LootTransferQuantityMode.SingleUnit, out LootTransferRequestIdentity retry), Is.True);
            Assert.That(retry.RequestSequence, Is.EqualTo(rejected.RequestSequence));
        }

        [Test]
        public void UnknownConfirmation_DoesNotReleaseLegitimateRequest()
        {
            var state = new LootTransferClientRequestState();
            state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, LootTransferQuantityMode.FullStack, out LootTransferRequestIdentity identity);
            state.MarkSent(identity);

            Assert.That(state.TryRelease(identity.RequestSequence + 1, out _), Is.False);
            Assert.That(state.HasInFlight, Is.True);
            Assert.That(state.TryRelease(identity.RequestSequence, out LootTransferRequestIdentity expected), Is.True);
            Assert.That(expected, Is.EqualTo(identity));
            Assert.That(state.HasInFlight, Is.False);
        }

        [Test]
        public void Reset_ClearsInFlightAndRestartsSessionSequence()
        {
            var state = new LootTransferClientRequestState();
            state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, LootTransferQuantityMode.FullStack, out LootTransferRequestIdentity identity);
            state.MarkSent(identity);

            state.Reset();

            Assert.That(state.HasInFlight, Is.False);
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, LootTransferQuantityMode.SingleUnit, out LootTransferRequestIdentity newSession), Is.True);
            Assert.That(newSession.RequestSequence, Is.EqualTo(1));
        }

        [Test]
        public void AddressedCandidate_RetainsSlotAndRevisionThroughInFlightRelease()
        {
            var state = new LootTransferClientRequestState();
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2,
                LootTransferQuantityMode.FullStack, 7, 19, out LootTransferRequestIdentity first), Is.True);
            Assert.That(first.SourceInventorySlotIndex, Is.EqualTo(7));
            Assert.That(first.ExpectedInventoryRevision, Is.EqualTo(19));
            state.MarkSent(first);
            Assert.That(state.TryGetExpected(out LootTransferRequestIdentity pending), Is.True);
            Assert.That(pending, Is.EqualTo(first));
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2,
                LootTransferQuantityMode.SingleUnit, 8, 20, out _), Is.False);
            Assert.That(state.TryRelease(first.RequestSequence, out LootTransferRequestIdentity released), Is.True);
            Assert.That(released.SourceInventorySlotIndex, Is.EqualTo(7));
            Assert.That(released.ExpectedInventoryRevision, Is.EqualTo(19));
            Assert.That(state.HasInFlight, Is.False, "Releasing a rejected/stale intention must not automatically retarget it.");
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2,
                LootTransferQuantityMode.SingleUnit, 8, 20, out LootTransferRequestIdentity next), Is.True);
            Assert.That(next.RequestSequence, Is.EqualTo(first.RequestSequence + 1));
        }

        [TestCase(-1)]
        [TestCase(30)]
        public void AddressedCandidate_RejectsInvalidAddressWithoutAdvancingSequence(int slotIndex)
        {
            var state = new LootTransferClientRequestState();
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2,
                LootTransferQuantityMode.FullStack, slotIndex, 19, out _), Is.False);
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2,
                LootTransferQuantityMode.FullStack, 29, 19, out LootTransferRequestIdentity valid), Is.True);
            Assert.That(valid.RequestSequence, Is.EqualTo(1));
        }

        [Test]
        public void LegacyCandidate_RemainsExplicitlyUnaddressed()
        {
            var state = new LootTransferClientRequestState();
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2,
                LootTransferQuantityMode.FullStack, out LootTransferRequestIdentity legacy), Is.True);
            Assert.That(legacy.SourceInventorySlotIndex, Is.EqualTo(-1));
            Assert.That(legacy.ExpectedInventoryRevision, Is.EqualTo(-1));
        }

        [Test]
        public void Candidate_RejectsMissingOrIdenticalEndpoints()
        {
            var state = new LootTransferClientRequestState();

            Assert.That(state.TryCreateCandidate(default, new EntityId(20), 2, LootTransferQuantityMode.SingleUnit, out _), Is.False);
            Assert.That(state.TryCreateCandidate(new EntityId(10), default, 2, LootTransferQuantityMode.SingleUnit, out _), Is.False);
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(10), 2, LootTransferQuantityMode.SingleUnit, out _), Is.False);
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, default, out _), Is.False);
        }
    }
}
