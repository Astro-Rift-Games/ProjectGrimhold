using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Network component responsible for processing player interaction intentions.
/// Delegates targets validation and execution to the pure logical resolver.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInteractionNetworkController : NetworkBehaviour
{
    [Header("Dependencies")]
    [SerializeField]
    private PlayerInteractionConfig _config;

    [SerializeField]
    private MonoBehaviour _characterSource;

    [SerializeField]
    private MonoBehaviour _querySource;

    [SerializeField]
    private PlayerMovementNetworkController _movementController;

    [SerializeField]
    private Transform _interactionOrigin;

    private ICharacter _character;
    private IInteractionTargetQuery _query;
    private EntityRegistry _registry;
    private PlayerExtractionController _extractionController;
    private NetworkMatchController _matchController;
    private bool _dependenciesValid;
    private readonly Queue<InteractionPresentationEvent> _pendingPresentationEvents = new();

    [Networked]
    private NetworkButtons PreviousButtons { get; set; }

    [Networked]
    private int InteractionSequence { get; set; }

    [Networked]
    private int LastInteractionTargetIdValue { get; set; }

    [Networked]
    private int LastInteractionTick { get; set; }

    [Networked]
    private NetworkBool LastInteractionSucceeded { get; set; }

    [Networked]
    private NetworkBool LastInteractionConsumed { get; set; }

    [Networked]
    private int LastInteractionFailureReasonValue { get; set; }

    /// <summary>
    /// Gets the latest authoritative interaction attempt sequence.
    /// Presentation uses it only to establish a non-replaying baseline.
    /// </summary>
    public int CurrentInteractionSequence => InteractionSequence;

    /// <summary>
    /// Local event raised during Render for each authoritative interaction result
    /// delivered to this object's Input Authority.
    /// </summary>
    public event Action<InteractionPresentationEvent> InteractionResolved;

    private void Awake()
    {
        CacheDependencies();
    }

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();

        _registry = Runner.GetComponent<EntityRegistry>();
        _matchController = Runner.GetComponent<NetworkMatchController>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: EntityRegistry was not found on the NetworkRunner.", this);
            _dependenciesValid = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
        {
            return;
        }

        if (!_dependenciesValid)
        {
            return;
        }

        if (_matchController != null &&
            _matchController.Phase != NetworkMatchController.MatchPhase.InProgress)
        {
            PreviousButtons = default;
            return;
        }

        if (!GetInput(out PlayerNetworkInput input))
        {
            return;
        }

        NetworkButtons currentButtons = input.Buttons;
        bool interactPressed = currentButtons.WasPressed(PreviousButtons, PlayerInputButton.Interact);

        PreviousButtons = currentButtons;

        if (!interactPressed)
        {
            return;
        }

        if (_character == null || !_character.IsAlive || (_extractionController != null && _extractionController.State == ExtractionState.Extracted))
        {
            RecordInteractionResult(default, Runner.Tick, InteractionResult.Rejected(InteractionFailureReason.InteractorUnavailable));
            return;
        }

        if (_movementController == null || !_movementController.IsControlEnabled)
        {
            RecordInteractionResult(default, Runner.Tick, InteractionResult.Rejected(InteractionFailureReason.InteractionDisabled));
            return;
        }

        TryProcessInteraction();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _pendingPresentationEvents.Clear();
    }

    private void TryProcessInteraction()
    {
        Vector2 originPos = _interactionOrigin != null ? (Vector2)_interactionOrigin.position : (Vector2)transform.position;

        InteractionTargetQuery targetQuery = new InteractionTargetQuery(
            _character.Id,
            originPos,
            _config.MaximumDistance,
            _config.TargetLayerMask
        );

        var candidates = _query.FindTargets(in targetQuery);

        bool executed = InteractionResolver.TryResolve(
            _character.Id,
            Runner.Tick,
            _config.MaximumDistance,
            candidates,
            _registry.TryGetInteractable,
            out var resolvedRequest,
            out var resolvedResult
        );

        if (!executed)
        {
            RecordInteractionResult(default, Runner.Tick, InteractionResult.Rejected(InteractionFailureReason.InvalidTarget));
            return;
        }

        RecordInteractionResult(resolvedRequest.TargetId, resolvedRequest.SimulationTick, resolvedResult);
    }

    public override void Render()
    {
        if (!HasInputAuthority)
        {
            _pendingPresentationEvents.Clear();
            return;
        }

        // Dispatch only the events that existed when this render pass began. This keeps
        // presentation callbacks from extending the same dispatch pass re-entrantly.
        int pendingCount = _pendingPresentationEvents.Count;
        for (int index = 0; index < pendingCount; index++)
        {
            InteractionResolved?.Invoke(_pendingPresentationEvents.Dequeue());
        }
    }

    private void RecordInteractionResult(EntityId targetId, int simulationTick, in InteractionResult result)
    {
        InteractionSequence++;
        LastInteractionTargetIdValue = targetId.Value;
        LastInteractionTick = simulationTick;
        LastInteractionSucceeded = result.Success;
        LastInteractionConsumed = result.IsConsumed;
        LastInteractionFailureReasonValue = (int)result.FailureReason;

        if (InteractionResultDeliveryPolicy.ShouldEnqueueLocally(HasStateAuthority, HasInputAuthority))
        {
            EnqueueInteractionResult(
                InteractionSequence,
                targetId.Value,
                simulationTick,
                result.Success,
                result.IsConsumed,
                (int)result.FailureReason,
                (int)result.LootFailureReason);
            return;
        }

        if (InteractionResultDeliveryPolicy.ShouldSendRemote(HasStateAuthority, HasInputAuthority))
        {
            RPC_ReceiveInteractionResult(
                InteractionSequence,
                targetId.Value,
                simulationTick,
                result.Success,
                result.IsConsumed,
                (int)result.FailureReason,
                (int)result.LootFailureReason);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority, InvokeLocal = false)]
    private void RPC_ReceiveInteractionResult(
        int sequence,
        int targetIdValue,
        int simulationTick,
        bool succeeded,
        bool consumed,
        int failureReasonValue,
        int lootFailureReasonValue)
    {
        EnqueueInteractionResult(
            sequence,
            targetIdValue,
            simulationTick,
            succeeded,
            consumed,
            failureReasonValue,
            lootFailureReasonValue);
    }

    private void EnqueueInteractionResult(
        int sequence,
        int targetIdValue,
        int simulationTick,
        bool succeeded,
        bool consumed,
        int failureReasonValue,
        int lootFailureReasonValue)
    {
        if (!HasInputAuthority)
        {
            return;
        }

        _pendingPresentationEvents.Enqueue(new InteractionPresentationEvent(
            sequence,
            _character != null ? _character.Id : default,
            new EntityId(targetIdValue),
            simulationTick,
            succeeded,
            consumed,
            (InteractionFailureReason)failureReasonValue,
            (LootTransferFailureReason)lootFailureReasonValue));
    }

    private void CacheDependencies()
    {
        if (_characterSource != null)
        {
            _character = _characterSource as ICharacter;
        }
        else
        {
            _character = GetComponent<ICharacter>();
        }

        if (_querySource != null)
        {
            _query = _querySource as IInteractionTargetQuery;
        }
        else
        {
            _query = GetComponent<IInteractionTargetQuery>();
        }

        if (_movementController == null)
        {
            _movementController = GetComponent<PlayerMovementNetworkController>();
        }

        if (_interactionOrigin == null)
        {
            _interactionOrigin = transform;
        }

        if (_extractionController == null)
        {
            _extractionController = GetComponent<PlayerExtractionController>();
        }
    }

    private bool ValidateDependencies()
    {
        if (_config == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: Configuration asset is not assigned.", this);
            return false;
        }

        if (_character == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: No component implementing {nameof(ICharacter)} is assigned.", this);
            return false;
        }

        if (_query == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: No component implementing {nameof(IInteractionTargetQuery)} is assigned.", this);
            return false;
        }

        if (_movementController == null)
        {
            Debug.LogError($"{nameof(PlayerInteractionNetworkController)}: PlayerMovementNetworkController is not assigned.", this);
            return false;
        }

        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        CacheDependencies();
    }
#endif
}
