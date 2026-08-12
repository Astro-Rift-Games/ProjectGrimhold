using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidSpectatorSelectionPolicyTests
    {
        private static readonly string[] OrderedProfiles = { "B", "C", "D", "E" };

        [Test]
        public void InvalidatedTarget_SelectsFirstOrdinalSuccessor()
        {
            string[] afterCLeaves = { "B", "D", "E" };
            string[] afterELeaves = { "B", "C", "D" };

            Assert.That(
                RaidSpectatorSelectionPolicy.FindNextAfterInvalidated(afterCLeaves, "C"),
                Is.EqualTo(1));
            Assert.That(
                RaidSpectatorSelectionPolicy.FindNextAfterInvalidated(afterELeaves, "E"),
                Is.Zero);
        }

        [Test]
        public void PreviousAndNext_AreCircular()
        {
            Assert.That(
                RaidSpectatorSelectionPolicy.FindRelative(OrderedProfiles, "E", 1),
                Is.Zero);
            Assert.That(
                RaidSpectatorSelectionPolicy.FindRelative(OrderedProfiles, "B", -1),
                Is.EqualTo(3));
            Assert.That(
                RaidSpectatorSelectionPolicy.FindRelative(OrderedProfiles, "C", 1),
                Is.EqualTo(2));
        }

        [Test]
        public void MissingCurrentTarget_UsesDirectionBoundary()
        {
            Assert.That(
                RaidSpectatorSelectionPolicy.FindRelative(OrderedProfiles, "missing", 1),
                Is.Zero);
            Assert.That(
                RaidSpectatorSelectionPolicy.FindRelative(OrderedProfiles, "missing", -1),
                Is.EqualTo(3));
        }
    }
}
