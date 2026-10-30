#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Character
{
    public sealed class HitReactionPlaybackTests
    {
        private GameObject _root;
        private Transform _visual;
        private SpriteRenderer _renderer;
        private MeleeHitReactionProfile _profile;
        private HitReactionPlayback _playback;
        private readonly Vector3 _position = new Vector3(.25f, -.5f, .3f);
        private readonly Vector3 _scale = new Vector3(-1f, 2f, 1f);
        private readonly Color _color = new Color(.3f, .4f, .5f, .7f);

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Authoritative root");
            _visual = new GameObject("Visual only").transform;
            _visual.SetParent(_root.transform, false);
            _visual.localPosition = _position;
            _visual.localScale = _scale;
            _renderer = _visual.gameObject.AddComponent<SpriteRenderer>();
            _renderer.color = _color;
            _profile = ScriptableObject.CreateInstance<MeleeHitReactionProfile>();
            _playback = new HitReactionPlayback(_visual, new[] { _renderer });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_profile);
        }

        [Test]
        public void DelayedContact_AppliesPeakOnlyAtContact_HoldsThenRestoresExactly()
        {
            Assert.That(_playback.Play(_profile, Vector2.right, .1f, .2f), Is.True);
            AssertBase();
            _playback.Tick(.05f);
            AssertBase();
            _playback.Tick(.05f);
            Vector3 peakPosition = _visual.localPosition;
            Vector3 peakScale = _visual.localScale;
            Color peakColor = _renderer.color;
            Assert.That(peakPosition, Is.EqualTo(_position + Vector3.right / 16f));
            Assert.That(peakScale, Is.EqualTo(Vector3.Scale(_scale, new Vector3(.875f, 1.0625f, 1f))));
            Assert.That(peakColor, Is.EqualTo(Color.Lerp(_color, _profile.FlashColor, _profile.FlashStrength)));
            _playback.Tick(.15f);
            Assert.That(_visual.localPosition, Is.EqualTo(peakPosition));
            Assert.That(_visual.localScale, Is.EqualTo(peakScale));
            Assert.That(_renderer.color, Is.EqualTo(peakColor));
            _playback.Tick(.05f + _profile.RecoverySeconds + .001f);
            AssertBase();
            Assert.That(_playback.IsActive, Is.False);
            Assert.That(_root.transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(_root.transform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void NoHold_StartsAtPeak_UsesFastRecovery_AndDoesNotLeaveResiduals()
        {
            _playback.Play(_profile, Vector2.up, 0f, 0f);
            Assert.That(_visual.localPosition.y, Is.EqualTo(_position.y + 1f / 16f));
            _playback.Tick(_profile.RecoverySeconds * .5f);
            Assert.That(_renderer.color, Is.EqualTo(Color.Lerp(_color, _profile.FlashColor, _profile.FlashStrength * .125f)));
            Assert.That(_visual.localPosition, Is.EqualTo(_position));
            _playback.Tick(_profile.RecoverySeconds);
            AssertBase();
        }

        [Test]
        public void ConsecutiveHits_AfterGuardUseCapturedBase_NotPreviousContribution()
        {
            for (int i = 0; i < 20; i++)
            {
                _playback.Play(_profile, Vector2.right, 0f, .1f);
                Assert.That(_visual.localPosition, Is.EqualTo(_position + Vector3.right / 16f));
                _playback.Tick(.06f);
                _playback.Play(_profile, Vector2.left, 0f, .1f);
                Assert.That(_visual.localPosition, Is.EqualTo(_position - Vector3.right / 16f));
                _playback.Tick(1f);
                AssertBase();
            }
        }

        [Test]
        public void RetriggerInsideGuard_DoesNotRestartOrChangeDirection()
        {
            _playback.Play(_profile, Vector2.right, 0f, .1f);
            _playback.Tick(.025f);
            Assert.That(_playback.Play(_profile, Vector2.left, 0f, .5f), Is.True);
            Assert.That(_visual.localPosition, Is.EqualTo(_position + Vector3.right / 16f));
            _playback.Tick(.1f + _profile.RecoverySeconds);
            Assert.That(_playback.IsActive, Is.False, "Guarded confirmation must not extend the original timeline.");
            AssertBase();
        }

        [Test]
        public void HealthMergeWindow_SurvivesShortRecovery_ButExpiresAndExplicitCancelClearsIt()
        {
            Assert.That(_playback.AbsorbsHealthFeedback(.15f), Is.False);
            _playback.Play(_profile, Vector2.up, 0f, 0f);
            Assert.That(_playback.AbsorbsHealthFeedback(.15f), Is.True);
            _playback.Tick(.13f);
            Assert.That(_playback.IsActive, Is.False);
            Assert.That(_playback.AbsorbsHealthFeedback(.15f), Is.True);
            _playback.Tick(.03f);
            Assert.That(_playback.AbsorbsHealthFeedback(.15f), Is.False);
            _playback.Play(_profile, Vector2.up, 0f, .5f);
            _playback.Restore();
            Assert.That(_playback.AbsorbsHealthFeedback(.15f), Is.False);
            AssertBase();
        }

        [TestCase(0f)]
        [TestCase(.15f)]
        public void ExplicitCancel_RestoresEvenDuringDelayOrHold(float delay)
        {
            _playback.Play(_profile, Vector2.down, delay, .5f);
            _playback.Restore();
            _playback.Restore();
            _playback.Tick(1f);
            AssertBase();
            Assert.That(_playback.IsActive, Is.False);
        }

        [TestCase(1f, .2f, true)]
        [TestCase(-1f, .2f, true)]
        [TestCase(.2f, 1f, false)]
        [TestCase(.2f, -1f, false)]
        [TestCase(0f, 0f, false)]
        [TestCase(1f, 1f, false)]
        [TestCase(float.NaN, 1f, false)]
        public void Squash_UsesDominantAxis_ZeroOrInvalidUsesY(float x, float y, bool horizontal)
        {
            Vector3 factors = HitReactionMath.Scale(new Vector2(x, y), .16f, .5f, 1f);
            Assert.That(horizontal ? factors.x : factors.y, Is.LessThan(1f));
            Assert.That(horizontal ? factors.y : factors.x, Is.GreaterThanOrEqualTo(1f));
            Assert.That(factors.z, Is.EqualTo(1f));
        }

        [TestCase(0f, 0f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 0f)]
        public void ZeroOrInvalidDirection_HasNoNudge(float x, float y)
        {
            _playback.Play(_profile, new Vector2(x, y), 0f, .1f);
            Assert.That(_visual.localPosition, Is.EqualTo(_position));
            Assert.That(Mathf.Abs(_visual.localScale.y), Is.LessThan(_scale.y));
        }

        [Test]
        public void Recovery_OffsetsAndScaleFactorsStayOnPixelGrid()
        {
            for (int i = 0; i <= 100; i++)
            {
                float weight = i / 100f;
                Vector3 offset = HitReactionMath.Offset(new Vector2(1f, -.7f), 4, 16f, weight);
                Vector3 scale = HitReactionMath.Scale(Vector2.right, .25f, 1f, weight);
                AssertGrid(offset.x * 16f);
                AssertGrid(offset.y * 16f);
                AssertGrid(scale.x * 16f);
                AssertGrid(scale.y * 16f);
                Assert.That(scale.x, Is.InRange(.75f, 1f));
                Assert.That(scale.y, Is.InRange(1f, 1.25f));
            }
        }

        [Test]
        public void InvalidProfileOrTiming_DoesNotChangeAnActiveReaction()
        {
            _playback.Play(_profile, Vector2.right, 0f, .1f);
            Vector3 peak = _visual.localPosition;
            Assert.That(_playback.Play(null, Vector2.left, 0f, 0f), Is.False);
            Assert.That(_playback.Play(_profile, Vector2.left, float.NaN, 0f), Is.False);
            Assert.That(_visual.localPosition, Is.EqualTo(peak));
            Assert.That(_playback.IsActive, Is.True);
        }

        [Test]
        public void MissingOrDestroyedVisuals_RestoreSafely()
        {
            var empty = new HitReactionPlayback(null, new SpriteRenderer[] { null });
            empty.Play(_profile, Vector2.one, 0f, 0f);
            empty.Tick(1f);
            empty.Restore();
            Object.DestroyImmediate(_visual.gameObject);
            _playback.Restore();
        }

        [Test]
        public void LargeFiniteDirection_NormalizesWithoutOverflow()
        {
            Assert.That(HitReactionMath.NormalizeDirection(new Vector2(float.MaxValue, 0f)), Is.EqualTo(Vector2.right));
        }

        private void AssertBase()
        {
            Assert.That(_visual.localPosition, Is.EqualTo(_position));
            Assert.That(_visual.localScale, Is.EqualTo(_scale));
            Assert.That(_renderer.color, Is.EqualTo(_color));
        }

        private static void AssertGrid(float value) => Assert.That(value, Is.EqualTo(Mathf.Round(value)).Within(.00001f));
    }
}
#endif
