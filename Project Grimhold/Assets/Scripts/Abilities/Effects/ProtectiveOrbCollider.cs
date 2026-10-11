using UnityEngine;

/// <summary>
/// Local geometry for one original orb slot. Never registers as a damageable entity.
/// Combat queries carry this exact collider to the avatar-owned authoritative effect.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class ProtectiveOrbCollider : MonoBehaviour
{
    private ProtectiveOrbsNetworkEffect _effect;
    private Collider2D _collider;
    private SpriteRenderer[] _renderers;

    public int SlotIndex { get; private set; }
    internal Collider2D PhysicsCollider => _collider;
    public EntityId OwnerId => _effect != null ? _effect.OwnerId : default;
    public bool IsActive => _effect != null && _effect.IsSlotActive(SlotIndex) &&
        gameObject.activeInHierarchy && _collider != null && _collider.enabled;

    internal void Initialize(ProtectiveOrbsNetworkEffect effect, int slotIndex)
    {
        _effect = effect;
        SlotIndex = slotIndex;
        _collider = GetComponent<Collider2D>();
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        SetSlotEnabled(false);
    }

    internal void SetSlotEnabled(bool active)
    {
        // Physics is removed synchronously, not deferred until Render or destruction.
        if (_collider != null && _collider.enabled != active) _collider.enabled = active;
        if (_renderers != null)
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i].enabled != active) _renderers[i].enabled = active;
    }

    public bool CanInterceptEnemyImpact(EntityId attackerId) =>
        IsActive && _effect.CanConsumeEnemyImpact(SlotIndex, attackerId);

    public bool TryConsumeEnemyImpact(EntityId attackerId) =>
        IsActive && _effect.TryConsumeEnemyImpact(SlotIndex, attackerId);

    public bool CanInterceptEnemyProjectileImpact(EntityId attackerId, bool sourceWasPvEEnemy) =>
        IsActive && _effect.CanConsumeEnemyProjectileImpact(SlotIndex, attackerId, sourceWasPvEEnemy);

    public bool TryConsumeEnemyProjectileImpact(EntityId attackerId, bool sourceWasPvEEnemy) =>
        IsActive && _effect.TryConsumeEnemyProjectileImpact(SlotIndex, attackerId, sourceWasPvEEnemy);
}
