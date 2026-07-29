using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootTransferClientRequestStateTests
    {
        [Test]
        public void InFlightRequest_RejectsSecondCandidateWithoutAdvancingSequence()
        {
            var state = new LootTransferClientRequestState();
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, out LootTransferRequestIdentity first), Is.True);
            state.MarkSent(first);

            Assert.That(state.TryCreateCandidate(new EntityId(11), new EntityId(20), 3, out _), Is.False);
            Assert.That(state.TryRelease(first.RequestSequence, out _), Is.True);
            Assert.That(state.TryCreateCandidate(new EntityId(11), new EntityId(20), 3, out LootTransferRequestIdentity second), Is.True);
            Assert.That(second.RequestSequence, Is.EqualTo(first.RequestSequence + 1));
        }

        [Test]
        public void UnacceptedCandidate_DoesNotAdvanceSequence()
        {
            var state = new LootTransferClientRequestState();
            state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, out LootTransferRequestIdentity rejected);

            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, out LootTransferRequestIdentity retry), Is.True);
            Assert.That(retry.RequestSequence, Is.EqualTo(rejected.RequestSequence));
        }

        [Test]
        public void UnknownConfirmation_DoesNotReleaseLegitimateRequest()
        {
            var state = new LootTransferClientRequestState();
            state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, out LootTransferRequestIdentity identity);
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
            state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, out LootTransferRequestIdentity identity);
            state.MarkSent(identity);

            state.Reset();

            Assert.That(state.HasInFlight, Is.False);
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(20), 2, out LootTransferRequestIdentity newSession), Is.True);
            Assert.That(newSession.RequestSequence, Is.EqualTo(1));
        }

        [Test]
        public void Candidate_RejectsMissingOrIdenticalEndpoints()
        {
            var state = new LootTransferClientRequestState();

            Assert.That(state.TryCreateCandidate(default, new EntityId(20), 2, out _), Is.False);
            Assert.That(state.TryCreateCandidate(new EntityId(10), default, 2, out _), Is.False);
            Assert.That(state.TryCreateCandidate(new EntityId(10), new EntityId(10), 2, out _), Is.False);
        }
    }
}
