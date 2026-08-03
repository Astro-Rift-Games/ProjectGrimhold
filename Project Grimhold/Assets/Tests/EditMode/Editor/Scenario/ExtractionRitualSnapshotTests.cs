using NUnit.Framework;

namespace Tests.EditMode.Editor.Scenario
{
    [TestFixture]
    public sealed class ExtractionRitualSnapshotTests
    {
        [TestCase(ExtractionRitualState.NotStarted, 10f, 0f)]
        [TestCase(ExtractionRitualState.Cancelled, 10f, 0f)]
        [TestCase(ExtractionRitualState.Completed, 0f, 1f)]
        public void TerminalAndInitialStates_PreserveDefinedSemantics(
            ExtractionRitualState state,
            float remainingSeconds,
            float progress)
        {
            var snapshot = new ExtractionRitualSnapshot(state, 10f, remainingSeconds, progress);

            Assert.That(snapshot.State, Is.EqualTo(state));
            Assert.That(snapshot.TotalSeconds, Is.EqualTo(10f));
            Assert.That(snapshot.RemainingSeconds, Is.EqualTo(remainingSeconds));
            Assert.That(snapshot.Progress, Is.EqualTo(progress));
        }

        [TestCase(-1f, 0f)]
        [TestCase(0.4f, 0.4f)]
        [TestCase(2f, 1f)]
        public void Progress_IsClampedZeroToOne(float supplied, float expected)
        {
            var snapshot = new ExtractionRitualSnapshot(
                ExtractionRitualState.InProgress,
                10f,
                5f,
                supplied);

            Assert.That(snapshot.Progress, Is.EqualTo(expected));
        }
    }
}
