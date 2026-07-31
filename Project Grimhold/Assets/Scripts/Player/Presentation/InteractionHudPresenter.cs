using Fusion;
using TMPro;
using UnityEngine;

/// <summary>
/// Presents the local predictive interaction prompt and confirmed interaction results.
/// It never queries or mutates gameplay directly.
/// </summary>
[DisallowMultipleComponent]
public sealed class InteractionHudPresenter : MonoBehaviour
{
    [SerializeField]
    private GameObject _promptRoot;

    [SerializeField]
    private TMP_Text _promptText;

    [SerializeField]
    private GameObject _feedbackRoot;

    [SerializeField]
    private TMP_Text _feedbackText;

    [SerializeField, Min(0f)]
    private float _feedbackDuration = 1.25f;

    [SerializeField, Min(0f)]
    private float _attemptPulseDuration = 0.15f;

    [SerializeField]
    private Color _rejectionColor = new(1f, 0.35f, 0.2f, 1f);

    private LocalInteractionCandidateSource _candidateSource;
    private PlayerInteractionNetworkController _interactionController;
    private NetworkRunner _runner;
    private int _lastConsumedSequence;
    private float _feedbackRemaining;
    private float _attemptPulseRemaining;
    private bool _isBound;
    private Color _promptBaseColor = Color.white;

    private void Awake()
    {
        if (_promptText != null)
        {
            _promptBaseColor = _promptText.color;
        }
    }

    public void Bind(
        LocalInteractionCandidateSource candidateSource,
        PlayerInteractionNetworkController interactionController,
        NetworkRunner runner)
    {
        Unbind();

        _candidateSource = candidateSource;
        _interactionController = interactionController;
        _runner = runner;
        if (_interactionController == null)
        {
            HideAll();
            return;
        }

        _lastConsumedSequence = _interactionController.CurrentInteractionSequence;
        _interactionController.InteractionResolved += OnInteractionResolved;
        _isBound = true;
        RefreshPrompt();
    }

    public void Unbind()
    {
        if (_isBound && _interactionController != null)
        {
            _interactionController.InteractionResolved -= OnInteractionResolved;
        }

        _candidateSource = null;
        _interactionController = null;
        _runner = null;
        _isBound = false;
        HideAll();
    }

    private void Update()
    {
        if (!_isBound)
        {
            return;
        }

        RefreshPrompt();

        if (_attemptPulseRemaining > 0f)
        {
            _attemptPulseRemaining -= Time.deltaTime;
            if (_promptText != null)
            {
                _promptText.color = Color.Lerp(
                    _promptBaseColor,
                    _rejectionColor,
                    Mathf.Clamp01(_attemptPulseRemaining / Mathf.Max(0.001f, _attemptPulseDuration)));
            }
        }
        else if (_promptText != null && _promptText.color != _promptBaseColor)
        {
            _promptText.color = _promptBaseColor;
        }

        if (_feedbackRemaining <= 0f)
        {
            return;
        }

        _feedbackRemaining -= Time.deltaTime;
        if (_feedbackRemaining <= 0f && _feedbackRoot != null)
        {
            _feedbackRoot.SetActive(false);
        }
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (interactionEvent.Sequence <= _lastConsumedSequence)
        {
            return;
        }

        _lastConsumedSequence = interactionEvent.Sequence;

        if (interactionEvent.Success && interactionEvent.IsConsumed)
        {
            HideFeedback();
            return;
        }

        if (!interactionEvent.Success &&
            interactionEvent.FailureReason == InteractionFailureReason.LootRejected &&
            interactionEvent.LootFailureReason == LootTransferFailureReason.InventoryFull)
        {
            TryPlayPickupRejection(interactionEvent.TargetId);
            HideFeedback();
            return;
        }

        if (!interactionEvent.Success && IsContextualRejection(interactionEvent.FailureReason))
        {
            _attemptPulseRemaining = _attemptPulseDuration;
            HideFeedback();
            return;
        }

        ShowFeedback(interactionEvent.Success
            ? "Interacción aceptada"
            : GetFailureMessage(interactionEvent.FailureReason));
    }

    private void RefreshPrompt()
    {
        bool hasCandidate = _candidateSource != null && _candidateSource.HasCandidate;
        bool showPrompt = hasCandidate || _attemptPulseRemaining > 0f;

        if (_promptRoot != null)
        {
            _promptRoot.SetActive(showPrompt);
        }

        if (_promptText != null && showPrompt)
        {
            string action = _candidateSource != null && !string.IsNullOrWhiteSpace(_candidateSource.CurrentPromptText)
                ? _candidateSource.CurrentPromptText
                : "Interactuar";
            _promptText.text = $"E — {action}";
        }
    }

    private void ShowFeedback(string message)
    {
        if (_feedbackText != null)
        {
            _feedbackText.text = message;
        }

        if (_feedbackRoot != null)
        {
            _feedbackRoot.SetActive(true);
        }

        _feedbackRemaining = _feedbackDuration;
    }

    private void HideFeedback()
    {
        _feedbackRemaining = 0f;
        if (_feedbackRoot != null)
        {
            _feedbackRoot.SetActive(false);
        }
    }

    private void HideAll()
    {
        _feedbackRemaining = 0f;
        _attemptPulseRemaining = 0f;

        if (_promptText != null)
        {
            _promptText.color = _promptBaseColor;
        }

        if (_promptRoot != null)
        {
            _promptRoot.SetActive(false);
        }

        HideFeedback();
    }

    private void TryPlayPickupRejection(EntityId targetId)
    {
        if (_runner == null || targetId.Value == 0)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)targetId.Value) };
        if (_runner.TryFindObject(networkId, out NetworkObject target) && target != null &&
            target.Id.Raw == networkId.Raw &&
            target.TryGetComponent(out LootPickupRejectionPresenter presenter))
        {
            presenter.PlayRejectedPickup();
        }
    }

    private static bool IsContextualRejection(InteractionFailureReason reason)
    {
        return reason == InteractionFailureReason.InvalidTarget ||
            reason == InteractionFailureReason.InteractionDisabled ||
            reason == InteractionFailureReason.InteractorUnavailable ||
            reason == InteractionFailureReason.TargetUnavailable ||
            reason == InteractionFailureReason.OutOfRange;
    }

    private static string GetFailureMessage(InteractionFailureReason reason)
    {
        return reason switch
        {
            InteractionFailureReason.InteractionDisabled => "No podés interactuar ahora",
            InteractionFailureReason.InteractorUnavailable => "No podés interactuar",
            InteractionFailureReason.OutOfRange => "Fuera de alcance",
            InteractionFailureReason.TargetUnavailable => "Objetivo no disponible",
            InteractionFailureReason.ReceiverNotFound => "No se pudo recibir el loot",
            InteractionFailureReason.LootRejected => "Loot rechazado",
            _ => "Nada para interactuar"
        };
    }
}
