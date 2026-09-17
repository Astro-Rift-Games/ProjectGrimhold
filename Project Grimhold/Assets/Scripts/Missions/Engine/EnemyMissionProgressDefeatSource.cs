using UnityEngine;
using Fusion;

/// <summary>
/// Component attached to an enemy entity to expose its mission progress rewards upon defeat.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class EnemyMissionProgressDefeatSource : NetworkBehaviour, IMissionProgressDefeatSource
{
    [SerializeField, Tooltip("The target ID matching the mission objective (e.g., 'skeleton').")]
    private string _targetId;

    [SerializeField, Tooltip("The zone ID where this enemy resides (e.g., 'crypt_f1').")]
    private string _zoneId;

    [SerializeField, Min(1), Tooltip("How much progress this enemy grants when defeated.")]
    private int _defeatProgressAmount = 1;

    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public string TargetId => _targetId;
    public string ZoneId => _zoneId;
    public int DefeatProgressAmount => _defeatProgressAmount;

    public override void Spawned()
    {
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        if (_registry == null)
        {
            Debug.LogError($"{nameof(EnemyMissionProgressDefeatSource)} requires a runner registry.", this);
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterMissionProgressDefeatSource(_registeredId, this);
        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(EnemyMissionProgressDefeatSource)} could not register entity {_registeredId}.", this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterSource();
    }

    private void UnregisterSource()
    {
        if (!_isRegistered) return;
        _registry?.TryUnregisterMissionProgressDefeatSource(_registeredId, this);
        _registeredId = default;
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterSource();
    }
}
