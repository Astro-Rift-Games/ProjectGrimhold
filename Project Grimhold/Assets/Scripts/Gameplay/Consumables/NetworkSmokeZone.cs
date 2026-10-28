using Fusion;
using UnityEngine;

/// <summary>
/// Networked entity representing a persistent smoke cloud deployed by CON_SMOKE_BOMB (Doc 14 TASK-233).
/// Moves along a parabolic trajectory towards the target position, detonates upon arrival,
/// and impairs player line-of-sight and AI visual perception for 8.0 seconds across a 3.0m radius.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public sealed class NetworkSmokeZone : NetworkBehaviour, ISmokeZone
{
    [SerializeField, Min(0.1f)]
    private float _radius = 3.0f;

    [SerializeField, Min(0.1f)]
    private float _durationSeconds = 8.0f;

    [SerializeField, Min(0.1f)]
    private float _flightDuration = 0.5f;

    [SerializeField, Min(0f)]
    private float _arcHeight = 1.2f;

    [SerializeField]
    private SpriteRenderer _visualRenderer;

    [SerializeField]
    private ParticleSystem _smokeParticles;

    [SerializeField]
    private Transform _bombVisualRoot;

    [SerializeField]
    private SpriteRenderer _bombRenderer;

    [SerializeField]
    private Sprite _bombSprite;

    [SerializeField]
    private AudioClip _detonationSound;

    private ParticleSystem _runtimeParticles;

    [Networked]
    private Vector2 StartPosition { get; set; }

    [Networked]
    private Vector2 TargetPosition { get; set; }

    [Networked]
    private TickTimer FlightTimer { get; set; }

    [Networked]
    private TickTimer LifetimeTimer { get; set; }

    [Networked]
    private float NetworkedRadius { get; set; }

    [Networked]
    private NetworkBool HasDetonated { get; set; }

    [Networked]
    private NetworkBool IsThrown { get; set; }

    public Vector2 Position => transform.position;
    public float Radius => NetworkedRadius > 0f ? NetworkedRadius : _radius;
    public float DurationSeconds { get => _durationSeconds; set => _durationSeconds = value; }
    public float FlightDuration { get => _flightDuration; set => _flightDuration = value; }
    public float ArcHeight { get => _arcHeight; set => _arcHeight = value; }
    public bool IsActive => Object != null && Object.IsValid && HasDetonated && (!LifetimeTimer.IsRunning || !LifetimeTimer.Expired(Runner));

    public void InitializeThrow(Vector2 start, Vector2 target, float radius, float duration, float flightDuration = 0.5f)
    {
        if (!HasStateAuthority) return;

        _radius = radius;
        _durationSeconds = duration;
        _flightDuration = Mathf.Max(0.1f, flightDuration);
        NetworkedRadius = radius;
        StartPosition = start;
        TargetPosition = target;
        transform.position = start;
        IsThrown = true;
        HasDetonated = false;
        FlightTimer = TickTimer.CreateFromSeconds(Runner, _flightDuration);
    }

    internal void Configure(float radius, float durationSeconds, float flightDuration = 0.5f)
    {
        _radius = radius;
        _durationSeconds = durationSeconds;
        _flightDuration = Mathf.Max(0.1f, flightDuration);
        UpdateVisualScale();
        if (HasStateAuthority && Object != null && Object.IsValid)
        {
            NetworkedRadius = radius;
            if (HasDetonated || !IsThrown)
            {
                LifetimeTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
            }
        }
    }

    public override void Spawned()
    {
        EnsureVisualReferences();

        if (HasStateAuthority)
        {
            if (NetworkedRadius <= 0f) NetworkedRadius = _radius;
            if (!IsThrown)
            {
                Detonate();
            }
        }

        UpdateVisualScale();

        if (HasDetonated || !IsThrown)
        {
            SetBombVisualActive(false);
            SetSmokeVisualActive(true);
            SmokeZoneRegistry.Register(this);
            SpawnSmokeParticles();
        }
        else
        {
            SetBombVisualActive(true);
            SetSmokeVisualActive(false);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward) return;

        if (IsThrown && !HasDetonated)
        {
            if (FlightTimer.Expired(Runner))
            {
                Detonate();
            }
            else
            {
                float remaining = FlightTimer.RemainingTime(Runner) ?? 0f;
                float elapsed = Mathf.Max(0f, _flightDuration - remaining);
                float tau = _flightDuration > 0.001f ? Mathf.Clamp01(elapsed / _flightDuration) : 1f;
                transform.position = Vector2.Lerp(StartPosition, TargetPosition, tau);
            }
            return;
        }

        if (HasDetonated)
        {
            if (LifetimeTimer.Expired(Runner))
            {
                Runner.Despawn(Object);
            }
        }
    }

    public override void Render()
    {
        if (IsThrown && !HasDetonated)
        {
            if (_bombVisualRoot != null)
            {
                float remaining = FlightTimer.RemainingTime(Runner) ?? 0f;
                float elapsed = Mathf.Max(0f, _flightDuration - remaining);
                float tau = _flightDuration > 0.001f ? Mathf.Clamp01(elapsed / _flightDuration) : 1f;
                float arc = 4f * _arcHeight * tau * (1f - tau);
                _bombVisualRoot.localPosition = new Vector3(0f, arc, 0f);
            }
        }
        else
        {
            if (_bombVisualRoot != null && _bombVisualRoot.localPosition != Vector3.zero)
            {
                _bombVisualRoot.localPosition = Vector3.zero;
            }

            if (HasDetonated && _bombRenderer != null && _bombRenderer.enabled)
            {
                SetBombVisualActive(false);
                SetSmokeVisualActive(true);
                SpawnSmokeParticles();
            }
        }
    }

    private void Detonate()
    {
        HasDetonated = true;
        if (TargetPosition != Vector2.zero)
        {
            transform.position = TargetPosition;
        }
        if (_bombVisualRoot != null) _bombVisualRoot.localPosition = Vector3.zero;

        LifetimeTimer = TickTimer.CreateFromSeconds(Runner, _durationSeconds);

        SetBombVisualActive(false);
        SetSmokeVisualActive(true);
        SmokeZoneRegistry.Register(this);
        SpawnSmokeParticles();

        RPC_PlayDetonationEffects(transform.position);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayDetonationEffects(Vector2 position)
    {
        SetBombVisualActive(false);
        SetSmokeVisualActive(true);
        UpdateVisualScale();
        SpawnSmokeParticles();
        PlayDetonationPop(position);
    }

    private void PlayDetonationPop(Vector2 position)
    {
        if (_detonationSound != null)
        {
            AudioSource.PlayClipAtPoint(_detonationSound, position, 0.8f);
        }

        GameObject puffGo = new GameObject("VFX_SmokeDetonationBurst");
        puffGo.transform.position = position;
        puffGo.SetActive(false);

        ParticleSystem ps = puffGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new(0f, 16, 24) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.3f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new(new Color(0.8f, 0.8f, 0.85f), 0f), new(new Color(0.4f, 0.4f, 0.45f), 1f) },
            new GradientAlphaKey[] { new(0.6f, 0f), new(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        ParticleSystemRenderer renderer = puffGo.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 5;
        Material mat = Resources.Load<Material>("Materials/Default-Particle");
        if (mat == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null) mat = new Material(shader);
        }
        if (mat != null) renderer.material = mat;

        puffGo.SetActive(true);
        ps.Play();
        UnityEngine.Object.Destroy(puffGo, 1.5f);
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        SmokeZoneRegistry.Unregister(this);
        ReleaseSmokeParticles();
    }

    private void OnDestroy()
    {
        SmokeZoneRegistry.Unregister(this);
        ReleaseSmokeParticles();
    }

    private void EnsureVisualReferences()
    {
        if (_bombVisualRoot == null)
        {
            Transform bombChild = transform.Find("BombVisual");
            if (bombChild != null)
            {
                _bombVisualRoot = bombChild;
                _bombRenderer = bombChild.GetComponent<SpriteRenderer>();
            }
            else
            {
                GameObject bombGo = new GameObject("BombVisual");
                bombGo.transform.SetParent(transform, false);
                _bombVisualRoot = bombGo.transform;
                _bombRenderer = bombGo.AddComponent<SpriteRenderer>();
                _bombRenderer.sortingLayerName = "Characters";
                _bombRenderer.sortingOrder = 5;
                if (_bombSprite != null)
                {
                    _bombRenderer.sprite = _bombSprite;
                }
                _bombRenderer.color = new Color(0.8f, 0.85f, 0.9f, 1f);
            }
        }
    }

    private void SetBombVisualActive(bool active)
    {
        EnsureVisualReferences();
        if (_bombVisualRoot != null)
        {
            _bombVisualRoot.gameObject.SetActive(active);
        }
        if (_bombRenderer != null)
        {
            _bombRenderer.enabled = active;
        }
    }

    private void SetSmokeVisualActive(bool active)
    {
        if (_visualRenderer != null)
        {
            _visualRenderer.enabled = active;
        }
    }

    private void UpdateVisualScale()
    {
        if (_visualRenderer != null)
        {
            float diameter = Radius * 2f;
            _visualRenderer.transform.localScale = new Vector3(diameter, diameter, 1f);
        }
    }

    private void SpawnSmokeParticles()
    {
        if (_runtimeParticles != null) return;

        if (_smokeParticles != null)
        {
            _runtimeParticles = Instantiate(_smokeParticles, transform.position, Quaternion.identity, transform);
            _runtimeParticles.Play();
        }
        else
        {
            _runtimeParticles = CreateProceduralSmoke(transform, Radius);
        }
    }

    private void ReleaseSmokeParticles()
    {
        if (_runtimeParticles != null)
        {
            _runtimeParticles.transform.SetParent(null);
            var emission = _runtimeParticles.emission;
            emission.enabled = false;
            Destroy(_runtimeParticles.gameObject, 4.5f);
            _runtimeParticles = null;
        }
    }

    public static ParticleSystem CreateProceduralSmoke(Transform parent, float radius)
    {
        GameObject go = new GameObject("VFX_SmokeCloud");
        go.transform.position = parent != null ? parent.position : Vector3.zero;
        if (parent != null) go.transform.SetParent(parent);
        go.SetActive(false);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 1.0f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.0f, 4.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 16f;
        emission.SetBursts(new ParticleSystem.Burst[] { new(0f, 24, 32) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0.5f, radius * 0.7f);

        var colOverLifetime = ps.colorOverLifetime;
        colOverLifetime.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new(new Color(0.65f, 0.66f, 0.70f), 0f),
                new(new Color(0.55f, 0.56f, 0.60f), 0.5f),
                new(new Color(0.45f, 0.46f, 0.50f), 1f)
            },
            new GradientAlphaKey[]
            {
                new(0f, 0f),
                new(0.45f, 0.15f),
                new(0.40f, 0.7f),
                new(0f, 1f)
            }
        );
        colOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.6f);
        sizeCurve.AddKey(0.4f, 1.0f);
        sizeCurve.AddKey(1f, 1.35f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var rotOverLifetime = ps.rotationOverLifetime;
        rotOverLifetime.enabled = true;
        rotOverLifetime.z = new ParticleSystem.MinMaxCurve(-30f * Mathf.Deg2Rad, 30f * Mathf.Deg2Rad);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 4;
        Material mat = Resources.Load<Material>("Materials/Default-Particle");
        if (mat == null)
        {
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null) mat = new Material(shader);
        }
        if (mat != null) renderer.material = mat;

        go.SetActive(true);
        ps.Play();
        return ps;
    }

    public bool ContainsPoint(Vector2 point)
    {
        if (IsThrown && !HasDetonated) return false;
        return ((Vector2)transform.position - point).sqrMagnitude <= Radius * Radius;
    }

    public bool BlocksLine(Vector2 start, Vector2 end, float closeVisionDistance = 1.2f)
    {
        if (IsThrown && !HasDetonated) return false;
        return SmokeZoneRegistry.CircleBlocksLine(transform.position, Radius, start, end, closeVisionDistance);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
#endif
}
