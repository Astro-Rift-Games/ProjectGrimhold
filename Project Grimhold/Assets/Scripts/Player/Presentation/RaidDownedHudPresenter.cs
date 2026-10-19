using UnityEngine;

/// <summary>
/// Projects the replicated Downed and revive state into the local HUD for both participants: the
/// Downed player (reserve, drain pause, progress, notices) and the reviver (ally Downed, progress,
/// notices). Presentation only: it reads <c>IsDowned</c>, <c>DownedHealth</c> and the recovery
/// session/outcome of <see cref="PlayerDownedRecoveryNetworkController"/> and never writes state.
/// Outcome notices edge-detect on the replicated sequence, so late join, rebind and restore never
/// replay an old outcome.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidDownedHudPresenter : MonoBehaviour
{
    [SerializeField]
    private RaidDownedHudView _view;

    [SerializeField, Min(0f)]
    private float _messageDuration = 2f;

    [Header("Audio (optional, 2D)")]
    [SerializeField]
    private CustomClip _enteredDownedSound;

    [SerializeField]
    private CustomClip _interruptedSound;

    [SerializeField]
    private CustomClip _completedSound;

    /// <summary>Test seam: stands in for the Duo teammate, which needs a Raid launch context to resolve.</summary>
    internal PlayerCharacter TestAllyOverride { get; set; }

    private readonly DownedRecoveryOutcomeTracker _ownOutcome = new();
    private readonly DownedRecoveryOutcomeTracker _allyOutcome = new();

    private PlayerCharacter _character;
    private PlayerDownedStateNetworkController _downedState;
    private PlayerDownedRecoveryNetworkController _ownRecovery;
    private RaidTeammateHudPresenter _teammate;
    private PlayerCharacter _allyCharacter;
    private PlayerDownedRecoveryNetworkController _allyRecovery;
    private bool _isBound;
    private bool _wasDowned;
    private bool _hasDownedBaseline;
    private float _messageUntil;

    public void Bind(PlayerCharacter character, RaidTeammateHudPresenter teammate)
    {
        Unbind();
        if (character == null ||
            !character.TryGetComponent(out _downedState) ||
            !character.TryGetComponent(out _ownRecovery))
        {
            _downedState = null;
            _ownRecovery = null;
            return;
        }

        _character = character;
        _teammate = teammate;
        _isBound = true;
        Baseline();
        Refresh();
    }

    public void Unbind()
    {
        _isBound = false;
        _character = null;
        _downedState = null;
        _ownRecovery = null;
        _teammate = null;
        ClearAlly();
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

        Baseline();
        Refresh();
    }

    private void OnDisable()
    {
        ResetObserved();
        if (_view != null)
        {
            _view.Clear();
        }
    }

    private void OnDestroy()
    {
        _isBound = false;
    }

    private void Update()
    {
        Refresh();
    }

    private void Baseline()
    {
        ResetObserved();
        if (HasValidSources())
        {
            _wasDowned = _character.IsDowned;
            _hasDownedBaseline = true;
            _ownOutcome.Baseline(_ownRecovery.LastOutcomeSequence);
        }
    }

    private void ResetObserved()
    {
        _wasDowned = false;
        _hasDownedBaseline = false;
        _messageUntil = 0f;
        _ownOutcome.Reset();
        _allyOutcome.Reset();
    }

    private bool HasValidSources() =>
        _character != null && _character.Object != null && _character.Object.IsValid &&
        _downedState != null && _ownRecovery != null;

    internal void Refresh()
    {
        if (!_isBound || _view == null)
        {
            return;
        }

        if (!HasValidSources())
        {
            _view.Clear();
            return;
        }

        bool isDowned = _character.IsDowned;
        if (!_hasDownedBaseline)
        {
            _wasDowned = isDowned;
            _hasDownedBaseline = true;
        }
        else if (isDowned && !_wasDowned)
        {
            _view.PulseDowned();
            Play(_enteredDownedSound);
        }

        _wasDowned = isDowned;

        RefreshDowned(isDowned);
        RefreshAlly(isDowned);
        RefreshNotices();
    }

    private void RefreshDowned(bool isDowned)
    {
        if (!isDowned)
        {
            _view.PresentDowned(false, 0f, string.Empty, false);
            return;
        }

        float reserve = _downedState.DownedHealth;
        float initial = _downedState.InitialDownedHealth;
        _view.PresentDowned(
            true,
            DownedRecoveryFeedbackRules.ReserveFraction(reserve, initial),
            $"{Mathf.CeilToInt(Mathf.Max(0f, reserve))} / {Mathf.CeilToInt(initial)}",
            _ownRecovery.HasValidSession);
    }

    private void RefreshAlly(bool isDowned)
    {
        PlayerCharacter ally = null;
        bool hasAlly = TestAllyOverride != null
            ? (ally = TestAllyOverride) != null
            : _teammate != null && _teammate.TryGetTeammateCharacter(out ally);
        if (!hasAlly)
        {
            ClearAlly();
            _view.PresentAllyDowned(false, false);
            ShowProgress(isDowned, null);
            return;
        }

        if (!ReferenceEquals(ally, _allyCharacter))
        {
            ClearAlly();
            _allyCharacter = ally;
            if (ally.TryGetComponent(out _allyRecovery))
            {
                _allyOutcome.Baseline(_allyRecovery.LastOutcomeSequence);
            }
        }

        bool allyDowned = !isDowned && _character.IsAlive && ally.IsDowned;
        _view.PresentAllyDowned(allyDowned, allyDowned && IsWithinReviveRange(ally));
        ShowProgress(isDowned, allyDowned ? _allyRecovery : null);
    }

    private bool IsWithinReviveRange(PlayerCharacter ally)
    {
        if (_allyRecovery == null)
        {
            return false;
        }

        float distance = Vector2.Distance(_character.transform.position, ally.transform.position);
        return distance <= _allyRecovery.ReviveRange;
    }

    private void ShowProgress(bool isDowned, PlayerDownedRecoveryNetworkController allyRecovery)
    {
        if (isDowned)
        {
            bool running = _ownRecovery.TryGetProgress(out float ownProgress);
            _view.PresentReviveProgress(running, ownProgress, false);
            return;
        }

        float progress = 0f;
        bool reviving = allyRecovery != null &&
            allyRecovery.TryGetProgress(out progress) &&
            allyRecovery.ReviverId == _character.Object.Id;
        _view.PresentReviveProgress(reviving, reviving ? progress : 0f, true);
    }

    private void RefreshNotices()
    {
        // A lethal hit during a session records the interruption and then Defeat; the Defeat overlay
        // owns that moment, so a notice is consumed but not shown once a participant is defeated.
        if (_ownOutcome.Observe(_ownRecovery.LastOutcomeSequence) && _character.IsAlive)
        {
            Announce(_ownRecovery.LastOutcomeKind, _ownRecovery.LastOutcomeReason);
        }

        if (_allyRecovery != null && _allyRecovery.Object != null && _allyRecovery.Object.IsValid &&
            _allyOutcome.Observe(_allyRecovery.LastOutcomeSequence) &&
            _allyRecovery.LastOutcomeReviverId == _character.Object.Id &&
            _allyCharacter != null && _allyCharacter.IsAlive)
        {
            Announce(_allyRecovery.LastOutcomeKind, _allyRecovery.LastOutcomeReason);
        }

        if (_messageUntil > 0f && Time.unscaledTime >= _messageUntil)
        {
            _messageUntil = 0f;
            _view.HideMessage();
        }
    }

    private void Announce(RecoveryOutcomeKind kind, DownedRecoveryInterruptReason reason)
    {
        string message = DownedRecoveryFeedbackRules.MessageFor(kind, reason);
        if (message == null)
        {
            return;
        }

        bool completed = kind == RecoveryOutcomeKind.Completed;
        _view.ShowMessage(message, completed);
        _messageUntil = Time.unscaledTime + _messageDuration;
        Play(completed ? _completedSound : _interruptedSound);
    }

    private void ClearAlly()
    {
        _allyCharacter = null;
        _allyRecovery = null;
        _allyOutcome.Reset();
    }

    private static void Play(in CustomClip clip)
    {
        if (clip.IsValid && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySfx(clip);
        }
    }
}
