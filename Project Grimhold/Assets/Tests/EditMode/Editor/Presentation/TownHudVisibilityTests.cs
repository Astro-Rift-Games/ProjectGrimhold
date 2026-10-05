#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Presentation
{
    public sealed class TownHudVisibilityTests
    {
        private GameObject _hud;
        private GameObject _other;
        private TownHudVisibility _visibility;

        [SetUp]
        public void SetUp()
        {
            _hud = new GameObject("Hud", typeof(RectTransform));
            _other = new GameObject("OtherHud", typeof(RectTransform));
            _visibility = new TownHudVisibility();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_hud);
            Object.DestroyImmediate(_other);
        }

        [Test]
        public void SetHidden_HidesRegisteredTargetsWithoutDeactivatingThem()
        {
            _visibility.Register(_hud);

            _visibility.SetHidden(true);

            var group = _hud.GetComponent<CanvasGroup>();
            Assert.That(group, Is.Not.Null);
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.blocksRaycasts, Is.False);
            Assert.That(group.interactable, Is.False);
            Assert.That(_hud.activeSelf, Is.True, "Hiding must not fight the HUD's own SetActive logic.");
        }

        [Test]
        public void SetHidden_False_RestoresTheOriginalGroupValues()
        {
            var existing = _hud.AddComponent<CanvasGroup>();
            existing.alpha = 0.5f;
            existing.blocksRaycasts = false;
            existing.interactable = true;
            _visibility.Register(_hud);
            _visibility.SetHidden(true);

            _visibility.SetHidden(false);

            Assert.That(existing.alpha, Is.EqualTo(0.5f));
            Assert.That(existing.blocksRaycasts, Is.False);
            Assert.That(existing.interactable, Is.True);
        }

        [Test]
        public void Register_WhileHidden_AppliesTheHiddenState()
        {
            _visibility.SetHidden(true);

            _visibility.Register(_hud);

            Assert.That(_hud.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }

        [Test]
        public void Unregister_WhileHidden_RestoresThatTarget()
        {
            _visibility.Register(_hud);
            _visibility.Register(_other);
            _visibility.SetHidden(true);

            _visibility.Unregister(_hud);

            Assert.That(_hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(_other.GetComponent<CanvasGroup>().alpha, Is.Zero);
        }

        [Test]
        public void RegisteringTwice_DoesNotCaptureTheHiddenValuesAsOriginal()
        {
            _visibility.Register(_hud);
            _visibility.SetHidden(true);
            _visibility.Register(_hud);

            _visibility.SetHidden(false);

            Assert.That(_hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(_hud.GetComponent<CanvasGroup>().blocksRaycasts, Is.True);
        }

        [Test]
        public void NullAndDestroyedTargets_AreIgnored()
        {
            _visibility.Register(null);
            _visibility.Register(_other);
            Object.DestroyImmediate(_other);

            Assert.DoesNotThrow(() => _visibility.SetHidden(true));
            Assert.DoesNotThrow(() => _visibility.SetHidden(false));
            Assert.DoesNotThrow(() => _visibility.Unregister(null));
        }

        [Test]
        public void SetHidden_WithNothingRegistered_IsHarmless()
        {
            Assert.DoesNotThrow(() => _visibility.SetHidden(true));
            Assert.That(_visibility.IsHidden, Is.True);
        }
    }
}
#endif
