using System.Collections.Generic;
using Fusion;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Development-only scene harness for exercising container transfers without production UI.
/// The component remains loadable in release builds but performs no input or discovery there.
/// </summary>
[DisallowMultipleComponent]
public sealed class LootContainerTransferDebugHarness : MonoBehaviour
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField]
    private NetworkRunner _runner;

    [SerializeField, Min(0.1f)]
    private float _detectionRadius = 3f;

    [SerializeField]
    private LayerMask _lootLayerMask = 1 << 8;

    private readonly Collider2D[] _colliderBuffer = new Collider2D[32];
    private PlayerLootTransferNetworkController _controller;
    private PlayerLootReceiver _receiver;

    private void Update()
    {
        if (!TryResolveController() || Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.f8Key.wasPressedThisFrame)
        {
            TryRequestNearest();
        }

        if (Keyboard.current.f9Key.wasPressedThisFrame)
        {
            TrySendDuplicate();
        }

        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            TrySendConflict();
        }

        if (Keyboard.current.f11Key.wasPressedThisFrame)
        {
            TrySendBusySequence();
        }

        if (Keyboard.current.f12Key.wasPressedThisFrame)
        {
            TryToggleAvailability();
        }
    }

    private bool TryResolveController()
    {
        if (_controller != null)
        {
            return true;
        }

        if (_runner == null)
        {
            _runner = FindAnyObjectByType<NetworkRunner>();
        }

        if (_runner == null || !_runner.TryGetPlayerObject(_runner.LocalPlayer, out NetworkObject playerObject))
        {
            return false;
        }

        _controller = playerObject.GetComponent<PlayerLootTransferNetworkController>();
        _receiver = playerObject.GetComponent<PlayerLootReceiver>();
        if (_controller != null && _receiver != null)
        {
            _controller.TransferConfirmed += LogConfirmation;
        }

        return _controller != null && _receiver != null;
    }

    private void OnDestroy()
    {
        if (_controller != null)
        {
            _controller.TransferConfirmed -= LogConfirmation;
        }
    }

    private void TryRequestNearest()
    {
        if (!TryFindNearestContainer(out NetworkLootContainer container) ||
            !container.TryGetLootContent(out IReadOnlyList<LootEntry> content) || content.Count == 0)
        {
            Debug.LogWarning($"{nameof(LootContainerTransferDebugHarness)}: No nearby container stack was found.", this);
            return;
        }

        bool sent = _controller.TryRequestTransfer(
            container.Id,
            _receiver.Id,
            content[0].LootId,
            LootTransferQuantityMode.FullStack);
        Debug.Log($"{nameof(LootContainerTransferDebugHarness)}: Public request sent={sent}, source={container.Id}, loot={content[0].LootId}.", this);
    }

    private void TrySendDuplicate()
    {
        if (_controller.DebugTryGetInFlightIdentity(out LootTransferRequestIdentity identity))
        {
            _controller.DebugSendRawRequest(
                identity.SourceId,
                identity.DestinationId,
                identity.CatalogIndex,
                identity.QuantityMode,
                identity.RequestSequence);
        }
    }

    private void TrySendConflict()
    {
        if (_controller.DebugTryGetInFlightIdentity(out LootTransferRequestIdentity identity))
        {
            _controller.DebugSendRawRequest(
                identity.SourceId,
                identity.DestinationId,
                identity.CatalogIndex + 1,
                identity.QuantityMode,
                identity.RequestSequence);
        }
    }

    private void TrySendBusySequence()
    {
        if (_controller.DebugTryGetInFlightIdentity(out LootTransferRequestIdentity identity))
        {
            _controller.DebugSendRawRequest(
                identity.SourceId,
                identity.DestinationId,
                identity.CatalogIndex,
                identity.QuantityMode,
                unchecked(identity.RequestSequence + 1));
        }
    }

    private void TryToggleAvailability()
    {
        if (!TryFindNearestContainer(out NetworkLootContainer container))
        {
            Debug.LogWarning($"{nameof(LootContainerTransferDebugHarness)}: No nearby container was found.", this);
            return;
        }

        bool queued = container.DebugTryQueueAvailability(!container.IsAvailable);
        Debug.Log(
            $"{nameof(LootContainerTransferDebugHarness)}: Authoritative availability toggle queued={queued}, source={container.Id}.",
            this);
    }

    private bool TryFindNearestContainer(out NetworkLootContainer nearest)
    {
        nearest = null;
        float nearestSqrDistance = float.MaxValue;
        var filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = _lootLayerMask,
            useTriggers = true
        };
        Vector2 origin = _controller.transform.position;
        int count = Physics2D.OverlapCircle(
            origin,
            _detectionRadius,
            filter,
            _colliderBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D collider = _colliderBuffer[i];
            NetworkLootContainer candidate = collider != null
                ? collider.GetComponentInParent<NetworkLootContainer>()
                : null;
            if (candidate == null)
            {
                continue;
            }

            float sqrDistance = ((Vector2)candidate.transform.position - origin).sqrMagnitude;
            if (sqrDistance < nearestSqrDistance)
            {
                nearestSqrDistance = sqrDistance;
                nearest = candidate;
            }
        }

        return nearest != null;
    }

    private void LogConfirmation(LootTransferConfirmation confirmation)
    {
        Debug.Log(
            $"Loot confirmation seq={confirmation.RequestSequence}, source={confirmation.SourceId}, " +
            $"index={confirmation.CatalogIndex}, success={confirmation.Result.Success}, " +
            $"amount={confirmation.Result.TransferredAmount}, failure={confirmation.Result.FailureReason}, tick={confirmation.SimulationTick}.",
            this);
    }
#else
    private void Awake()
    {
        enabled = false;
    }
#endif
}
