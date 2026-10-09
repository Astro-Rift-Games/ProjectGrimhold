#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class RaidAbilityHudPresenterTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

        private GameObject _instance;
        private RaidAbilityHudPresenter _presenter;
        private RaidAbilityHudView _view;
        private FakeSource _source;
        private AbilityDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            _instance = UnityEngine.Object.Instantiate(prefab);
            _instance.SetActive(false);
            _presenter = _instance.GetComponentInChildren<RaidAbilityHudPresenter>(true);
            _view = _instance.GetComponentInChildren<RaidAbilityHudView>(true);
            Assert.That(_presenter, Is.Not.Null);
            Assert.That(_view, Is.Not.Null);

            _definition = ScriptableObject.CreateInstance<AbilityDefinition>();
            SetField(_definition, "_displayName", "Embestida");
            SetField(_definition, "_resource", AbilityResourceType.Stamina);
            SetField(_definition, "_cost", 15);
            SetField(_definition, "_cooldownSeconds", 6f);
            _source = new FakeSource();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_instance);
            UnityEngine.Object.DestroyImmediate(_definition);
        }

        [Test]
        public void UnavailableSourceShowsEmptySlotsWithKeyLabels()
        {
            _presenter.BindSource(_source);
            _presenter.Refresh();

            Assert.That(_view.Slot1.EmptyRoot.activeSelf, Is.True);
            Assert.That(_view.Slot2.EmptyRoot.activeSelf, Is.True);
            Assert.That(_view.Slot1.KeyLabel.text, Is.EqualTo("Q"));
            Assert.That(_view.Slot2.KeyLabel.text, Is.EqualTo("E"));
            Assert.That(_view.Slot1.SecondsText.text, Is.Empty);
        }

        [Test]
        public void EmptyPreparedSlotShowsEmptyState()
        {
            _source.Set(UniversalAbilitySlot.Slot1, new RaidAbilityHudReading { Facts = default });
            _presenter.BindSource(_source);
            _presenter.Refresh();

            Assert.That(_view.Slot1.EmptyRoot.activeSelf, Is.True);
        }

        [Test]
        public void ReadySlotHidesEmptyAndCooldown()
        {
            _source.Set(UniversalAbilitySlot.Slot1, Reading(1, AbilityExecutionPhase.Idle));
            _presenter.BindSource(_source);
            _presenter.Refresh();

            Assert.That(_view.Slot1.EmptyRoot.activeSelf, Is.False);
            Assert.That(_view.Slot1.CooldownFill.fillAmount, Is.Zero);
            Assert.That(_view.Slot1.SecondsText.text, Is.Empty);
            Assert.That(_view.Slot1.InsufficientOverlay.activeSelf, Is.False);
            Assert.That(_view.Slot1.PhaseHighlight.enabled, Is.False);
        }

        [Test]
        public void CooldownShowsFillAndRoundedSeconds()
        {
            RaidAbilityHudReading reading = Reading(1, AbilityExecutionPhase.Idle);
            reading.Facts.IsOnCooldown = true;
            reading.Facts.RemainingCooldownSeconds = 2.01f;
            _source.Set(UniversalAbilitySlot.Slot2, reading);
            _presenter.BindSource(_source);
            _presenter.Refresh();

            Assert.That(_view.Slot2.CooldownFill.fillAmount, Is.EqualTo(2.01f / 6f).Within(0.0001f));
            Assert.That(_view.Slot2.SecondsText.text, Is.EqualTo((2.1f).ToString("0.0")));
        }

        [Test]
        public void InsufficientResourceShowsOverlay()
        {
            RaidAbilityHudReading reading = Reading(1, AbilityExecutionPhase.Idle);
            reading.Facts.AvailableResource = 3f;
            _source.Set(UniversalAbilitySlot.Slot1, reading);
            _presenter.BindSource(_source);
            _presenter.Refresh();

            Assert.That(_view.Slot1.InsufficientOverlay.activeSelf, Is.True);
        }

        [Test]
        public void ExecutingSlotShowsPhaseHighlight()
        {
            _source.Set(UniversalAbilitySlot.Slot1, Reading(1, AbilityExecutionPhase.Executing));
            _presenter.BindSource(_source);
            _presenter.Refresh();

            Assert.That(_view.Slot1.PhaseHighlight.enabled, Is.True);
        }

        [Test]
        public void BindingDuringActiveExecutionDoesNotReplayStartedCue()
        {
            _source.Set(UniversalAbilitySlot.Slot1, Reading(5, AbilityExecutionPhase.Executing));

            _presenter.BindSource(_source);
            _presenter.Refresh();
            _presenter.Refresh();

            Assert.That(_view.Slot1.StartedCueCount, Is.Zero);
        }

        [Test]
        public void RebindAfterNewExecutionDoesNotReplayStaleStart()
        {
            _source.Set(UniversalAbilitySlot.Slot1, Reading(1, AbilityExecutionPhase.Idle));
            _presenter.BindSource(_source);
            _source.Set(UniversalAbilitySlot.Slot1, Reading(2, AbilityExecutionPhase.Executing));

            // Rebinding must baseline on the current confirmed state, not the previous one.
            _presenter.BindSource(_source);
            _presenter.Refresh();

            Assert.That(_view.Slot1.StartedCueCount, Is.Zero);
        }

        [Test]
        public void NewConfirmedSequenceTriggersStartedCueOnce()
        {
            _source.Set(UniversalAbilitySlot.Slot1, Reading(1, AbilityExecutionPhase.Idle));
            _presenter.BindSource(_source);
            _source.Set(UniversalAbilitySlot.Slot1, Reading(2, AbilityExecutionPhase.Preparing));

            _presenter.Refresh();
            _presenter.Refresh();

            Assert.That(_view.Slot1.StartedCueCount, Is.EqualTo(1));
        }

        [Test]
        public void ReenableRebaselinesAndDoesNotReplayExecutionThatStartedWhileDisabled()
        {
            _source.Set(UniversalAbilitySlot.Slot1, Reading(1, AbilityExecutionPhase.Idle));
            _presenter.BindSource(_source);
            Invoke("OnDisable");
            _source.Set(UniversalAbilitySlot.Slot1, Reading(2, AbilityExecutionPhase.Executing));

            Invoke("OnEnable");
            _presenter.Refresh();

            Assert.That(_view.Slot1.StartedCueCount, Is.Zero);
        }

        [Test]
        public void DisableUnsubscribesAndEnableSubscribesOnce()
        {
            _presenter.BindSource(_source);
            Assert.That(_source.RejectedSubscribers, Is.EqualTo(1));

            Invoke("OnDisable");
            Assert.That(_source.RejectedSubscribers, Is.Zero);
            Assert.That(_source.InterruptedSubscribers, Is.Zero);

            Invoke("OnEnable");
            Invoke("OnEnable");
            Assert.That(_source.RejectedSubscribers, Is.EqualTo(1));
            Assert.That(_source.InterruptedSubscribers, Is.EqualTo(1));
        }

        [Test]
        public void RebindingTwiceKeepsOneSubscription()
        {
            _presenter.BindSource(_source);
            _presenter.BindSource(_source);

            Assert.That(_source.RejectedSubscribers, Is.EqualTo(1));

            _presenter.Unbind();
            Assert.That(_source.RejectedSubscribers, Is.Zero);
        }

        [Test]
        public void RejectionShowsMessageAndFlashButNeverStartsAnExecution()
        {
            _source.Set(UniversalAbilitySlot.Slot1, Reading(1, AbilityExecutionPhase.Idle));
            _presenter.BindSource(_source);

            _source.RaiseRejected(UniversalAbilitySlot.Slot1, AbilityActivationFailure.Cooldown);
            _presenter.Refresh();

            Assert.That(_view.Slot1.MessageText.text, Is.EqualTo("En enfriamiento"));
            Assert.That(_view.Slot1.RejectionFlashCount, Is.EqualTo(1));
            Assert.That(_view.Slot1.StartedCueCount, Is.Zero);
            Assert.That(_view.Slot1.PhaseHighlight.enabled, Is.False);
            Assert.That(_view.Slot2.MessageText.text, Is.Empty);
        }

        [Test]
        public void RejectionOfNoneIsIgnored()
        {
            _presenter.BindSource(_source);

            _source.RaiseRejected(UniversalAbilitySlot.Slot1, AbilityActivationFailure.None);

            Assert.That(_view.Slot1.RejectionFlashCount, Is.Zero);
            Assert.That(_view.Slot1.MessageText.text, Is.Empty);
        }

        [Test]
        public void InterruptionShowsCueButCompletionDoesNot()
        {
            _presenter.BindSource(_source);

            _source.RaiseInterrupted(UniversalAbilitySlot.Slot2, AbilityExecutionStopReason.Completed);
            Assert.That(_view.Slot2.MessageText.text, Is.Empty);

            _source.RaiseInterrupted(UniversalAbilitySlot.Slot2, AbilityExecutionStopReason.Knockback);
            Assert.That(_view.Slot2.MessageText.text, Is.EqualTo("Interrumpida"));
            Assert.That(_view.Slot2.StartedCueCount, Is.Zero);
        }

        [Test]
        public void SourceLossClearsSlotAndRecoveryDoesNotReplay()
        {
            _source.Set(UniversalAbilitySlot.Slot1, Reading(1, AbilityExecutionPhase.Idle));
            _presenter.BindSource(_source);
            _presenter.Refresh();

            _source.Clear(UniversalAbilitySlot.Slot1);
            _presenter.Refresh();
            Assert.That(_view.Slot1.EmptyRoot.activeSelf, Is.True);

            _source.Set(UniversalAbilitySlot.Slot1, Reading(4, AbilityExecutionPhase.Executing));
            _presenter.Refresh();

            Assert.That(_view.Slot1.StartedCueCount, Is.Zero);
        }

        private RaidAbilityHudReading Reading(uint sequence, AbilityExecutionPhase phase)
        {
            return new RaidAbilityHudReading
            {
                Definition = _definition,
                Sequence = sequence,
                Facts = new RaidAbilityHudSlotFacts
                {
                    IsPrepared = true,
                    Phase = phase,
                    TotalCooldownSeconds = 6f,
                    Resource = AbilityResourceType.Stamina,
                    Cost = 15f,
                    HasResourceReading = true,
                    AvailableResource = 100f
                }
            };
        }

        private void Invoke(string method)
        {
            MethodInfo info = typeof(RaidAbilityHudPresenter).GetMethod(
                method, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null);
            info.Invoke(_presenter, null);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private sealed class FakeSource : IRaidAbilityHudSource
        {
            private readonly RaidAbilityHudReading?[] _readings = new RaidAbilityHudReading?[2];
            private Action<UniversalAbilitySlot, AbilityActivationFailure> _rejected;
            private Action<UniversalAbilitySlot, AbilityExecutionStopReason> _interrupted;

            public int RejectedSubscribers => _rejected?.GetInvocationList().Length ?? 0;
            public int InterruptedSubscribers => _interrupted?.GetInvocationList().Length ?? 0;

            public event Action<UniversalAbilitySlot, AbilityActivationFailure> ActivationRejected
            {
                add => _rejected += value;
                remove => _rejected -= value;
            }

            public event Action<UniversalAbilitySlot, AbilityExecutionStopReason> ExecutionInterrupted
            {
                add => _interrupted += value;
                remove => _interrupted -= value;
            }

            public void Set(UniversalAbilitySlot slot, RaidAbilityHudReading reading) =>
                _readings[Index(slot)] = reading;

            public void Clear(UniversalAbilitySlot slot) => _readings[Index(slot)] = null;

            public void RaiseRejected(UniversalAbilitySlot slot, AbilityActivationFailure failure) =>
                _rejected?.Invoke(slot, failure);

            public void RaiseInterrupted(UniversalAbilitySlot slot, AbilityExecutionStopReason reason) =>
                _interrupted?.Invoke(slot, reason);

            public bool TryRead(UniversalAbilitySlot slot, out RaidAbilityHudReading reading)
            {
                RaidAbilityHudReading? value = _readings[Index(slot)];
                reading = value ?? default;
                return value.HasValue;
            }

            private static int Index(UniversalAbilitySlot slot) => slot == UniversalAbilitySlot.Slot2 ? 1 : 0;
        }
    }
}
#endif
