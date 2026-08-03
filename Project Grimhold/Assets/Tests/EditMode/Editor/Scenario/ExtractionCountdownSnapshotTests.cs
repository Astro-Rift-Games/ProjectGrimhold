using NUnit.Framework;

namespace Tests.EditMode.Editor.Scenario
{
    [TestFixture]
    public sealed class ExtractionCountdownSnapshotTests
    {
        [Test]
        public void None_ReturnsDefaultValues()
        {
            ExtractionCountdownSnapshot snapshot = ExtractionCountdownSnapshot.None();

            Assert.AreEqual(ExtractionState.None, snapshot.State);
            Assert.AreEqual(0f, snapshot.ElapsedSeconds);
            Assert.AreEqual(0f, snapshot.TotalSeconds);
            Assert.AreEqual(0f, snapshot.Progress);
        }

        [Test]
        public void Extracted_ReturnsExtractedValues()
        {
            EntityId zoneId = new EntityId(27);
            ExtractionCountdownSnapshot snapshot = ExtractionCountdownSnapshot.Extracted(zoneId);

            Assert.AreEqual(ExtractionState.Extracted, snapshot.State);
            Assert.AreEqual(zoneId, snapshot.ActiveZoneId);
            Assert.AreEqual(0f, snapshot.ElapsedSeconds);
            Assert.AreEqual(0f, snapshot.TotalSeconds);
            Assert.AreEqual(1f, snapshot.Progress);
        }

        [Test]
        public void InProgress_ClampsProgressZeroToOne()
        {
            ExtractionCountdownSnapshot snapshot = new ExtractionCountdownSnapshot(
                ExtractionState.InProgress,
                new EntityId(27),
                remainingSeconds: 2.5f,
                totalSeconds: 5f,
                progress: 0.5f);

            Assert.AreEqual(ExtractionState.InProgress, snapshot.State);
            Assert.AreEqual(2.5f, snapshot.ElapsedSeconds);
            Assert.AreEqual(5f, snapshot.TotalSeconds);
            Assert.AreEqual(0.5f, snapshot.Progress);

            ExtractionCountdownSnapshot overclamped = new ExtractionCountdownSnapshot(
                ExtractionState.InProgress,
                new EntityId(27),
                remainingSeconds: 0f,
                totalSeconds: 5f,
                progress: 1.5f);
            Assert.AreEqual(1f, overclamped.Progress);
        }
    }
}
