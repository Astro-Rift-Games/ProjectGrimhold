using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;

namespace Tests.EditMode.Visibility
{
    public sealed class VisibilityFadeRulesTests
    {
        private static readonly VisibilityFadeTimings Timings = new VisibilityFadeTimings(
            graceSeconds: 0.3f,
            fadeInSeconds: 0.2f,
            fadeOutSeconds: 0.4f);

        [Test]
        public void Hidden_StartsFullyTransparent()
        {
            Assert.That(VisibilityFadeState.Hidden.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void Seen_FadesInOverFadeInSeconds()
        {
            VisibilityFadeState state = VisibilityFadeState.Hidden;

            state = VisibilityFadeRules.Step(state, true, 0.1f, Timings);
            Assert.That(state.Alpha, Is.EqualTo(0.5f).Within(0.0001f));

            state = VisibilityFadeRules.Step(state, true, 0.1f, Timings);
            Assert.That(state.Alpha, Is.EqualTo(1f).Within(0.0001f));

            state = VisibilityFadeRules.Step(state, true, 1f, Timings);
            Assert.That(state.Alpha, Is.EqualTo(1f));
        }

        [Test]
        public void LostSight_StaysFullyVisibleDuringGrace()
        {
            VisibilityFadeState state = FullyVisible();

            state = VisibilityFadeRules.Step(state, false, 0.29f, Timings);

            Assert.That(state.Alpha, Is.EqualTo(1f));
        }

        [Test]
        public void LostSight_AfterGrace_FadesOutOverFadeOutSeconds()
        {
            VisibilityFadeState state = FullyVisible();

            state = VisibilityFadeRules.Step(state, false, 0.3f, Timings);
            state = VisibilityFadeRules.Step(state, false, 0.2f, Timings);
            Assert.That(state.Alpha, Is.EqualTo(0.5f).Within(0.0001f));

            state = VisibilityFadeRules.Step(state, false, 0.2f, Timings);
            Assert.That(state.Alpha, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void FrameCrossingGraceEnd_OnlyFadesTheTimePastGrace()
        {
            VisibilityFadeState state = FullyVisible();

            state = VisibilityFadeRules.Step(state, false, 0.5f, Timings);

            Assert.That(state.Alpha, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void SeenAgainWhileFading_FadesInFromCurrentAlphaAndResetsGrace()
        {
            VisibilityFadeState state = FullyVisible();
            state = VisibilityFadeRules.Step(state, false, 0.5f, Timings);

            state = VisibilityFadeRules.Step(state, true, 0.05f, Timings);
            Assert.That(state.Alpha, Is.EqualTo(0.75f).Within(0.0001f));

            state = VisibilityFadeRules.Step(state, true, 0.05f, Timings);
            state = VisibilityFadeRules.Step(state, false, 0.29f, Timings);
            Assert.That(state.Alpha, Is.EqualTo(1f).Within(0.0001f), "Grace restarts after being seen again.");
        }

        [Test]
        public void InstantTimings_SnapBetweenHiddenAndVisible()
        {
            VisibilityFadeState state = VisibilityFadeState.Hidden;

            state = VisibilityFadeRules.Step(state, true, 0.016f, VisibilityFadeTimings.Instant);
            Assert.That(state.Alpha, Is.EqualTo(1f));

            state = VisibilityFadeRules.Step(state, false, 0.016f, VisibilityFadeTimings.Instant);
            Assert.That(state.Alpha, Is.EqualTo(0f));
        }

        private static VisibilityFadeState FullyVisible()
        {
            return VisibilityFadeRules.Step(VisibilityFadeState.Hidden, true, 1f, Timings);
        }
    }
}
