using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class ChestOpeningPresentationStateTests
    {
        [TestCase(false, 0)]
        [TestCase(true, 4)]
        public void Bootstrap_ShowsSnapshotWithoutPlaying(bool opened, int expectedFrame)
        {
            var state = new ChestOpeningPresentationState();
            Assert.That(state.Observe(opened), Is.False);
            Assert.That(state.IsOpening, Is.False);
            Assert.That(state.Advance(0f, 5, 12f), Is.EqualTo(expectedFrame));
        }

        [Test]
        public void ConfirmedResultBeforeReplicatedFlag_StartsOnlyOnce()
        {
            var state = new ChestOpeningPresentationState();
            state.Initialize(false);
            Assert.That(state.Observe(true), Is.True);
            state.Advance(0.2f, 5, 12f);
            Assert.That(state.Observe(false), Is.False);
            Assert.That(state.Observe(true), Is.False);
            Assert.That(state.Advance(0f, 5, 12f), Is.EqualTo(2));
        }

        [Test]
        public void ReplicatedFlagBeforeConfirmation_DoesNotRestartPlayback()
        {
            var state = new ChestOpeningPresentationState();
            state.Initialize(false);
            Assert.That(state.Observe(true), Is.True);
            state.Advance(0.3f, 5, 12f);
            Assert.That(state.Observe(true), Is.False);
            Assert.That(state.Advance(0f, 5, 12f), Is.EqualTo(3));
        }

        [Test]
        public void FiveFramesAtTwelveFps_CompleteWithoutWaitingForAudio()
        {
            var state = new ChestOpeningPresentationState();
            state.Initialize(false);
            state.Observe(true);
            Assert.That(state.Advance(0.4f, 5, 12f), Is.EqualTo(4));
            Assert.That(state.IsOpening, Is.True);
            Assert.That(state.Advance(0.02f, 5, 12f), Is.EqualTo(4));
            Assert.That(state.IsOpening, Is.False);
            Assert.That(state.Observe(true), Is.False);
        }

        [Test]
        public void DisableCompletesPresentation_AndReenableDoesNotReplay()
        {
            var state = new ChestOpeningPresentationState();
            state.Initialize(false);
            state.Observe(true);
            state.Complete();
            Assert.That(state.IsOpening, Is.False);
            Assert.That(state.Advance(0f, 5, 12f), Is.EqualTo(4));
            Assert.That(state.Observe(true), Is.False);
        }
    }
}
