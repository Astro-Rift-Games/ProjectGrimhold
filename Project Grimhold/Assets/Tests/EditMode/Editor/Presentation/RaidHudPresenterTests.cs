#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Globalization;
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
        public void CooldownSecondsUseInvariantCultureAndKeepAttackTextDistinct()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("es-ES");

                _view.PresentAttack(false, 1.2f, 0.6f);

                Assert.That(_view.CooldownSecondsText.text, Is.EqualTo("1.2"));
                Assert.That(_view.AttackText, Is.Not.SameAs(_view.CooldownSecondsText));
                Assert.That(_view.AttackText.text, Is.Empty);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void MissingDependenciesKeepEveryGameplaySectionUnavailable()
        {
            Assert.That(_view.HealthText.text, Is.EqualTo("Salud: — / —"));
            Assert.That(_view.StaminaText.text, Is.EqualTo("Stamina: — / —"));
            Assert.That(_view.AttackText.text, Is.Empty);
            Assert.That(_view.CooldownSecondsText.text, Is.Empty);
            Assert.That(_view.InventoryText.text, Is.EqualTo("Inventario: — / —"));
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
            Assert.That(_view.QuotaText.text, Is.Empty);
            Assert.That(_view.SanctuaryText.text, Is.Empty);
            Assert.That(_view.HealthFill.fillAmount, Is.Zero);
            Assert.That(_view.StaminaFill.fillAmount, Is.Zero);
            Assert.That(_view.CooldownFill.fillAmount, Is.Zero);
            Assert.That(_view.HealthFill.rectTransform.localScale.x, Is.Zero);
            Assert.That(_view.StaminaFill.rectTransform.localScale.x, Is.Zero);
            Assert.That(_view.CooldownRoot.gameObject.activeSelf, Is.False);
            Assert.That(_view.DefeatedRoot.activeSelf, Is.False);
        }

        [Test]
        public void StaminaViewPresentsNormalizedValueAndExhaustion()
        {
            _view.PresentStamina(25.4f, 100.4f, isExhausted: true);

            Assert.That(_view.StaminaText.text, Is.EqualTo("Stamina: 25 / 100 (Agotado)"));
            Assert.That(_view.StaminaFill.fillAmount, Is.EqualTo(25.4f / 100.4f).Within(0.0001f));
            Assert.That(_view.StaminaFill.rectTransform.localScale.x, Is.EqualTo(25.4f / 100.4f).Within(0.0001f));

            _view.PresentStamina(float.NaN, float.PositiveInfinity, isExhausted: false);
            Assert.That(_view.StaminaText.text, Is.EqualTo("Stamina: 0 / 0"));
            Assert.That(_view.StaminaFill.fillAmount, Is.Zero);
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

        [Test]
        public void ExtractionViewRoutesQuotaSanctuaryAndRitualToTheirOwnLabels()
        {
            _view.PresentExtractionProgress(12, 30);
            Assert.That(_view.QuotaText.text, Is.EqualTo("Progreso: 12 / 30"));
            Assert.That(_view.SanctuaryText.text, Is.Empty);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));

            _view.PresentQuotaCompleted();
            Assert.That(_view.QuotaText.text, Is.EqualTo("Cuota completada"));

            _view.PresentSanctuaryAssigned();
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado"));
            Assert.That(_view.QuotaText.text, Is.EqualTo("Cuota completada"));

            _view.PresentRitualProgress(2.34f);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Ritual: 2,3 s"));

            _view.PresentRitualCancelled();
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Ritual cancelado"));

            _view.PresentSanctuaryEnabled();
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Santuario habilitado"));
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado"));
            Assert.That(_view.QuotaText.text, Is.EqualTo("Cuota completada"));
        }

        [Test]
        public void ExtractionViewSectionsClearIndependently()
        {
            _view.PresentExtractionProgress(1, 3);
            _view.PresentSanctuaryAssigned();
            _view.PresentRitualProgress(4f);

            _view.ClearQuota();
            Assert.That(_view.QuotaText.text, Is.Empty);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado"));
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Ritual: 4,0 s"));

            _view.PresentExtractionProgress(1, 3);
            _view.ClearSanctuary();
            Assert.That(_view.SanctuaryText.text, Is.Empty);
            Assert.That(_view.QuotaText.text, Is.EqualTo("Progreso: 1 / 3"));
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Ritual: 4,0 s"));

            _view.PresentSanctuaryAssigned();
            _view.Clear();
            Assert.That(_view.QuotaText.text, Is.Empty);
            Assert.That(_view.SanctuaryText.text, Is.Empty);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
        }

        [Test]
        public void QuotaSectionClearsOnlyItselfWhenItsSourceBecomesInvalid()
        {
            InvokeRefreshSanctuarySection(true);
            InvokeRefreshRitualStatusSection(
                false,
                default,
                true,
                new ExtractionRitualSnapshot(ExtractionRitualState.InProgress, 5f, 2.34f, 0.5f));

            InvokeRefreshQuotaSection(true, new ExtractionProgressSnapshot(12, 30, false));
            Assert.That(_view.QuotaText.text, Is.EqualTo("Progreso: 12 / 30"));

            InvokeRefreshQuotaSection(true, new ExtractionProgressSnapshot(30, 30, true));
            Assert.That(_view.QuotaText.text, Is.EqualTo("Cuota completada"));

            InvokeRefreshQuotaSection(false, default);
            Assert.That(_view.QuotaText.text, Is.Empty);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado"));
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Ritual: 2,4 s"));
        }

        [Test]
        public void SanctuarySectionClearsOnlyItselfWhenItsSourceBecomesInvalid()
        {
            InvokeRefreshQuotaSection(true, new ExtractionProgressSnapshot(2, 5, false));
            InvokeRefreshSanctuarySection(true);
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado"));

            InvokeRefreshSanctuarySection(false);

            Assert.That(_view.SanctuaryText.text, Is.Empty);
            Assert.That(_view.QuotaText.text, Is.EqualTo("Progreso: 2 / 5"));
        }

        [Test]
        public void SectionsSkipViewWritesWhileTheirSourceIsUnchanged()
        {
            InvokeRefreshQuotaSection(true, new ExtractionProgressSnapshot(2, 5, false));
            InvokeRefreshSanctuarySection(true);
            _view.QuotaText.text = "marker-quota";
            _view.SanctuaryText.text = "marker-sanctuary";

            InvokeRefreshQuotaSection(true, new ExtractionProgressSnapshot(2, 5, false));
            InvokeRefreshSanctuarySection(true);

            Assert.That(_view.QuotaText.text, Is.EqualTo("marker-quota"));
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("marker-sanctuary"));

            InvokeRefreshQuotaSection(true, new ExtractionProgressSnapshot(3, 5, false));
            Assert.That(_view.QuotaText.text, Is.EqualTo("Progreso: 3 / 5"));
        }

        [Test]
        public void RitualStatusKeepsDocumentedPriorityOrder()
        {
            var completedRitual = new ExtractionRitualSnapshot(ExtractionRitualState.Completed, 5f, 0f, 1f);
            var activeRitual = new ExtractionRitualSnapshot(ExtractionRitualState.InProgress, 5f, 2.34f, 0.5f);
            var cancelledRitual = new ExtractionRitualSnapshot(ExtractionRitualState.Cancelled, 5f, 5f, 0f);
            var countdown = new ExtractionCountdownSnapshot(
                ExtractionState.InProgress, default, 2.34f, 5f, 0.5f);

            InvokeRefreshRitualStatusSection(true, ExtractionCountdownSnapshot.Extracted(default), true, completedRitual);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("EXTRAÍDO"));

            InvokeRefreshRitualStatusSection(true, countdown, true, completedRitual);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: 2,4 s"));

            InvokeRefreshRitualStatusSection(true, ExtractionCountdownSnapshot.None(), true, completedRitual);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: cancelada"));

            SetPresenterFloat("_cancellationFeedbackUntil", -1f);
            InvokeRefreshRitualStatusSection(true, ExtractionCountdownSnapshot.None(), true, completedRitual);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Santuario habilitado"));

            InvokeRefreshRitualStatusSection(true, ExtractionCountdownSnapshot.None(), true, activeRitual);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Ritual: 2,4 s"));

            InvokeRefreshRitualStatusSection(true, ExtractionCountdownSnapshot.None(), true, cancelledRitual);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Ritual cancelado"));

            InvokeRefreshRitualStatusSection(true, ExtractionCountdownSnapshot.None(), false, default);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
        }

        [Test]
        public void DisableClearsViewButRetainsPresenterBinding()
        {
            _presenter.Bind(null, null, null, null, null, null, null);

            MethodInfo method = typeof(RaidHudPresenter).GetMethod(
                "OnDisable",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(_presenter, null);

            Assert.That(ReadPresenterFlag("_isBound"), Is.True);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
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
            InvokeRefreshCountdownOnly(new ExtractionCountdownSnapshot(
                ExtractionState.InProgress,
                default,
                2.34f,
                5f,
                0.5f));
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: 2,4 s"));

            InvokeRefreshCountdownOnly(ExtractionCountdownSnapshot.None());
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: cancelada"));

            SetPresenterFloat("_cancellationFeedbackUntil", -1f);
            InvokeRefreshCountdownOnly(ExtractionCountdownSnapshot.None());
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
        }

        [Test]
        public void InitialConfirmedTerminalSnapshotDoesNotEmitCancellation()
        {
            InvokeRefreshCountdownOnly(ExtractionCountdownSnapshot.Extracted(default));

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

        private void InvokeRefreshCountdownOnly(ExtractionCountdownSnapshot snapshot)
        {
            InvokeRefreshRitualStatusSection(true, snapshot, false, default);
        }

        private void InvokeRefreshRitualStatusSection(
            bool hasCountdown,
            ExtractionCountdownSnapshot countdown,
            bool hasSanctuary,
            ExtractionRitualSnapshot ritual)
        {
            InvokePresenter("RefreshRitualStatusSection", hasCountdown, countdown, hasSanctuary, ritual);
        }

        private void InvokeRefreshQuotaSection(bool hasProgress, ExtractionProgressSnapshot progress)
        {
            InvokePresenter("RefreshQuotaSection", hasProgress, progress);
        }

        private void InvokeRefreshSanctuarySection(bool hasSanctuary)
        {
            InvokePresenter("RefreshSanctuarySection", hasSanctuary);
        }

        private void InvokePresenter(string methodName, params object[] arguments)
        {
            MethodInfo method = typeof(RaidHudPresenter).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(_presenter, arguments);
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
