#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Character
{
    public sealed class DamageFeedbackPresenterTests
    {
        private GameObject _root;
        private Transform _visual;
        private SpriteRenderer _renderer;
        private ReactionTestCharacter _character;
        private DamageFeedbackPresenter _presenter;
        private MeleeHitReactionProfile _profile;
        private readonly Color _baseColor = new Color(.4f, .5f, .6f, 1f);

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Presentation fixture");
            _root.SetActive(false);
            _character = _root.AddComponent<ReactionTestCharacter>();
            _visual = new GameObject("Visual").transform;
            _visual.SetParent(_root.transform, false);
            _renderer = _visual.gameObject.AddComponent<SpriteRenderer>();
            _renderer.color = _baseColor;
            _presenter = _root.AddComponent<DamageFeedbackPresenter>();
            Set("_characterBase", _character);
            Set("_visualTransform", _visual);
            Set("_spriteRenderers", new[] { _renderer });
            _root.SetActive(true);
            // EditMode does not guarantee MonoBehaviour lifecycle dispatch for a non-ExecuteAlways component.
            typeof(DamageFeedbackPresenter).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);
            Set("_duration", float.MaxValue);
            // EditMode has no spawned Fusion object. Emulate the presenter's completed binding;
            // exercise its public presentation API, never write network state.
            Set("_isInitialized", true);
            _profile = ScriptableObject.CreateInstance<MeleeHitReactionProfile>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_profile);
        }

        [Test]
        public void RecentGenericFeedback_IsReplacedWithoutCapturingItsDeformedState()
        {
            HealthDrop();
            Assert.That(_renderer.color, Is.Not.EqualTo(_baseColor));
            Assert.That(Play(), Is.True);
            Assert.That(_renderer.color, Is.EqualTo(Color.Lerp(_baseColor, _profile.FlashColor, _profile.FlashStrength)));
            _presenter.CancelAndRestore();
            AssertBase();
        }

        [Test]
        public void HealthDropAfterMelee_IsAbsorbedWithoutGenericRestart()
        {
            Assert.That(Play(), Is.True);
            Vector3 peakPosition = _visual.localPosition;
            Vector3 peakScale = _visual.localScale;
            Color peakColor = _renderer.color;
            HealthDrop();
            Assert.That(_visual.localPosition, Is.EqualTo(peakPosition));
            Assert.That(_visual.localScale, Is.EqualTo(peakScale));
            Assert.That(_renderer.color, Is.EqualTo(peakColor));
        }

        [Test]
        public void OlderGenericFeedback_IsNotReplacedByAnUnrelatedLateConfirmation()
        {
            HealthDrop();
            Set("_genericStartedAt", Time.time - 1f);
            Color genericColor = _renderer.color;
            Vector3 genericScale = _visual.localScale;
            Assert.That(Play(), Is.False);
            Assert.That(_renderer.color, Is.EqualTo(genericColor));
            Assert.That(_visual.localScale, Is.EqualTo(genericScale));
        }

        [Test]
        public void NonMeleeHealthFeedback_KeepsExistingFlashPulseAndRestoration()
        {
            HealthDrop();
            Assert.That(_renderer.color, Is.Not.EqualTo(_baseColor));
            Assert.That(_visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_visual.localScale.x, Is.GreaterThanOrEqualTo(1f));
            _presenter.CancelAndRestore();
            AssertBase();
        }

        [Test]
        public void Presenter_RetriggerGuardKeepsCurrentDirection()
        {
            Assert.That(Play(), Is.True);
            Vector3 peak = _visual.localPosition;
            Assert.That(_presenter.TryPlayMeleeReaction(_profile, Vector2.left, 0f, .2f), Is.True);
            Assert.That(_visual.localPosition, Is.EqualTo(peak));
        }

        [TestCase("uninitialized")]
        [TestCase("dead")]
        [TestCase("downed")]
        [TestCase("invalid")]
        public void IneligibleMelee_IsRejectedWithoutVisualChanges(string condition)
        {
            if (condition == "uninitialized") Set("_isInitialized", false);
            if (condition == "dead") _character.Alive = false;
            if (condition == "downed") _character.IsDowned = true;
            Assert.That(_presenter.TryPlayMeleeReaction(condition == "invalid" ? null : _profile,
                Vector2.right, 0f, .1f), Is.False);
            AssertBase();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisableOrDespawn_RestoresEveryVisualChannel(bool despawn)
        {
            Assert.That(Play(), Is.True);
            if (despawn)
                typeof(DamageFeedbackPresenter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);
            else
            {
                _root.SetActive(false);
                typeof(DamageFeedbackPresenter).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);
            }
            AssertBase();
            Assert.That(Play(), Is.False);
        }

        private bool Play() => _presenter.TryPlayMeleeReaction(_profile, Vector2.right, 0f, .1f);

        // Isolate the existing Health-drop handler without booting a network runner in EditMode.
        private void HealthDrop() => typeof(DamageFeedbackPresenter)
            .GetMethod("TriggerFeedback", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_presenter, null);

        private void Set(string name, object value) => typeof(DamageFeedbackPresenter)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_presenter, value);

        private void AssertBase()
        {
            Assert.That(_visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(_visual.localScale, Is.EqualTo(Vector3.one));
            Assert.That(_renderer.color, Is.EqualTo(_baseColor));
        }
    }

    public sealed class ReactionTestCharacter : CharacterBase
    {
        public bool Alive = true;
        public bool IsDowned { get; set; }
        public override bool IsAlive => Alive;
    }
}
#endif
