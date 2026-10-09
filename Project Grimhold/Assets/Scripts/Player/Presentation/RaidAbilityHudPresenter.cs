using UnityEngine;

/// <summary>
/// Projects the confirmed ability state of the Input Authority player into the Raid ability HUD.
/// It polls copied state with dirty checking, derives start edges with one
/// <see cref="AbilityFeedbackTracker"/> per slot, and shows owner-only rejection/interruption cues.
/// It never changes simulation or network state.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidAbilityHudPresenter : MonoBehaviour
{
    private const int SlotCount = 2;

    [SerializeField]
    private RaidAbilityHudView _view;

    [SerializeField]
    [Min(0f)]
    private float _messageDuration = 1.25f;

    private readonly AbilityFeedbackTracker[] _trackers =
    {
        new AbilityFeedbackTracker(),
        new AbilityFeedbackTracker()
    };

    private readonly bool[] _hasObserved = new bool[SlotCount];
    private readonly RaidAbilityHudSlotModel[] _observedModels = new RaidAbilityHudSlotModel[SlotCount];
    private readonly AbilityDefinition[] _observedDefinitions = new AbilityDefinition[SlotCount];
    private readonly float[] _messageUntil = new float[SlotCount];

    private IRaidAbilityHudSource _source;
    private bool _isBound;
    private bool _isSubscribed;

    /// <summary>
    /// Binds the local presentation to the current Input Authority player's ability runtime.
    /// Resource owners are nullable: a missing balance simply never flags insufficiency.
    /// </summary>
    public void Bind(
        PlayerAbilityRuntimeNetworkController abilities,
        PlayerManaNetworkController mana,
        PlayerStaminaNetworkController stamina)
    {
        BindSource(abilities == null ? null : new RaidAbilityHudControllerSource(abilities, mana, stamina));
    }

    internal void BindSource(IRaidAbilityHudSource source)
    {
        Unbind();
        if (source == null)
        {
            return;
        }

        _source = source;
        _isBound = true;
        Subscribe();
        BaselineAll();
        Refresh();
    }

    /// <summary>Unsubscribes, clears all references and observed state, and empties the view.</summary>
    public void Unbind()
    {
        Unsubscribe();
        _source = null;
        _isBound = false;
        ResetObserved();
        if (_view != null)
        {
            _view.Clear();
        }
    }

    private void OnEnable()
    {
        if (!_isBound)
        {
            return;
        }

        Subscribe();
        ResetObserved();
        BaselineAll();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
        ResetObserved();
        if (_view != null)
        {
            _view.Clear();
        }
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Update()
    {
        Refresh();
    }

    internal void Refresh()
    {
        if (!_isBound)
        {
            return;
        }

        RefreshSlot(UniversalAbilitySlot.Slot1);
        RefreshSlot(UniversalAbilitySlot.Slot2);
    }

    private void RefreshSlot(UniversalAbilitySlot slot)
    {
        int index = Index(slot);
        RaidAbilityHudSlotView slotView = _view != null ? _view.For(slot) : null;
        ExpireMessage(index, slotView);

        if (!_source.TryRead(slot, out RaidAbilityHudReading reading))
        {
            // Unconfirmed runtime: show nothing and adopt the next sample silently.
            _trackers[index].Reset();
            if (_hasObserved[index])
            {
                _hasObserved[index] = false;
                _observedDefinitions[index] = null;
                if (slotView != null)
                {
                    slotView.Clear();
                }
            }

            return;
        }

        AbilityFeedbackEdge edges = _trackers[index].Observe(reading.Sequence, reading.Facts.Phase);
        if ((edges & AbilityFeedbackEdge.Started) != 0 && slotView != null)
        {
            slotView.PlayStartedCue();
        }

        RaidAbilityHudSlotModel model = RaidAbilityHudModelBuilder.Build(reading.Facts);
        if (_hasObserved[index] &&
            _observedModels[index].Equals(model) &&
            _observedDefinitions[index] == reading.Definition)
        {
            return;
        }

        _hasObserved[index] = true;
        _observedModels[index] = model;
        _observedDefinitions[index] = reading.Definition;
        if (slotView != null)
        {
            slotView.Present(model, reading.Definition != null ? reading.Definition.Icon : null);
        }
    }

    private void BaselineAll()
    {
        BaselineSlot(UniversalAbilitySlot.Slot1);
        BaselineSlot(UniversalAbilitySlot.Slot2);
    }

    private void BaselineSlot(UniversalAbilitySlot slot)
    {
        AbilityFeedbackTracker tracker = _trackers[Index(slot)];
        if (_source != null && _source.TryRead(slot, out RaidAbilityHudReading reading))
        {
            tracker.Baseline(reading.Sequence, reading.Facts.Phase);
        }
        else
        {
            tracker.Reset();
        }
    }

    private void HandleRejected(UniversalAbilitySlot slot, AbilityActivationFailure failure)
    {
        // A rejection is feedback only: it never touches the trackers, so it cannot look like a start.
        if (!AbilityFeedbackPolicy.ShouldNotifyRejection(failure) || _view == null)
        {
            return;
        }

        RaidAbilityHudSlotView slotView = _view.For(slot);
        if (slotView == null)
        {
            return;
        }

        slotView.PresentRejection(RaidAbilityHudMessages.ForRejection(failure));
        _messageUntil[Index(slot)] = Time.unscaledTime + _messageDuration;
    }

    private void HandleInterrupted(UniversalAbilitySlot slot, AbilityExecutionStopReason reason)
    {
        if (!AbilityFeedbackPolicy.ShouldNotifyInterruption(reason) || _view == null)
        {
            return;
        }

        RaidAbilityHudSlotView slotView = _view.For(slot);
        if (slotView == null)
        {
            return;
        }

        slotView.PresentInterruption(RaidAbilityHudMessages.ForInterruption(reason));
        _messageUntil[Index(slot)] = Time.unscaledTime + _messageDuration;
    }

    private void ExpireMessage(int index, RaidAbilityHudSlotView slotView)
    {
        if (_messageUntil[index] > 0f && Time.unscaledTime >= _messageUntil[index])
        {
            _messageUntil[index] = 0f;
            if (slotView != null)
            {
                slotView.ClearMessage();
            }
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed || _source == null)
        {
            return;
        }

        _source.ActivationRejected += HandleRejected;
        _source.ExecutionInterrupted += HandleInterrupted;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || _source == null)
        {
            _isSubscribed = false;
            return;
        }

        _source.ActivationRejected -= HandleRejected;
        _source.ExecutionInterrupted -= HandleInterrupted;
        _isSubscribed = false;
    }

    private void ResetObserved()
    {
        for (int index = 0; index < SlotCount; index++)
        {
            _hasObserved[index] = false;
            _observedModels[index] = default;
            _observedDefinitions[index] = null;
            _messageUntil[index] = 0f;
            _trackers[index].Reset();
        }
    }

    private static int Index(UniversalAbilitySlot slot) => slot == UniversalAbilitySlot.Slot2 ? 1 : 0;
}
