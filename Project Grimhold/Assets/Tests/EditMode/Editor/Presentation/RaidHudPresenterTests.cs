#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidHudPresenterTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        private GameObject _instance;
        private RaidHudPresenter _presenter;
        private RaidHudView _view;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _instance = Object.Instantiate(prefab);
            _instance.SetActive(false);
            _presenter = _instance.GetComponentInChildren<RaidHudPresenter>(true);
            _view = _instance.GetComponentInChildren<RaidHudView>(true);
            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_view, Is.Not.Null);
            _presenter.Bind(null, null, null, null);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_instance);
        }

        [TestCase(PlayerClassId.Melee, "Clase: Caballero")]
        [TestCase(PlayerClassId.Ranged, "Clase: Mago")]
        public void SupportedClassPresentsSelectedClass(PlayerClassId playerClass, string expected)
        {
            _presenter.SetPlayerClass(playerClass);

            Assert.That(_view.ClassText.text, Is.EqualTo(expected));
            Assert.That(ReadPresenterFlag("_classResolved"), Is.True);
        }

        [TestCase(PlayerClassId.None)]
        [TestCase((PlayerClassId)255)]
        public void InvalidClassRemainsUnavailableAndCanResolveLater(PlayerClassId invalidClass)
        {
            _presenter.SetPlayerClass(invalidClass);
            Assert.That(_view.ClassText.text, Is.EqualTo("Clase: —"));
            Assert.That(ReadPresenterFlag("_classResolved"), Is.False);

            _presenter.SetPlayerClass(PlayerClassId.Ranged);

            Assert.That(_view.ClassText.text, Is.EqualTo("Clase: Mago"));
            Assert.That(ReadPresenterFlag("_classResolved"), Is.True);
        }

        [Test]
        public void UnbindClearsClassFromPreviousBinding()
        {
            _presenter.SetPlayerClass(PlayerClassId.Melee);

            _presenter.Unbind();

            Assert.That(_view.ClassText.text, Is.EqualTo("Clase: —"));
            Assert.That(ReadPresenterFlag("_classResolved"), Is.False);
            Assert.That(ReadPresenterFlag("_isBound"), Is.False);
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(-1f, 1f, 0f)]
        [TestCase(1f, -1f, 0f)]
        [TestCase(float.NaN, 1f, 0f)]
        [TestCase(float.PositiveInfinity, 1f, 0f)]
        [TestCase(1f, float.NegativeInfinity, 0f)]
        [TestCase(2f, 1f, 0.5f)]
        [TestCase(1f, 2f, 1f)]
        public void CooldownNormalizationProducesSafeObservableFill(
            float duration,
            float remaining,
            float expected)
        {
            float normalized = InvokeNormalizeCooldown(duration, remaining);

            _view.PresentAttack(false, remaining, normalized);

            Assert.That(_view.CooldownFill.fillAmount, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(_view.CooldownFill.fillAmount, Is.InRange(0f, 1f));
            Assert.That(_view.CooldownFill.rectTransform.localScale, Is.EqualTo(Vector3.one));
        }

        [Test]
        public void MissingDependenciesKeepEveryGameplaySectionUnavailable()
        {
            Assert.That(_view.HealthText.text, Is.EqualTo("Salud: — / —"));
            Assert.That(_view.AttackText.text, Is.Empty);
            Assert.That(_view.CooldownSecondsText.text, Is.Empty);
            Assert.That(_view.InventoryText.text, Is.EqualTo("Inventario: — / —"));
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
            Assert.That(_view.HealthFill.fillAmount, Is.Zero);
            Assert.That(_view.CooldownFill.fillAmount, Is.Zero);
            Assert.That(_view.HealthFill.rectTransform.localScale.x, Is.Zero);
            Assert.That(_view.CooldownRoot.gameObject.activeSelf, Is.False);
            Assert.That(_view.DefeatedRoot.activeSelf, Is.False);
        }

        [Test]
        public void RepeatingAnIdenticalValueDoesNotDirtyTheTextAgain()
        {
            _view.PresentClass("Caballero");
            _view.ClassText.havePropertiesChanged = false;

            _view.PresentClass("Caballero");

            Assert.That(_view.ClassText.havePropertiesChanged, Is.False);
        }

        [TestCase(3.2f, "Extracción: 3,2 s")]
        [TestCase(float.NaN, "Extracción: 0,0 s")]
        [TestCase(-1f, "Extracción: 0,0 s")]
        public void ExtractionViewPresentsSanitizedCountdown(float remainingSeconds, string expected)
        {
            _view.PresentExtractionCountdown(remainingSeconds);

            Assert.That(_view.ExtractionText.text, Is.EqualTo(expected));
        }

        [Test]
        public void ExtractionViewPresentsCancellationAndTerminalState()
        {
            _view.PresentExtractionCancelled();
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: cancelada"));

            _view.PresentExtractionCompleted();
            Assert.That(_view.ExtractionText.text, Is.EqualTo("EXTRAÍDO"));
        }

        [TestCase(3.21f, 3.3f)]
        [TestCase(3.2f, 3.2f)]
        [TestCase(0f, 0f)]
        [TestCase(-1f, 0f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        public void ExtractionRemainingIsRoundedUpWithoutLeavingValidRange(float remainingSeconds, float expected)
        {
            float sanitized = InvokeSanitizeExtractionRemaining(remainingSeconds);

            Assert.That(sanitized, Is.EqualTo(expected).Within(0.0001f));
            Assert.That(sanitized, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void ConfirmedExtractionSnapshotsDriveBaselineAndOneShotCancellation()
        {
            InvokeApplyExtractionSnapshot(new ExtractionCountdownSnapshot(
                ExtractionState.InProgress,
                default,
                2.34f,
                5f,
                0.5f));
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: 2,4 s"));

            InvokeApplyExtractionSnapshot(ExtractionCountdownSnapshot.None());
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: cancelada"));

            SetPresenterFloat("_cancellationFeedbackUntil", -1f);
            InvokeApplyExtractionSnapshot(ExtractionCountdownSnapshot.None());
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
        }

        [Test]
        public void InitialConfirmedTerminalSnapshotDoesNotEmitCancellation()
        {
            InvokeApplyExtractionSnapshot(ExtractionCountdownSnapshot.Extracted(default));

            Assert.That(_view.ExtractionText.text, Is.EqualTo("EXTRAÍDO"));
        }

        private static float InvokeNormalizeCooldown(float duration, float remaining)
        {
            MethodInfo method = typeof(RaidHudPresenter).GetMethod(
                "NormalizeCooldown",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, new object[] { duration, remaining });
        }

        private static float InvokeSanitizeExtractionRemaining(float remaining)
        {
            MethodInfo method = typeof(RaidHudPresenter).GetMethod(
                "SanitizeExtractionRemaining",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, new object[] { remaining });
        }

        private void InvokeApplyExtractionSnapshot(ExtractionCountdownSnapshot snapshot)
        {
            MethodInfo method = typeof(RaidHudPresenter).GetMethod(
                "ApplyExtractionSnapshot",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, new object[] { snapshot });
        }

        private void SetPresenterFloat(string fieldName, float value)
        {
            FieldInfo field = typeof(RaidHudPresenter).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(_presenter, value);
        }

        private bool ReadPresenterFlag(string fieldName)
        {
            FieldInfo field = typeof(RaidHudPresenter).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (bool)field.GetValue(_presenter);
        }
    }
}
#endif
