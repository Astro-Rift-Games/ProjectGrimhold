#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidTeammateHudTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        private GameObject _instance;
        private RaidTeammateHudPresenter _presenter;
        private RaidTeammateHudView _view;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _instance = Object.Instantiate(prefab);
            _instance.SetActive(false);
            _presenter = _instance.GetComponentInChildren<RaidTeammateHudPresenter>(true);
            _view = _instance.GetComponentInChildren<RaidTeammateHudView>(true);
            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_view, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_instance);
        }

        [Test]
        public void PrefabStartsHiddenAndHasCompleteViewReferences()
        {
            Assert.That(_view.Root, Is.Not.Null);
            Assert.That(_view.Root.name, Is.EqualTo("RaidDuoHud"));
            Assert.That(_view.Root.activeSelf, Is.False);
            Assert.That(_view.HealthText, Is.Not.Null);
            Assert.That(_view.HealthFill, Is.Not.Null);
        }

        [Test]
        public void ViewPresentsSanitizedHealthDefeatAndUnavailableStates()
        {
            _view.SetVisible(true);
            _view.PresentHealth(25f, 100f);
            Assert.That(_view.HealthText.text, Is.EqualTo("Compañero: 25 / 100"));
            Assert.That(_view.HealthFill.fillAmount, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(_view.HealthFill.rectTransform.localScale.x, Is.EqualTo(0.25f).Within(0.0001f));

            _view.PresentHealth(float.NaN, float.PositiveInfinity);
            Assert.That(_view.HealthText.text, Is.EqualTo("Compañero: 0 / 0"));
            Assert.That(_view.HealthFill.fillAmount, Is.Zero);

            _view.PresentDefeated(120f, hasMaximumHealth: true);
            Assert.That(_view.HealthText.text, Is.EqualTo("Compañero: 0 / 120"));
            _view.PresentDefeated(0f, hasMaximumHealth: false);
            Assert.That(_view.HealthText.text, Is.EqualTo("Compañero: 0 / —"));

            _view.PresentUnavailable();
            Assert.That(_view.HealthText.text, Is.EqualTo("Compañero: — / —"));
            Assert.That(_view.HealthFill.fillAmount, Is.Zero);
        }

        [Test]
        public void PresenterDirtyCheckingAvoidsDuplicateHealthWrites()
        {
            MethodInfo method = typeof(RaidTeammateHudPresenter).GetMethod(
                "PresentHealth",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);

            method.Invoke(_presenter, new object[] { 50f, 100f });
            _view.HealthText.havePropertiesChanged = false;
            method.Invoke(_presenter, new object[] { 50f, 100f });

            Assert.That(_view.HealthText.havePropertiesChanged, Is.False);
        }

        [TestCase(RaidParticipantState.Raiding, true, false,
            (int)RaidTeammateHudPresenter.ProjectionMode.LiveHealth)]
        [TestCase(RaidParticipantState.Raiding, false, true,
            (int)RaidTeammateHudPresenter.ProjectionMode.Unavailable)]
        [TestCase(RaidParticipantState.Defeated, true, true,
            (int)RaidTeammateHudPresenter.ProjectionMode.Defeated)]
        [TestCase(RaidParticipantState.Extracted, false, true,
            (int)RaidTeammateHudPresenter.ProjectionMode.ExtractedCache)]
        [TestCase(RaidParticipantState.Extracted, false, false,
            (int)RaidTeammateHudPresenter.ProjectionMode.Unavailable)]
        [TestCase(RaidParticipantState.Aborted, true, true,
            (int)RaidTeammateHudPresenter.ProjectionMode.Unavailable)]
        public void PresenterSelectsProjectionFromParticipantTerminalState(
            RaidParticipantState state,
            bool hasValidCharacter,
            bool hasCachedHealth,
            int expected)
        {
            Assert.That(
                RaidTeammateHudPresenter.ResolveProjectionMode(
                    state,
                    hasValidCharacter,
                    hasCachedHealth),
                Is.EqualTo((RaidTeammateHudPresenter.ProjectionMode)expected));
        }

        [Test]
        public void UnbindClearsExtractedHealthAndGenerationCaches()
        {
            SetField("_hasLastHealth", true);
            SetField("_lastHealth", 35f);
            SetField("_lastMaximumHealth", 140f);
            SetField("_boundGenerationId", "generation-a");

            _presenter.Unbind();

            Assert.That(ReadField<bool>("_hasLastHealth"), Is.False);
            Assert.That(ReadField<float>("_lastHealth"), Is.Zero);
            Assert.That(ReadField<float>("_lastMaximumHealth"), Is.Zero);
            Assert.That(ReadField<string>("_boundGenerationId"), Is.Empty);
            Assert.That(_view.Root.activeSelf, Is.False);
        }

        private void SetField(string fieldName, object value)
        {
            FieldInfo field = typeof(RaidTeammateHudPresenter).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_presenter, value);
        }

        private T ReadField<T>(string fieldName)
        {
            FieldInfo field = typeof(RaidTeammateHudPresenter).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(_presenter);
        }
    }
}
#endif
