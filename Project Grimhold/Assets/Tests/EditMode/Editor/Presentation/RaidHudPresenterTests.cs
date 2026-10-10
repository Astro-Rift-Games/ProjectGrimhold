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
            _presenter.Bind(null, null, null);
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
        public void ViewNoLongerExposesTheInventorySummary()
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Assert.That(typeof(RaidHudView).GetProperty("InventoryText", all), Is.Null);
            Assert.That(typeof(RaidHudView).GetField("_inventoryText", all), Is.Null);
            Assert.That(typeof(RaidHudView).GetMethod("PresentInventory", all), Is.Null);
            Assert.That(typeof(RaidHudView).GetMethod("ClearInventory", all), Is.Null);
        }

        [Test]
        public void PresenterNoLongerReadsThePlayerLootReceiverForTheHud()
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo field in typeof(RaidHudPresenter).GetFields(all))
            {
                Assert.That(field.FieldType, Is.Not.EqualTo(typeof(PlayerLootReceiver)), field.Name);
            }

            foreach (MethodInfo method in typeof(RaidHudPresenter).GetMethods(all))
            {
                foreach (ParameterInfo parameter in method.GetParameters())
                {
                    Assert.That(parameter.ParameterType, Is.Not.EqualTo(typeof(PlayerLootReceiver)), method.Name);
                }
            }

            Assert.That(typeof(RaidHudPresenter).GetMethod("RefreshInventoryIfNeeded", all), Is.Null);
        }

        [Test]
        public void MissingDependenciesKeepEveryGameplaySectionUnavailable()
        {
            Assert.That(_view.HealthText.text, Is.EqualTo("— / —"));
            Assert.That(_view.StaminaText.text, Is.EqualTo("— / —"));
            Assert.That(_view.ManaText.text, Is.EqualTo("— / —"));
            Assert.That(_view.AttackText.text, Is.Empty);
            Assert.That(_view.CooldownSecondsText.text, Is.Empty);
            Assert.That(_view.ExtractionText.text, Is.EqualTo("Extracción: no disponible"));
            Assert.That(_view.QuotaText.text, Is.Empty);
            Assert.That(_view.SanctuaryText.text, Is.Empty);
            Assert.That(_view.HealthFill.fillAmount, Is.Zero);
            Assert.That(_view.StaminaFill.fillAmount, Is.Zero);
            Assert.That(_view.ManaFill.fillAmount, Is.Zero);
            Assert.That(_view.CooldownFill.fillAmount, Is.Zero);
            Assert.That(_view.HealthFill.rectTransform.localScale.x, Is.Zero);
            Assert.That(_view.StaminaFill.rectTransform.localScale.x, Is.Zero);
            Assert.That(_view.ManaFill.rectTransform.localScale.x, Is.Zero);
            Assert.That(_view.CooldownRoot.gameObject.activeSelf, Is.False);
            Assert.That(_view.DefeatedRoot.activeSelf, Is.False);
        }

        [Test]
        public void StaminaViewPresentsNormalizedValueAndExhaustion()
        {
            _view.PresentStamina(25.4f, 100.4f, isExhausted: true);

            Assert.That(_view.StaminaText.text, Is.EqualTo("25 / 100 (Agotado)"));
            Assert.That(_view.StaminaFill.fillAmount, Is.EqualTo(25.4f / 100.4f).Within(0.0001f));
            Assert.That(_view.StaminaFill.rectTransform.localScale.x, Is.EqualTo(25.4f / 100.4f).Within(0.0001f));

            _view.PresentStamina(float.NaN, float.PositiveInfinity, isExhausted: false);
            Assert.That(_view.StaminaText.text, Is.EqualTo("0 / 0"));
            Assert.That(_view.StaminaFill.fillAmount, Is.Zero);
        }

        [TestCase(40f, 100f, "40 / 100", 0.4f)]
        [TestCase(150f, 100f, "100 / 100", 1f)]
        [TestCase(-5f, 100f, "0 / 100", 0f)]
        [TestCase(float.NaN, float.PositiveInfinity, "0 / 0", 0f)]
        [TestCase(10f, 0f, "0 / 0", 0f)]
        public void ManaViewPresentsClampedValueAndFill(float current, float maximum, string expectedText, float expectedFill)
        {
            _view.PresentMana(current, maximum);

            Assert.That(_view.ManaText.text, Is.EqualTo(expectedText));
            Assert.That(_view.ManaFill.fillAmount, Is.EqualTo(expectedFill).Within(0.0001f));
            Assert.That(_view.ManaFill.rectTransform.localScale.x, Is.EqualTo(expectedFill).Within(0.0001f));
        }

        [Test]
        public void ManaSectionClearsOnlyItselfWhenItsSourceBecomesInvalid()
        {
            _view.PresentHealth(10f, 20f);
            _view.PresentStamina(30f, 60f, isExhausted: false);
            InvokeRefreshManaSection(true, 40f, 80f);
            Assert.That(_view.ManaText.text, Is.EqualTo("40 / 80"));

            InvokeRefreshManaSection(false, 0f, 0f);

            Assert.That(_view.ManaText.text, Is.EqualTo("— / —"));
            Assert.That(_view.ManaFill.fillAmount, Is.Zero);
            Assert.That(_view.HealthText.text, Is.EqualTo("10 / 20"));
            Assert.That(_view.StaminaText.text, Is.EqualTo("30 / 60"));
        }

        [Test]
        public void ManaSectionSkipsViewWritesWhileItsSourceIsUnchanged()
        {
            InvokeRefreshManaSection(true, 40f, 80f);
            _view.ManaText.text = "marker-mana";

            InvokeRefreshManaSection(true, 40f, 80f);
            Assert.That(_view.ManaText.text, Is.EqualTo("marker-mana"));

            InvokeRefreshManaSection(true, 41f, 80f);
            Assert.That(_view.ManaText.text, Is.EqualTo("41 / 80"));

            InvokeRefreshManaSection(false, 0f, 0f);
            _view.ManaText.text = "marker-mana";
            InvokeRefreshManaSection(false, 0f, 0f);
            Assert.That(_view.ManaText.text, Is.EqualTo("marker-mana"), "an already cleared section must not write again");
        }

        [Test]
        public void ManaIsClearedWhenThePresenterIsUnbound()
        {
            InvokeRefreshManaSection(true, 40f, 80f);

            _presenter.Unbind();

            Assert.That(_view.ManaText.text, Is.EqualTo("— / —"));
            Assert.That(_view.ManaFill.fillAmount, Is.Zero);
        }

        [Test]
        public void ViewPresentsVitalsWithoutDuplicatedNameLabels()
        {
            _view.PresentHealth(25f, 75f);
            _view.PresentStamina(75f, 75f, isExhausted: false);
            _view.PresentMana(10f, 50f);

            foreach (string text in new[] { _view.HealthText.text, _view.StaminaText.text, _view.ManaText.text })
            {
                Assert.That(text, Does.Not.Contain("Salud"));
                Assert.That(text, Does.Not.Contain("Stamina"));
                Assert.That(text, Does.Not.Contain("Mana"));
                Assert.That(text, Does.Not.Contain(":"));
            }
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

        [TestCase(78, 100, true, 0.78f)]
        [TestCase(0, 100, true, 0f)]
        [TestCase(100, 100, true, 1f)]
        [TestCase(150, 100, true, 1f)]
        [TestCase(-5, 100, true, 0f)]
        [TestCase(5, 0, false, 0f)]
        [TestCase(5, -3, false, 0f)]
        public void ExpeditionProgressFractionIsNormalizedAndClamped(
            int current,
            int quota,
            bool expectedValid,
            float expectedFraction)
        {
            bool valid = ExpeditionProgressMath.TryGetFraction(current, quota, out float fraction);

            Assert.That(valid, Is.EqualTo(expectedValid));
            Assert.That(fraction, Is.EqualTo(expectedFraction).Within(0.0001f));
        }

        [Test]
        public void ExpeditionProgressIndicatorPresentsFractionAndPercentage()
        {
            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(78, 100, false));

            Assert.That(_view.ProgressRoot.activeSelf, Is.True);
            Assert.That(_view.ProgressFill.fillAmount, Is.EqualTo(0.78f).Within(0.0001f));
            Assert.That(_view.ProgressFill.rectTransform.localScale.x, Is.EqualTo(0.78f).Within(0.0001f));
            Assert.That(_view.ProgressPercentText.text, Is.EqualTo("78%"));

            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(199, 200, false));
            Assert.That(_view.ProgressPercentText.text, Is.EqualTo("99%"));

            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(250, 100, true));
            Assert.That(_view.ProgressFill.fillAmount, Is.EqualTo(1f));
            Assert.That(_view.ProgressPercentText.text, Is.EqualTo("100%"));
        }

        [Test]
        public void ExpeditionProgressIndicatorClearsOnlyItselfWhenSourceIsPendingOrInvalid()
        {
            InvokeRefreshQuotaSection(true, new ExtractionProgressSnapshot(12, 30, false));
            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(12, 30, false));
            InvokeRefreshSanctuarySection(true);

            InvokeRefreshProgressSection(false, default);

            Assert.That(_view.ProgressRoot.activeSelf, Is.False);
            Assert.That(_view.ProgressFill.fillAmount, Is.Zero);
            Assert.That(_view.ProgressPercentText.text, Is.Empty);
            Assert.That(_view.QuotaText.text, Is.EqualTo("Progreso: 12 / 30"));
            Assert.That(_view.SanctuaryText.text, Is.EqualTo("Santuario asignado"));

            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(12, 30, false));
            Assert.That(_view.ProgressRoot.activeSelf, Is.True);

            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(12, 0, false));
            Assert.That(_view.ProgressRoot.activeSelf, Is.False);
            Assert.That(_view.ProgressPercentText.text, Is.Empty);
        }

        [Test]
        public void ExpeditionProgressIndicatorSkipsViewWritesWhileSourceIsUnchanged()
        {
            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(2, 5, false));
            _view.ProgressPercentText.text = "marker-progress";

            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(2, 5, false));
            Assert.That(_view.ProgressPercentText.text, Is.EqualTo("marker-progress"));

            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(3, 5, false));
            Assert.That(_view.ProgressPercentText.text, Is.EqualTo("60%"));
        }

        [Test]
        public void ExpeditionProgressIndicatorIsClearedWhenThePresenterIsUnbound()
        {
            InvokeRefreshProgressSection(true, new ExtractionProgressSnapshot(2, 5, false));

            _presenter.Unbind();

            Assert.That(_view.ProgressRoot.activeSelf, Is.False);
            Assert.That(_view.ProgressPercentText.text, Is.Empty);
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
            _presenter.Bind(null, null, null, null, null, null);

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

        private void InvokeRefreshProgressSection(bool hasProgress, ExtractionProgressSnapshot progress)
        {
            InvokePresenter("RefreshProgressSection", hasProgress, progress);
        }

        private void InvokeRefreshManaSection(bool hasMana, float current, float maximum)
        {
            InvokePresenter("RefreshManaSection", hasMana, current, maximum);
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
