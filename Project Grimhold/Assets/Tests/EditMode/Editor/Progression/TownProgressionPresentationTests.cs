#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class TownProgressionPresentationTests
    {
        private ExperienceCurve _curve;

        [SetUp]
        public void SetUp()
        {
            Assert.That(ExperienceCurve.TryCreate(
                new long[] { 100, 200 },
                out _curve), Is.True);
        }

        [Test]
        public void InitialLevel_ProducesEmptyProgress()
        {
            Assert.That(TownProgressionPresentation.TryCreate(
                _curve,
                1,
                0,
                out TownProgressionPresentation presentation), Is.True);

            Assert.That(presentation.Level, Is.EqualTo(1));
            Assert.That(presentation.CurrentExperience, Is.Zero);
            Assert.That(presentation.RequiredExperience, Is.EqualTo(100));
            Assert.That(presentation.NormalizedProgress, Is.Zero);
            Assert.That(presentation.IsMaximumLevel, Is.False);
        }

        [Test]
        public void PartialProgress_UsesCurrentLevelRequirement()
        {
            Assert.That(TownProgressionPresentation.TryCreate(
                _curve,
                1,
                25,
                out TownProgressionPresentation presentation), Is.True);

            Assert.That(presentation.RequiredExperience, Is.EqualTo(100));
            Assert.That(presentation.NormalizedProgress, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void RemainderAfterLevelUp_UsesResultingLevelRequirement()
        {
            Assert.That(TownProgressionPresentation.TryCreate(
                _curve,
                2,
                50,
                out TownProgressionPresentation presentation), Is.True);

            Assert.That(presentation.Level, Is.EqualTo(2));
            Assert.That(presentation.CurrentExperience, Is.EqualTo(50));
            Assert.That(presentation.RequiredExperience, Is.EqualTo(200));
            Assert.That(presentation.NormalizedProgress, Is.EqualTo(0.25f).Within(0.0001f));
        }

        [Test]
        public void MaximumLevel_DoesNotRequireAnotherTransition()
        {
            Assert.That(TownProgressionPresentation.TryCreate(
                _curve,
                _curve.MaximumLevel,
                0,
                out TownProgressionPresentation presentation), Is.True);

            Assert.That(presentation.Level, Is.EqualTo(3));
            Assert.That(presentation.RequiredExperience, Is.Zero);
            Assert.That(presentation.NormalizedProgress, Is.EqualTo(1f));
            Assert.That(presentation.IsMaximumLevel, Is.True);
        }

        [TestCase(0, 0)]
        [TestCase(4, 0)]
        [TestCase(1, -1)]
        [TestCase(1, 100)]
        [TestCase(3, 1)]
        public void InvalidPersistentState_IsRejected(int level, long experience)
        {
            Assert.That(TownProgressionPresentation.TryCreate(
                _curve,
                level,
                experience,
                out TownProgressionPresentation presentation), Is.False);
            Assert.That(presentation, Is.EqualTo(default(TownProgressionPresentation)));
        }

        [Test]
        public void MissingCurve_IsRejected()
        {
            Assert.That(TownProgressionPresentation.TryCreate(
                null,
                1,
                0,
                out _), Is.False);
        }
    }
}
#endif
