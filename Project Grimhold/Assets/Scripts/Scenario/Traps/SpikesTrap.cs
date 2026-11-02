using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Trampa de pinchos de escenario.
///
/// Hereda la simulación de red y máquina de estados de <see cref="BaseTrap"/>.
/// Al activarse (fase <see cref="TrapState.Active"/>), realiza una consulta espacial
/// no asignativa sobre su área de impacto (<see cref="Collider2D"/>) e inflige daño
/// autoritativo a cada entidad dañable presente utilizando <see cref="IDamageResolver"/>
/// y el <see cref="EntityRegistry"/> de la sesión de Fusion.
/// </summary>
[DisallowMultipleComponent]
public sealed class SpikesTrap : BaseTrap
{
    [Header("Área de Impacto y Filtros")]
    [SerializeField] private Collider2D _impactCollider;
    [SerializeField] private LayerMask _targetLayerMask;
    [SerializeField] private int _maxTargetsBuffer = 32;

    [Header("Dependencias de Daño")]
    [SerializeField] private MonoBehaviour _damageResolverSource;

    private IDamageResolver _damageResolver;
    private EntityRegistry _registry;

    private Collider2D[] _colliderBuffer;
    private ContactFilter2D _contactFilter;
    private readonly List<EntityId> _processedTargets = new();
    private bool _reportedMissingArea;

    public override void Spawned()
    {
        base.Spawned();

        _colliderBuffer = new Collider2D[_maxTargetsBuffer];
        _contactFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = true
        };
        _contactFilter.SetLayerMask(_targetLayerMask);

        CacheDependencies();
    }

    private void CacheDependencies()
    {
        _registry = Runner.GetComponent<EntityRegistry>();
        if (_registry == null)
        {
            Debug.LogError($"{nameof(SpikesTrap)}: EntityRegistry component no fue encontrado en el NetworkRunner GameObject.", this);
        }

        if (_damageResolverSource != null)
        {
            _damageResolver = _damageResolverSource as IDamageResolver;
        }

        if (_damageResolver == null)
        {
            _damageResolver = GetComponent<IDamageResolver>() ?? GetComponentInChildren<IDamageResolver>() ?? GetComponentInParent<IDamageResolver>();
            if (_damageResolver == null)
            {
                _damageResolver = FindAnyObjectByType<DamageResolver>(FindObjectsInactive.Exclude);
            }
            if (_damageResolver is MonoBehaviour resolverMb)
            {
                _damageResolverSource = resolverMb;
            }
        }

        if (_damageResolver == null)
        {
            Debug.LogWarning($"{nameof(SpikesTrap)}: No se encontró una implementación de IDamageResolver.", this);
        }
    }

    /// <summary>Identidad estable del peligro para la atribución causal ambiental (Doc 19 §8.2).</summary>
    private EntityId HazardId => new EntityId(unchecked((int)Object.Id.Raw));

    /// <summary>
    /// Reporta cada tick la exposición de los jugadores en el área de impacto, en cualquier fase, para que
    /// un empuje hacia la trampa conserve su causalidad mientras el objetivo permanezca dentro.
    /// </summary>
    protected override void OnAuthorityTick()
    {
        if (_registry == null || !TryResolveAreaCollider(out Collider2D areaCollider)) return;

        int hitCount = OverlapTargets(areaCollider);
        EntityId hazardId = HazardId;
        for (int i = 0; i < hitCount; i++)
        {
            if (TryResolveTarget(_colliderBuffer[i], out _, out IDamageable damageable) &&
                damageable is PlayerCharacter player && player.HazardExposureCausality != null)
            {
                player.HazardExposureCausality.ReportHazardPresence(hazardId);
            }
        }

        System.Array.Clear(_colliderBuffer, 0, hitCount);
    }

    /// <summary>
    /// Invocado autoritativamente por State Authority cuando la trampa pasa a la fase Active.
    /// Detecta todas las entidades en la zona de impacto e inflige daño a través del DamageResolver.
    /// </summary>
    protected override void OnEnterActive()
    {
        base.OnEnterActive();
        ApplySpikeDamage();
    }

    /// <summary>
    /// Consulta el área de colisión del collider de la trampa sin realizar asignaciones de memoria,
    /// desduplica objetivos por EntityId y emite una solicitud de daño autoritativa para cada objetivo válido.
    /// </summary>
    private void ApplySpikeDamage()
    {
        if (_damageResolver == null || _registry == null || trapInfo == null) return;

        if (!TryResolveAreaCollider(out Collider2D areaCollider)) return;

        int hitCount = OverlapTargets(areaCollider);
        EntityId hazardId = HazardId;

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D col = _colliderBuffer[i];
            if (!TryResolveTarget(col, out EntityId targetId, out IDamageable damageable)) continue;
            if (!damageable.CanReceiveDamage) continue;

            Vector2 hitPoint = col.ClosestPoint(transform.position);

            // EntityId(0) identifica a la trampa/escenario como el origen de daño ambiental
            DamageRequest request = new DamageRequest(
                new EntityId(0),
                targetId,
                trapInfo.damage,
                trapInfo.DamageType,
                Vector2.up,
                hitPoint,
                Runner.Tick,
                environmentalHazardId: hazardId
            );

            _damageResolver.Resolve(in request);
        }

        System.Array.Clear(_colliderBuffer, 0, hitCount);
    }

    private bool TryResolveAreaCollider(out Collider2D areaCollider)
    {
        areaCollider = _impactCollider != null ? _impactCollider : GetComponent<Collider2D>();
        if (areaCollider != null) return true;

        if (!_reportedMissingArea)
        {
            Debug.LogWarning($"{nameof(SpikesTrap)}: Falta un Collider2D de impacto en {gameObject.name}.", this);
            _reportedMissingArea = true;
        }
        return false;
    }

    /// <summary>Consulta no asignativa del área; reinicia la desduplicación por EntityId de esta consulta.</summary>
    private int OverlapTargets(Collider2D areaCollider)
    {
        _contactFilter.SetLayerMask(_targetLayerMask);
        _processedTargets.Clear();
        return areaCollider.Overlap(_contactFilter, _colliderBuffer);
    }

    /// <summary>
    /// Resuelve el hitbox de daño registrado a una entidad viva no procesada en esta consulta, evitando
    /// búsquedas de componentes en runtime y múltiples colliders de una misma entidad.
    /// </summary>
    private bool TryResolveTarget(Collider2D col, out EntityId targetId, out IDamageable damageable)
    {
        damageable = null;
        targetId = default;
        if (col == null || !_registry.TryGetEntityId(col, out targetId) || targetId.Value == 0 ||
            !_registry.IsDamageCollider(targetId, col) || _processedTargets.Contains(targetId))
        {
            return false;
        }

        _processedTargets.Add(targetId);
        return _registry.TryGetDamageable(targetId, out damageable) &&
            (damageable is not ICharacter character || character.IsAlive);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_damageResolverSource != null && _damageResolverSource is not IDamageResolver)
        {
            Debug.LogWarning($"{nameof(SpikesTrap)}: _damageResolverSource no implementa IDamageResolver.", this);
        }
    }
#endif
}
