using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Networked explosive device thrown via CON_EXPLOSIVE_BOMB (Doc 14 TASK-233).
/// Moves along a parabolic trajectory towards the target position, detonates 1.5 seconds after launch,
/// and deals 50 neutral base damage in a 2.5m radius.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public sealed class NetworkExplosiveBomb : NetworkBehaviour
{
    [SerializeField, Min(0f)]
    private float _damage = 50f;

    [SerializeField, Min(0.1f)]
    private float _radius = 2.5f;

    [SerializeField, Min(0.1f)]
    private float _fuseSeconds = 1.5f;

    [SerializeField, Min(0.1f)]
    private float _flightDuration = 0.75f;

    [SerializeField, Min(0f)]
    private float _arcHeight = 1.2f;

    [SerializeField]
    private LayerMask _impactLayerMask;

    [SerializeField]
    private Transform _visualRoot;

    [SerializeField]
    private ParticleSystem _explosionParticles;

    [SerializeField]
    private AudioClip _explosionSound;

    private IDamageResolver _damageResolver;

    [Networked]
    private Vector2 StartPosition { get; set; }

    [Networked]
    private Vector2 TargetPosition { get; set; }

    [Networked]
    private TickTimer FuseTimer { get; set; }

    [Networked]
    private int OwnerEntityIdValue { get; set; }

    [Networked]
    private NetworkBool HasDetonated { get; set; }

    public float Damage { get => _damage; set => _damage = value; }
    public float Radius { get => _radius; set => _radius = value; }
    public float FuseSeconds { get => _fuseSeconds; set => _fuseSeconds = value; }
    public bool IsDetonated => HasDetonated;

    public void InitializeNetworkState(EntityId ownerId, Vector2 start, Vector2 target, IDamageResolver resolver = null)
    {
        if (!HasStateAuthority) return;

        OwnerEntityIdValue = ownerId.Value;
        StartPosition = start;
        TargetPosition = target;
        FuseTimer = TickTimer.CreateFromSeconds(Runner, _fuseSeconds);
        HasDetonated = false;
        _damageResolver = resolver;
    }

    public void SetDamageResolver(IDamageResolver resolver)
    {
        _damageResolver = resolver;
    }

    public override void Spawned()
    {
        if (_damageResolver == null)
        {
            _damageResolver = GetComponent<IDamageResolver>() ?? GetComponentInChildren<IDamageResolver>();
        }

        if (_impactLayerMask.value == 0)
        {
            _impactLayerMask = (1 << 7) | (1 << 10) | (1 << 11); // Character, Enemies, Obstacles
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward) return;

        if (HasDetonated) return;

        if (FuseTimer.Expired(Runner))
        {
            Detonate();
            return;
        }

        // Parabolic trajectory towards TargetPosition
        float remaining = FuseTimer.RemainingTime(Runner) ?? 0f;
        float elapsed = Mathf.Max(0f, _fuseSeconds - remaining);
        float tau = Mathf.Clamp01(elapsed / _flightDuration);

        Vector2 currentPlanePos = Vector2.Lerp(StartPosition, TargetPosition, tau);
        transform.position = currentPlanePos;
    }

    public override void Render()
    {
        if (_visualRoot == null) return;

        if (HasDetonated)
        {
            _visualRoot.localPosition = Vector3.zero;
            return;
        }

        float remaining = FuseTimer.RemainingTime(Runner) ?? 0f;
        float elapsed = Mathf.Max(0f, _fuseSeconds - remaining);
        float tau = Mathf.Clamp01(elapsed / _flightDuration);

        float arc = 4f * _arcHeight * tau * (1f - tau);
        _visualRoot.localPosition = new Vector3(0f, arc, 0f);
    }

    private void Detonate()
    {
        HasDetonated = true;
        transform.position = TargetPosition;
        if (_visualRoot != null) _visualRoot.localPosition = Vector3.zero;

        EntityRegistry registry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(TargetPosition, _radius, _impactLayerMask);

        HashSet<EntityId> hitTargets = new();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null) continue;

            EntityId targetId = default;
            if (registry != null)
            {
                registry.TryGetEntityId(col, out targetId);
            }

            if (targetId.Value == 0 && col.TryGetComponent(out IDamageable colDamageable))
            {
                targetId = colDamageable.Id;
            }

            if (targetId.Value == 0 || targetId.Value == OwnerEntityIdValue) continue;
            if (!hitTargets.Add(targetId)) continue;

            IDamageable damageable = null;
            if (registry != null)
            {
                registry.TryGetDamageable(targetId, out damageable);
            }
            if (damageable == null)
            {
                col.TryGetComponent(out damageable);
            }

            if (damageable != null && damageable.CanReceiveDamage)
            {
                Vector2 targetPos = col.transform.position;
                Vector2 dir = (targetPos - TargetPosition).normalized;
                DamageRequest request = new DamageRequest(
                    new EntityId(OwnerEntityIdValue),
                    targetId,
                    _damage,
                    DamageType.TrueDamage,
                    dir,
                    targetPos,
                    Runner.Tick
                );

                if (_damageResolver != null)
                {
                    _damageResolver.Resolve(in request);
                }
                else
                {
                    // Fail closed: bypassing the resolver would bypass friendly-fire policy.
                    Debug.LogError($"{nameof(NetworkExplosiveBomb)} requires an {nameof(IDamageResolver)} to apply damage.", this);
                }
            }
        }

        RPC_PlayExplosionEffects(TargetPosition, _radius);

        Runner.Despawn(Object);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayExplosionEffects(Vector2 position, float radius)
    {
        PlayFireParticles(position, radius);

        if (_explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(_explosionSound, position);
        }
    }

    private void PlayFireParticles(Vector2 position, float radius)
    {
        if (_explosionParticles != null)
        {
            ParticleSystem ps = Instantiate(_explosionParticles, position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax + 0.5f);
            return;
        }

        SpawnProceduralFireExplosion(position, radius);
    }

    public static void SpawnProceduralFireExplosion(Vector2 position, float radius)
    {
        GameObject go = new GameObject("VFX_ExplosiveBomb_Fire");
        go.transform.position = position;
        go.SetActive(false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, Mathf.Max(2f, radius * 2.2f));
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new(0f, 40, 55) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.2f, radius * 0.35f);

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new(new Color(1f, 0.95f, 0.25f), 0f),
                new(new Color(1f, 0.45f, 0.05f), 0.35f),
                new(new Color(0.85f, 0.15f, 0.02f), 0.7f),
                new(new Color(0.2f, 0.15f, 0.15f), 1.0f)
            },
            new GradientAlphaKey[]
            {
                new(1.0f, 0f),
                new(0.9f, 0.4f),
                new(0.4f, 0.75f),
                new(0f, 1.0f)
            }
        );
        colOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.25f, 1.0f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 10;
        Material mat = Resources.Load<Material>("Materials/Default-Particle");
        if (mat == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null) mat = new Material(shader);
        }
        if (mat != null) renderer.material = mat;

        go.SetActive(true);
        ps.Play();
        UnityEngine.Object.Destroy(go, 2.0f);
    }

    public void Configure(float damage, float radius, float fuseSeconds)
    {
        _damage = damage;
        _radius = radius;
        _fuseSeconds = fuseSeconds;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
#endif
}
