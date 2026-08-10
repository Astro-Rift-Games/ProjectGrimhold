using Fusion;
using UnityEngine;

/// <summary>
/// Supplies identity and availability for the lightweight Town avatar.
/// It deliberately exposes no health, damage, loot, defeat or extraction capability.
/// </summary>
[DisallowMultipleComponent]
public sealed class SocialPlayerCharacter : NetworkBehaviour, ICharacter
{
    private EntityRegistry _registry;
    private Collider2D[] _colliders;
    private EntityId _registeredId;
    private bool _isRegistered;

    public new EntityId Id => new EntityId(unchecked((int)Object.Id.Raw));
    public bool IsAlive => Object != null && Object.IsValid;

    private void Awake()
    {
        _colliders = GetComponentsInChildren<Collider2D>(true);
    }

    public override void Spawned()
    {
        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(SocialPlayerCharacter)} requires {nameof(EntityRegistry)} on the Town runner.", this);
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterEntity(_registeredId, this, _colliders);
        if (!_isRegistered)
        {
            Debug.LogError($"{nameof(SocialPlayerCharacter)} could not register social entity '{_registeredId}'.", this);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        Unregister();
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void Unregister()
    {
        if (!_isRegistered || _registry == null)
        {
            return;
        }

        _registry.TryUnregisterEntity(_registeredId, this);
        _isRegistered = false;
        _registry = null;
    }
}
