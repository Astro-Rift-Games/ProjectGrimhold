#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Player.Presentation
{
    public sealed class DownedPostProcessBlendTests
    {
        private const float Frame = 1f / 60f;

        private DownedPostProcessConfig _config;
        private DownedPostProcessBlend _blend;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<DownedPostProcessConfig>();
            _blend = new DownedPostProcessBlend(_config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Config_DefaultsToFullDesaturationWithinTheRequestedTimings()
        {
            Assert.That(_config.Saturation, Is.EqualTo(-100f));
            Assert.That(_config.FadeInSeconds, Is.InRange(0.3f, 0.5f));
            Assert.That(_config.FadeOutSeconds, Is.EqualTo(0.4f));
        }

        [Test]
        public void Step_WhileActive_IsInactive()
        {
            DownedPostProcessFrame frame = StepActive(1f);

            Assert.That(frame.IsActive, Is.False);
            Assert.That(frame.Weight, Is.EqualTo(0f));
        }

        [Test]
        public void Step_EnteringDowned_FadesToBlackAndWhiteOverConfiguredDuration()
        {
            DownedPostProcessFrame half = StepDowned(1f, _config.FadeInSeconds * 0.5f);
            Assert.That(half.Weight, Is.GreaterThan(0f).And.LessThan(1f));

            DownedPostProcessFrame full = StepDowned(1f, _config.FadeInSeconds * 0.5f);
            Assert.That(full.Weight, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(full.Saturation, Is.EqualTo(-100f));
            Assert.That(full.Contrast, Is.EqualTo(_config.Contrast));
            Assert.That(full.VignetteIntensity, Is.EqualTo(_config.VignetteIntensity).Within(1e-5f));
            Assert.That(full.PostExposure, Is.EqualTo(0f).Within(1e-5f));
        }

        [Test]
        public void Step_WhileDownedAndWaiting_StaysBlackAndWhite()
        {
            StepDowned(1f, _config.FadeInSeconds);
            for (int i = 0; i < 300; i++)
            {
                DownedPostProcessFrame frame = StepDowned(1f, Frame);
                Assert.That(frame.Weight, Is.EqualTo(1f));
                Assert.That(frame.Saturation, Is.EqualTo(-100f));
            }
        }

        [Test]
        public void Step_EnterAndExit_NeverJumpsInASingleFrame()
        {
            float previous = 0f;
            for (int i = 0; i < 60; i++)
            {
                float weight = StepDowned(1f, Frame).Weight;
                Assert.That(Mathf.Abs(weight - previous), Is.LessThan(0.1f), $"enter frame {i}");
                previous = weight;
            }

            for (int i = 0; i < 60; i++)
            {
                float weight = StepActive(Frame).Weight;
                Assert.That(Mathf.Abs(weight - previous), Is.LessThan(0.1f), $"exit frame {i}");
                previous = weight;
            }

            Assert.That(previous, Is.EqualTo(0f));
        }

        [Test]
        public void Step_LeavingDowned_RestoresColourOverConfiguredDuration()
        {
            StepDowned(1f, _config.FadeInSeconds);

            DownedPostProcessFrame half = StepActive(_config.FadeOutSeconds * 0.5f);
            Assert.That(half.Weight, Is.GreaterThan(0f).And.LessThan(1f));

            DownedPostProcessFrame done = StepActive(_config.FadeOutSeconds * 0.5f);
            Assert.That(done.IsActive, Is.False);
        }

        [Test]
        public void Step_DepletingReserve_DeepensVignetteAndDimmingProgressively()
        {
            DownedPostProcessFrame full = StepDowned(1f, _config.FadeInSeconds);
            DownedPostProcessFrame half = StepDowned(0.5f, _config.ReserveResponseSeconds);
            DownedPostProcessFrame empty = StepDowned(0f, _config.ReserveResponseSeconds);

            Assert.That(half.VignetteIntensity, Is.GreaterThan(full.VignetteIntensity));
            Assert.That(empty.VignetteIntensity, Is.GreaterThan(half.VignetteIntensity));
            Assert.That(empty.VignetteIntensity,
                Is.EqualTo(_config.VignetteIntensity + _config.DepletedVignetteBoost).Within(1e-4f));
            Assert.That(half.PostExposure, Is.LessThan(full.PostExposure));
            Assert.That(empty.PostExposure, Is.EqualTo(-_config.DepletedExposureDim).Within(1e-4f));
        }

        [Test]
        public void Step_DamageChunk_EasesTheReserveModulation()
        {
            DownedPostProcessFrame before = StepDowned(1f, _config.FadeInSeconds);

            DownedPostProcessFrame hit = StepDowned(0.2f, Frame);

            Assert.That(hit.VignetteIntensity - before.VignetteIntensity, Is.LessThan(0.02f));
            Assert.That(hit.VignetteIntensity, Is.GreaterThan(before.VignetteIntensity));
        }

        [Test]
        public void Step_ReviveInProgress_RecoversColourGradually()
        {
            DownedPostProcessFrame before = StepDowned(1f, _config.FadeInSeconds);

            DownedPostProcessFrame first = StepReviving(0.5f, Frame);
            Assert.That(first.Saturation, Is.GreaterThan(before.Saturation));
            Assert.That(first.Saturation - before.Saturation, Is.LessThan(10f));

            DownedPostProcessFrame halfway = StepReviving(0.5f, _config.ReviveResponseSeconds);
            DownedPostProcessFrame complete = StepReviving(1f, _config.ReviveResponseSeconds);
            Assert.That(halfway.Saturation, Is.EqualTo(-100f * (1f - 0.5f * _config.ReviveColorRecovery)).Within(1e-3f));
            Assert.That(complete.Saturation, Is.EqualTo(-100f * (1f - _config.ReviveColorRecovery)).Within(1e-3f));
        }

        [Test]
        public void Step_ReviveInterrupted_ReturnsSmoothlyToBlackAndWhite()
        {
            StepDowned(1f, _config.FadeInSeconds);
            DownedPostProcessFrame reviving = StepReviving(1f, _config.ReviveResponseSeconds);

            DownedPostProcessFrame interrupted = StepDowned(1f, Frame);
            Assert.That(reviving.Saturation - interrupted.Saturation, Is.LessThan(10f));

            DownedPostProcessFrame settled = StepDowned(1f, _config.ReviveResponseSeconds);
            Assert.That(settled.Saturation, Is.EqualTo(-100f).Within(1e-3f));
        }

        [Test]
        public void Step_AfterSuccessfulRevive_KeepsTheLastLookWhileFadingOut()
        {
            StepDowned(1f, _config.FadeInSeconds);
            DownedPostProcessFrame revived = StepReviving(1f, _config.ReviveResponseSeconds);

            // Revive completion clears the reserve and the session in the same tick IsDowned becomes false.
            for (int i = 0; i < 10; i++)
            {
                DownedPostProcessFrame frame = _blend.Step(false, 0f, false, 0f, Frame);
                Assert.That(frame.VignetteIntensity, Is.EqualTo(revived.VignetteIntensity).Within(1e-5f));
                Assert.That(frame.PostExposure, Is.EqualTo(revived.PostExposure).Within(1e-5f));
                Assert.That(frame.Saturation, Is.EqualTo(revived.Saturation).Within(1e-5f));
            }
        }

        [Test]
        public void Step_NewDownedCycle_StartsFromItsOwnReserve()
        {
            StepDowned(0f, _config.FadeInSeconds);
            StepDowned(0f, _config.ReserveResponseSeconds);
            StepActive(_config.FadeOutSeconds);

            DownedPostProcessFrame next = StepDowned(1f, Frame);

            Assert.That(next.VignetteIntensity, Is.EqualTo(_config.VignetteIntensity).Within(1e-5f));
        }

        [Test]
        public void Reset_ClearsTheEffectImmediately()
        {
            StepDowned(0.2f, _config.FadeInSeconds);

            _blend.Reset();

            Assert.That(StepActive(0f).IsActive, Is.False);
        }

        [TestCase(float.NaN)]
        [TestCase(-1f)]
        [TestCase(float.PositiveInfinity)]
        public void Step_InvalidDeltaTime_LeavesTransitionUnchanged(float deltaTime)
        {
            float before = StepDowned(1f, _config.FadeInSeconds * 0.5f).Weight;

            float after = StepDowned(1f, deltaTime).Weight;

            Assert.That(after, Is.EqualTo(before));
        }

        private DownedPostProcessFrame StepDowned(float reserve, float deltaTime) =>
            _blend.Step(true, reserve, false, 0f, deltaTime);

        private DownedPostProcessFrame StepReviving(float progress, float deltaTime) =>
            _blend.Step(true, 1f, true, progress, deltaTime);

        private DownedPostProcessFrame StepActive(float deltaTime) =>
            _blend.Step(false, 0f, false, 0f, deltaTime);
    }
}
#endif
