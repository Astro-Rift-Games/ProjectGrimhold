using NUnit.Framework;

namespace Tests.EditMode.Editor.Scenario
{
    [TestFixture]
    public sealed class ExtractionProgressSnapshotTests
    {
        [Test]
        public void None_ReturnsDefaultValues()
        {
            ExtractionProgressSnapshot snapshot = ExtractionProgressSnapshot.None();

            Assert.AreEqual(ExtractionState.None, snapshot.State);
            Assert.AreEqual(0f, snapshot.ElapsedSeconds);
            Assert.AreEqual(0f, snapshot.TotalSeconds);
            Assert.AreEqual(0f, snapshot.Progress);
        }

        [Test]
        public void Extracted_ReturnsExtractedValues()
        {
            EntityId zoneId = new EntityId(27);
            ExtractionProgressSnapshot snapshot = ExtractionProgressSnapshot.Extracted(zoneId);

            Assert.AreEqual(ExtractionState.Extracted, snapshot.State);
            Assert.AreEqual(zoneId, snapshot.ActiveZoneId);
            Assert.AreEqual(0f, snapshot.ElapsedSeconds);
            Assert.AreEqual(0f, snapshot.TotalSeconds);
            Assert.AreEqual(1f, snapshot.Progress);
        }

        [Test]
        public void InProgress_ClampsProgressZeroToOne()
        {
            ExtractionProgressSnapshot snapshot = new ExtractionProgressSnapshot(
                ExtractionState.InProgress,
                new EntityId(27),
                remainingSeconds: 2.5f,
                totalSeconds: 5f,
                progress: 0.5f);

            Assert.AreEqual(ExtractionState.InProgress, snapshot.State);
            Assert.AreEqual(2.5f, snapshot.ElapsedSeconds);
            Assert.AreEqual(5f, snapshot.TotalSeconds);
            Assert.AreEqual(0.5f, snapshot.Progress);

            ExtractionProgressSnapshot overclamped = new ExtractionProgressSnapshot(
                ExtractionState.InProgress,
                new EntityId(27),
                remainingSeconds: 0f,
                totalSeconds: 5f,
                progress: 1.5f);
            Assert.AreEqual(1f, overclamped.Progress);
        }
    }
}
