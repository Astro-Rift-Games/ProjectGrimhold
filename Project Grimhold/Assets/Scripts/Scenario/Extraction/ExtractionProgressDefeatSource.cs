using Fusion;
using UnityEngine;

/// <summary>
/// Registers an entity's independently configured defeat reward for authoritative resolution.
/// It owns no health, combat, death, or progress state.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class ExtractionProgressDefeatSource : NetworkBehaviour, IExtractionProgressDefeatSource
{
    [SerializeField, Min(0)]
    private int _defeatProgressReward;

    private EntityRegistry _registry;
    private EntityId _registeredId;
    private bool _isRegistered;

    public int DefeatProgressReward => _defeatProgressReward;

    public new EntityId Id => Object != null && Object.IsValid
        ? new EntityId(unchecked((int)Object.Id.Raw))
        : default;

    public override void Spawned()
    {
        _registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        string error = null;
        if (_registry == null || !TryValidate(out error))
        {
            Debug.LogError($"{nameof(ExtractionProgressDefeatSource)} is invalid. {error}", this);
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterExtractionProgressDefeatSource(_registeredId, this);
        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(ExtractionProgressDefeatSource)} could not register entity {_registeredId}.", this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        UnregisterSource();
    }

    public bool TryValidate(out string error)
    {
        error = null;
        if (_defeatProgressReward < 0)
        {
            error = "Defeat progress reward must be non-negative.";
            return false;
        }

        return true;
    }

    private void UnregisterSource()
    {
        if (!_isRegistered)
        {
            return;
        }

        _registry?.TryUnregisterExtractionProgressDefeatSource(_registeredId, this);
        _registeredId = default;
        _isRegistered = false;
    }

    private void OnDestroy()
    {
        UnregisterSource();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _defeatProgressReward = Mathf.Max(0, _defeatProgressReward);
    }
#endif
}
