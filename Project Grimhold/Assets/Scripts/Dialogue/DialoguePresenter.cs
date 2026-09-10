using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Orchestrates dialogue presentation on the local player client.
/// Listens to confirmed interactions, resolves dialogue sequences (primary vs. secondary),
/// drives the <see cref="IDialogueController"/> and updates the <see cref="IDialogueView"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInteractionNetworkController))]
public sealed class DialoguePresenter : NetworkBehaviour
{
    [Header("Configuration")]
    [Tooltip("Maximum distance between player and NPC before dialogue automatically aborts.")]
    [SerializeField, Min(0.5f)] private float _maxDialogueDistance = 3.5f;

    [Header("UI Prefab (Optional - instantiated if View/Controller are not pre-assigned)")]
    [SerializeField] private GameObject _dialogueUiPrefab;

    [Header("Direct References (Optional)")]
    [SerializeField] private DialogueController _dialogueController;
    [SerializeField] private MonoBehaviour _dialogueViewBehaviour;

    [SerializeField] private PlayerInteractionNetworkController _interactionController;

    private readonly HashSet<EntityId> _interactedEntities = new();

    private IDialogueController _controller;
    private IDialogueView _view;
    private GameObject _instantiatedUiInstance;

    private NetworkObject _currentNpc;
    private IDisposable _inputSuppression;
    private PlayerInputReader _inputReader;
    private bool _isDialogueActive;

    public bool IsDialogueActive => _isDialogueActive;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        Bind();
    }

    private void OnEnable()
    {
        if (Object != null && Object.IsValid)
        {
            Bind();
        }
    }

    public override void Render()
    {
        if (!HasInputAuthority || !_isDialogueActive)
        {
            return;
        }

        // Safety checks: if NPC despawned or player moved too far away, abort
        if (Runner == null || !Runner.IsRunning || _currentNpc == null || !_currentNpc.IsValid)
        {
            ForceEndDialogue();
            return;
        }

        if (Vector3.Distance(transform.position, _currentNpc.transform.position) > _maxDialogueDistance)
        {
            ForceEndDialogue();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unbind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    private void OnDestroy()
    {
        Unbind();
    }

    public void Initialize(IDialogueController controller, IDialogueView view)
    {
        _controller = controller;
        _view = view;
    }

    private void CacheDependencies()
    {
        if (_interactionController == null)
        {
            _interactionController = GetComponent<PlayerInteractionNetworkController>();
        }

        EnsureControllerAndView();
    }

    private void EnsureControllerAndView()
    {
        if (_controller == null)
        {
            if (_dialogueController != null)
            {
                _controller = _dialogueController;
            }
            else
            {
                _controller = GetComponentInChildren<IDialogueController>(true);
            }
        }

        if (_view == null)
        {
            if (_dialogueViewBehaviour is IDialogueView v)
            {
                _view = v;
            }
            else
            {
                _view = GetComponentInChildren<IDialogueView>(true);
            }
        }

        // If still missing and a prefab is provided, instantiate it (only at runtime)
        if (Application.isPlaying && (_controller == null || _view == null) && _dialogueUiPrefab != null && _instantiatedUiInstance == null)
        {
            _instantiatedUiInstance = Instantiate(_dialogueUiPrefab, transform, false);
            _instantiatedUiInstance.name = "DialogueUI";

            Canvas canvas = _instantiatedUiInstance.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 105;
            }

            if (_controller == null)
            {
                _controller = _instantiatedUiInstance.GetComponentInChildren<IDialogueController>(true);
            }

            if (_view == null)
            {
                _view = _instantiatedUiInstance.GetComponentInChildren<IDialogueView>(true);
            }
        }
    }

    private void Bind()
    {
        if (!HasInputAuthority || _interactionController == null)
        {
            return;
        }

        EnsureControllerAndView();

        _interactionController.InteractionResolved -= OnInteractionResolved;
        _interactionController.InteractionResolved += OnInteractionResolved;
    }

    private void Unbind()
    {
        if (_interactionController != null)
        {
            _interactionController.InteractionResolved -= OnInteractionResolved;
        }

        ForceEndDialogue();

        if (_instantiatedUiInstance != null)
        {
            Destroy(_instantiatedUiInstance);
            _instantiatedUiInstance = null;
            _controller = null;
            _view = null;
        }
    }

    private void OnInteractionResolved(InteractionPresentationEvent interactionEvent)
    {
        if (!interactionEvent.Success || interactionEvent.TargetId.Value == 0 || Runner == null)
        {
            return;
        }

        var networkId = new NetworkId { Raw = unchecked((uint)interactionEvent.TargetId.Value) };
        if (!Runner.TryFindObject(networkId, out NetworkObject target) || target == null)
        {
            return;
        }

        var trigger = target.GetComponentInChildren<IDialogueTrigger>();
        if (trigger == null)
        {
            return;
        }

        if (_isDialogueActive)
        {
            // If already talking to this NPC, treat interact as advance
            if (_currentNpc == target && _controller != null)
            {
                _controller.Advance();
            }
            return;
        }

        StartDialogueWithTarget(target, trigger, interactionEvent.TargetId);
    }

    private void StartDialogueWithTarget(NetworkObject target, IDialogueTrigger trigger, EntityId targetId)
    {
        EnsureControllerAndView();

        if (_controller == null || _view == null)
        {
            Debug.LogError($"{nameof(DialoguePresenter)} cannot start dialogue: Controller or View is missing.", this);
            return;
        }

        bool alreadyInteracted = _interactedEntities.Contains(targetId);
        DialogueSequence sequence = (alreadyInteracted && trigger.SecondarySequence != null)
            ? trigger.SecondarySequence
            : trigger.PrimarySequence;

        if (sequence == null)
        {
            Debug.LogWarning($"[DialoguePresenter] No dialogue sequence available for target {target.name}.", this);
            return;
        }

        _interactedEntities.Add(targetId);
        _currentNpc = target;
        _isDialogueActive = true;

        AcquireInputSuppression();

        _controller.LineStarted += OnLineStarted;
        _controller.CharacterTyped += OnCharacterTyped;
        _controller.DialogueEnded += OnDialogueEnded;

        _controller.StartDialogue(sequence);
    }

    private void OnLineStarted(DialogueLine line, int currentLineIndex, int totalLineCount)
    {
        if (_view != null)
        {
            _view.Show(line.SpeakerName, line.SpeakerPortrait);
            _view.UpdateTypedText(string.Empty);
        }
    }

    private void OnCharacterTyped(string typedText)
    {
        if (_view != null)
        {
            _view.UpdateTypedText(typedText);
        }
    }

    private void OnDialogueEnded()
    {
        CloseDialogue();
    }

    private void OnAdvanceRequested()
    {
        if (_isDialogueActive && _controller != null)
        {
            _controller.Advance();
        }
    }

    private bool TryCloseFromInput()
    {
        if (!_isDialogueActive)
        {
            return false;
        }

        ForceEndDialogue();
        return true;
    }

    public void ForceEndDialogue()
    {
        if (_controller != null && _controller.IsActive)
        {
            _controller.ForceEnd();
        }

        CloseDialogue();
    }

    private void CloseDialogue()
    {
        if (!_isDialogueActive)
        {
            return;
        }

        _isDialogueActive = false;
        _currentNpc = null;

        if (_controller != null)
        {
            _controller.LineStarted -= OnLineStarted;
            _controller.CharacterTyped -= OnCharacterTyped;
            _controller.DialogueEnded -= OnDialogueEnded;
        }

        if (_view != null)
        {
            _view.Hide();
        }

        ReleaseInputSuppression();
    }

    private void AcquireInputSuppression()
    {
        if (_inputSuppression != null || Runner == null)
        {
            return;
        }

        LocalInputContext context = Runner.GetComponent<LocalInputContext>();
        if (context == null || context.Reader == null)
        {
            return;
        }

        _inputReader = context.Reader;
        _inputReader.InteractPressedLocally += OnAdvanceRequested;
        _inputReader.InventoryCloseRequested += TryCloseFromInput;
        _inputSuppression = _inputReader.AcquireGameplayInputSuppression();
    }

    private void ReleaseInputSuppression()
    {
        if (_inputReader != null)
        {
            _inputReader.InteractPressedLocally -= OnAdvanceRequested;
            _inputReader.InventoryCloseRequested -= TryCloseFromInput;
            _inputReader = null;
        }

        _inputSuppression?.Dispose();
        _inputSuppression = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
