#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class DungeonPressureHudPresenterTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        private GameObject _instance;
        private DungeonPressureHudPresenter _presenter;
        private DungeonPressureHudView _view;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _instance = Object.Instantiate(prefab);
            _instance.SetActive(false);
            _presenter = _instance.GetComponentInChildren<DungeonPressureHudPresenter>(true);
            _view = _instance.GetComponentInChildren<DungeonPressureHudView>(true);
            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_view, Is.Not.Null);
            Assert.That(_view.TimerText, Is.Not.Null);
            Assert.That(_view.PhaseText, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_instance);
        }

        [TestCase(DungeonPressurePhase.Normal, "Exploración")]
        [TestCase(DungeonPressurePhase.Reinforcements, "Refuerzos")]
        [TestCase(DungeonPressurePhase.CriticalPressure, "Presión Crítica")]
        [TestCase(DungeonPressurePhase.Collapse, "COLAPSO")]
        public void PhaseTextMapsEveryPressurePhase(DungeonPressurePhase phase, string expected)
        {
            Assert.That(DungeonPressureHudPresenter.GetPhaseText(phase), Is.EqualTo(expected));
        }

        [Test]
        public void ValidReadPresentsTimerAndPhaseWithPhaseColor()
        {
            _presenter.Present(true, 125, DungeonPressurePhase.Reinforcements);

            Assert.That(_view.TimerText.text, Is.EqualTo("02:05"));
            Assert.That(_view.PhaseText.text, Is.EqualTo("Refuerzos"));
            Assert.That(_view.PhaseText.color, Is.EqualTo(Color.yellow));
        }

        [Test]
        public void InvalidSourceClearsOnlyThisSectionToUnavailable()
        {
            _presenter.Present(true, 61, DungeonPressurePhase.Normal);

            _presenter.Present(false, 0, DungeonPressurePhase.Normal);

            Assert.That(_view.TimerText.text, Is.EqualTo("--:--"));
            Assert.That(_view.PhaseText.text, Is.Empty);
        }

        [Test]
        public void UnchangedReadDoesNotRewriteTextsAndChangedReadDoes()
        {
            _presenter.Present(true, 90, DungeonPressurePhase.Normal);
            _view.TimerText.havePropertiesChanged = false;
            _view.PhaseText.havePropertiesChanged = false;

            _presenter.Present(true, 90, DungeonPressurePhase.Normal);

            Assert.That(_view.TimerText.havePropertiesChanged, Is.False);
            Assert.That(_view.PhaseText.havePropertiesChanged, Is.False);

            _presenter.Present(true, 89, DungeonPressurePhase.Normal);

            Assert.That(_view.TimerText.text, Is.EqualTo("01:29"));
        }

        [Test]
        public void UnbindIsIdempotentAndClearsPresentedState()
        {
            _presenter.Present(true, 30, DungeonPressurePhase.Collapse);

            _presenter.Unbind();
            _presenter.Unbind();

            Assert.That(_view.TimerText.text, Is.EqualTo("--:--"));
            Assert.That(_view.PhaseText.text, Is.Empty);
        }

        [Test]
        public void BindStartsWithUnbindAndFirstValidReadIsBaseline()
        {
            _presenter.Present(true, 45, DungeonPressurePhase.CriticalPressure);

            _presenter.Bind(null, null);

            Assert.That(_view.TimerText.text, Is.EqualTo("--:--"));

            _presenter.Present(true, 45, DungeonPressurePhase.CriticalPressure);

            Assert.That(_view.TimerText.text, Is.EqualTo("00:45"));
            Assert.That(_view.PhaseText.text, Is.EqualTo("Presión Crítica"));
        }

        [Test]
        public void NetworkPlayerBinderReferencesThePressurePresenter()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(playerPrefab, Is.Not.Null);
            LocalPlayerHudBinder binder = playerPrefab.GetComponentInChildren<LocalPlayerHudBinder>(true);
            Assert.That(binder, Is.Not.Null);

            var serialized = new SerializedObject(binder);
            SerializedProperty property = serialized.FindProperty("_dungeonPressureHudPresenter");

            Assert.That(property, Is.Not.Null);
            Assert.That(property.objectReferenceValue, Is.InstanceOf<DungeonPressureHudPresenter>());
        }
    }
}
#endif
