using UnityEngine;
using Fusion;

/// <summary>
/// Component attached to a loot container entity to expose its mission progress rewards upon interaction.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class ContainerMissionProgressInteractionSource : NetworkBehaviour, IMissionProgressInteractionSource
{
    [SerializeField, Tooltip("The target ID matching the mission objective (e.g., 'chest_wooden').")]
    private string _targetId;

    [SerializeField, Tooltip("The zone ID where this container resides (e.g., 'crypt_f1').")]
    private string _zoneId;

    [SerializeField, Min(1), Tooltip("How much progress this container grants when interacted with.")]
    private int _interactionProgressAmount = 1;

    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public string TargetId => _targetId;
    public string ZoneId => _zoneId;
    public int InteractionProgressAmount => _interactionProgressAmount;

    public override void Spawned()
    {
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        if (_registry == null)
        {
            Debug.LogError($"{nameof(ContainerMissionProgressInteractionSource)} requires a runner registry.", this);
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterMissionProgressInteractionSource(_registeredId, this);
        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(ContainerMissionProgressInteractionSource)} could not register entity {_registeredId}.", this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterSource();
    }

    private void UnregisterSource()
    {
        if (!_isRegistered) return;
        _registry?.TryUnregisterMissionProgressInteractionSource(_registeredId, this);
        _registeredId = default;
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterSource();
    }
}
