using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

/// <summary>
/// Controls global ambient lighting, camera-shake, post-process vignette, and ceiling debris particles for dungeon pressure phases.
/// Observes the replicated phase on <see cref="DungeonPressureController"/> and smoothly modulates
/// the scene's global light, local <see cref="DungeonLightProp"/> components, native URP Volume vignette, and falling debris particles.
/// Handles late-joining players, host migration, and offline preview cleanly without requiring networked state.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DungeonPressureAmbientPresenter : MonoBehaviour
{
    [Header("Dependencies")]
    [Tooltip("The pressure controller to monitor. If null, automatically resolves from this GameObject or the active scene.")]
    [SerializeField] private DungeonPressureController _pressureController;

    [Tooltip("The scene's global 2D light. If null, automatically located in the scene.")]
    [SerializeField] private Light2D _globalLight;

    [Tooltip("The URP Volume holding the post-process Vignette. If null, automatically located in the scene.")]
    [SerializeField] private Volume _volume;

    [Header("Ambient Dust Particles")]
    [Tooltip("ParticleSystem emitting ambient floating dust motes over the camera viewport.")]
    [SerializeField] private ParticleSystem _dustParticles;

    [Tooltip("Whether to follow the active camera position so particles cover the viewport everywhere in the dungeon.")]
    [SerializeField] private bool _followCamera = true;

    [Tooltip("Area size (width x height) around the camera where ambient dust particles are distributed.")]
    [SerializeField] private Vector2 _particleAreaSize = new Vector2(26f, 16f);

    [Header("Particle Occlusion & Open Space Filtering")]
    [Tooltip("If true, particles overlapping walls, obstacles, or exterior void defined by tilemaps are culled so they only exist in open spaces.")]
    [SerializeField] private bool _cullOccludedParticles = true;

    [Tooltip("Sorting layer name for ambient dust particles. Defaults to 'VFX' so dust floats in the room above the floor.")]
    [SerializeField] private string _particleSortingLayerName = "VFX";

    [Tooltip("Sorting order for ambient dust particles.")]
    [SerializeField] private int _particleSortingOrder = 10;

    [Header("Configuration")]
    [Tooltip("Configuration asset defining global ambient lighting, screen shake, vignette, and debris per phase.")]
    [SerializeField] private DungeonAmbientConfig _config;

    private DungeonPressurePhase _lastPhase = DungeonPressurePhase.Normal;

    private Color _currentGlobalColor = Color.white;
    private float _currentGlobalIntensity = 1f;
    private Color _fromGlobalColor = Color.white;
    private float _fromGlobalIntensity = 1f;
    private Color _targetGlobalColor = Color.white;
    private float _targetGlobalIntensity = 1f;

    private float _globalTransitionDuration;
    private float _globalTransitionProgress = 1f;
    private float _periodicShakeTimer;
    private bool _hasResolvedInitialPhase;

    // Post-Process Vignette (URP Volume)
    private Vignette _vignette;
    private Color _currentVignetteColor = Color.black;
    private float _currentVignetteIntensity;
    private float _currentVignetteSmoothness = 0.4f;
    private Color _fromVignetteColor = Color.black;
    private float _fromVignetteIntensity;
    private float _fromVignetteSmoothness = 0.4f;
    private Color _targetVignetteColor = Color.black;
    private float _targetVignetteIntensity;
    private float _targetVignetteSmoothness = 0.4f;
    private bool _targetVignetteRounded;
    private float _vignetteTransitionDuration;
    private float _vignetteTransitionProgress = 1f;

    private Tilemap[] _wallTilemaps;
    private Tilemap[] _floorTilemaps;
    private Collider2D[] _customOcclusionColliders;
    private ParticleSystem.Particle[] _particlesBuffer;
    private static Texture2D _circleParticleTexture;
    private static Material _circleParticleMaterial;

    public DungeonPressurePhase CurrentPhase => _lastPhase;
    public Light2D GlobalLight => _globalLight;
    public Volume AmbientVolume => _volume;
    public Vignette VignetteComponent => _vignette;
    public ParticleSystem DustParticles => _dustParticles;
    [System.Obsolete("Debris particle system removed; only dust particles are used.")]
    public ParticleSystem DebrisParticles => null;
    public DungeonAmbientConfig Config => _config;
    public DungeonPressureController PressureController => _pressureController;

    public Color CurrentVignetteColor => _currentVignetteColor;
    public float CurrentVignetteIntensity => _currentVignetteIntensity;
    public float CurrentVignetteSmoothness => _currentVignetteSmoothness;
    public bool CurrentVignetteRounded => _targetVignetteRounded;

    private void Awake()
    {
        InitializeComponents();
    }

    private void OnEnable()
    {
        InitializeComponents();
        if (!_hasResolvedInitialPhase)
        {
            DungeonPressurePhase initialPhase = _pressureController != null ? _pressureController.Phase : _lastPhase;
            ApplyInitialPhase(initialPhase);
        }
    }

    private void Start()
    {
        if (!_hasResolvedInitialPhase)
        {
            DungeonPressurePhase initialPhase = _pressureController != null ? _pressureController.Phase : _lastPhase;
            ApplyInitialPhase(initialPhase);
        }
    }

    private void OnDisable()
    {
        ResetPresentation();
    }

    private void OnDestroy()
    {
        ResetPresentation();
    }

    public void Configure(
        DungeonPressureController controller,
        DungeonAmbientConfig config,
        Light2D globalLight = null,
        Volume volume = null,
        ParticleSystem dustParticles = null,
        ParticleSystem debrisParticles = null)
    {
        _pressureController = controller;
        _config = config;
        if (globalLight != null)
        {
            _globalLight = globalLight;
        }
        if (volume != null)
        {
            _volume = volume;
        }
        if (dustParticles != null)
        {
            _dustParticles = dustParticles;
        }
        if (debrisParticles != null)
        {
            debrisParticles.gameObject.SetActive(false);
        }

        InitializeComponents();
        DungeonPressurePhase initialPhase = _pressureController != null ? _pressureController.Phase : _lastPhase;
        ApplyInitialPhase(initialPhase);
    }

    private void InitializeComponents()
    {
        if (_pressureController == null)
        {
            _pressureController = GetComponent<DungeonPressureController>();
            if (_pressureController == null)
            {
                _pressureController = FindObjectOfType<DungeonPressureController>();
            }
        }

        if (_globalLight == null)
        {
            LocateGlobalLight();
        }

        if (_volume == null)
        {
            LocateVolume();
        }
        else
        {
            InitializeVignette();
        }

        EnsureParticleSystems();
        RefreshTilemaps();
    }

    private void EnsureParticleSystems()
    {
        if (_dustParticles == null)
        {
            Transform existing = transform.Find("AmbientDustParticles") ?? transform.Find("CeilingDustParticles");
            if (existing != null)
            {
                _dustParticles = existing.GetComponent<ParticleSystem>();
            }
            else
            {
                _dustParticles = CreateDefaultDustParticleSystem("AmbientDustParticles");
            }
        }

        Transform existingDebris = transform.Find("AmbientDebrisParticles") ?? transform.Find("CeilingDebrisParticles");
        if (existingDebris != null)
        {
            if (Application.isPlaying)
            {
                Destroy(existingDebris.gameObject);
            }
            else
            {
                DestroyImmediate(existingDebris.gameObject);
            }
        }

        if (_dustParticles != null)
        {
            ConfigureDustParticleSystem(_dustParticles, _particleAreaSize, _particleSortingLayerName, _particleSortingOrder);
        }
    }

    public static void ConfigureDustParticleSystem(ParticleSystem ps, Vector2 areaSize)
    {
        ConfigureDustParticleSystem(ps, areaSize, "VFX", 10);
    }

    public static void ConfigureDustParticleSystem(ParticleSystem ps, Vector2 areaSize, string sortingLayer, int sortingOrder)
    {
        if (ps == null) return;

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.22f);
        // Desaturated brown, almost grey (warm earth/dust tone: R > G > B, muted)
        main.startColor = new Color(0.54f, 0.49f, 0.45f, 1f);
        main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 200;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(Mathf.Max(4f, areaSize.x), Mathf.Max(4f, areaSize.y), 1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-0.10f, 0.10f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.06f, 0.08f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = new ParticleSystem.MinMaxCurve(0.18f);
        noise.frequency = 0.25f;
        noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.08f);
        noise.damping = true;

        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.2f),
                new GradientAlphaKey(0.85f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLife.color = grad;

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.25f, 1f);
        sizeCurve.AddKey(0.75f, 1f);
        sizeCurve.AddKey(1f, 0.4f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var collision = ps.collision;
        collision.enabled = false;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingLayerName = !string.IsNullOrEmpty(sortingLayer) ? sortingLayer : "VFX";
            renderer.sortingOrder = sortingOrder;

            Material circleMat = GetOrCreateCircleMaterial();
            if (circleMat != null)
            {
                renderer.sharedMaterial = circleMat;
            }
            else if (renderer.sharedMaterial != null)
            {
                renderer.sharedMaterial.mainTexture = GetOrCreateCircleTexture();
                if (renderer.sharedMaterial.HasProperty("_BaseMap"))
                {
                    renderer.sharedMaterial.SetTexture("_BaseMap", GetOrCreateCircleTexture());
                }
            }
        }
    }

    public static Texture2D GetOrCreateCircleTexture()
    {
        if (_circleParticleTexture != null)
        {
            return _circleParticleTexture;
        }

        const int size = 64;
        _circleParticleTexture = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
        {
            name = "Procedural_DustCircle",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave
        };

        float center = (size - 1) * 0.5f;
        float radius = center - 1f;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center;
                float dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                // Smooth anti-aliased circular core with zero alpha outside the circle radius
                float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.60f, 0.95f, dist));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        _circleParticleTexture.SetPixels(pixels);
        _circleParticleTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        return _circleParticleTexture;
    }

    public static Material GetOrCreateCircleMaterial()
    {
        if (_circleParticleMaterial != null &&
            _circleParticleMaterial.shader != null &&
            _circleParticleMaterial.shader.name == "Universal Render Pipeline/Particles/Unlit" &&
            _circleParticleMaterial.HasProperty("_Surface") &&
            _circleParticleMaterial.GetFloat("_Surface") == 1.0f)
        {
            return _circleParticleMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Universal Render Pipeline/Unlit")
            ?? Shader.Find("Particles/Standard Unlit");

        if (shader == null)
        {
            Debug.LogWarning("[DungeonPressureAmbientPresenter] Universal Render Pipeline/Particles/Unlit shader not found.");
            return null;
        }

        _circleParticleMaterial = new Material(shader)
        {
            name = "Procedural_DustCircleMaterial",
            hideFlags = HideFlags.DontSave
        };

        Texture2D circleTex = GetOrCreateCircleTexture();
        _circleParticleMaterial.mainTexture = circleTex;
        if (_circleParticleMaterial.HasProperty("_BaseMap"))
        {
            _circleParticleMaterial.SetTexture("_BaseMap", circleTex);
        }
        if (_circleParticleMaterial.HasProperty("_MainTex"))
        {
            _circleParticleMaterial.SetTexture("_MainTex", circleTex);
        }
        if (_circleParticleMaterial.HasProperty("_BaseColor"))
        {
            _circleParticleMaterial.SetColor("_BaseColor", Color.white);
        }
        if (_circleParticleMaterial.HasProperty("_Color"))
        {
            _circleParticleMaterial.SetColor("_Color", Color.white);
        }

        // Configure URP Particles/Unlit for transparent alpha blending:
        if (_circleParticleMaterial.HasProperty("_Surface"))
        {
            _circleParticleMaterial.SetFloat("_Surface", 1.0f); // 1 = Transparent
        }
        if (_circleParticleMaterial.HasProperty("_Blend"))
        {
            _circleParticleMaterial.SetFloat("_Blend", 0.0f); // 0 = Alpha blend
        }
        if (_circleParticleMaterial.HasProperty("_AlphaClip"))
        {
            _circleParticleMaterial.SetFloat("_AlphaClip", 0.0f);
        }
        if (_circleParticleMaterial.HasProperty("_SrcBlend"))
        {
            _circleParticleMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        }
        if (_circleParticleMaterial.HasProperty("_DstBlend"))
        {
            _circleParticleMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        }
        if (_circleParticleMaterial.HasProperty("_SrcBlendAlpha"))
        {
            _circleParticleMaterial.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        }
        if (_circleParticleMaterial.HasProperty("_DstBlendAlpha"))
        {
            _circleParticleMaterial.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        }
        if (_circleParticleMaterial.HasProperty("_ZWrite"))
        {
            _circleParticleMaterial.SetFloat("_ZWrite", 0.0f);
        }
        if (_circleParticleMaterial.HasProperty("_Cull"))
        {
            _circleParticleMaterial.SetFloat("_Cull", 0.0f); // 0 = Off
        }
        if (_circleParticleMaterial.HasProperty("_ColorMode"))
        {
            _circleParticleMaterial.SetFloat("_ColorMode", 0.0f); // 0 = Multiply
        }

        _circleParticleMaterial.renderQueue = (int)RenderQueue.Transparent;
        _circleParticleMaterial.SetOverrideTag("RenderType", "Transparent");
        _circleParticleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _circleParticleMaterial.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        _circleParticleMaterial.DisableKeyword("_ALPHATEST_ON");
        _circleParticleMaterial.DisableKeyword("_COLOROVERLAY_ON");
        _circleParticleMaterial.DisableKeyword("_COLORCOLOR_ON");
        _circleParticleMaterial.DisableKeyword("_COLORADDSUBDIFF_ON");

        return _circleParticleMaterial;
    }

    private ParticleSystem CreateDefaultDustParticleSystem(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.SetActive(false);

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ConfigureDustParticleSystem(ps, _particleAreaSize, _particleSortingLayerName, _particleSortingOrder);

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        go.SetActive(true);
        return ps;
    }

    public void RefreshTilemaps()
    {
        Tilemap[] allTilemaps = FindObjectsOfType<Tilemap>();
        var wallList = new System.Collections.Generic.List<Tilemap>();
        var floorList = new System.Collections.Generic.List<Tilemap>();

        for (int i = 0; i < allTilemaps.Length; i++)
        {
            Tilemap tm = allTilemaps[i];
            if (tm == null) continue;

            string lower = tm.name.ToLowerInvariant();
            bool hasCollider = tm.GetComponent<Collider2D>() != null;

            if (hasCollider || lower.Contains("wall") || lower.Contains("obstacle") || lower.Contains("collision"))
            {
                wallList.Add(tm);
            }
            else if (lower.Contains("floor") || lower.Contains("ground"))
            {
                floorList.Add(tm);
            }
        }

        _wallTilemaps = wallList.ToArray();
        _floorTilemaps = floorList.ToArray();
    }

    public void SetTilemaps(Tilemap[] wallTilemaps, Tilemap[] floorTilemaps)
    {
        _wallTilemaps = wallTilemaps;
        _floorTilemaps = floorTilemaps;
    }

    public void SetCustomOcclusionColliders(Collider2D[] colliders)
    {
        _customOcclusionColliders = colliders;
    }

    public bool IsPointInOpenSpace(Vector2 worldPos)
    {
        // 1. If it touches any wall or obstacle tile, it is NOT in open space
        if (_wallTilemaps != null)
        {
            for (int i = 0; i < _wallTilemaps.Length; i++)
            {
                Tilemap wallTm = _wallTilemaps[i];
                if (wallTm != null && wallTm.isActiveAndEnabled)
                {
                    Vector3Int cell = wallTm.WorldToCell(worldPos);
                    if (wallTm.HasTile(cell))
                    {
                        return false;
                    }
                }
            }
        }

        // 2. If floor tilemaps exist in the scene, ensure the point is over an active floor tile (not outside the dungeon)
        if (_floorTilemaps != null && _floorTilemaps.Length > 0)
        {
            bool hasFloor = false;
            for (int i = 0; i < _floorTilemaps.Length; i++)
            {
                Tilemap floorTm = _floorTilemaps[i];
                if (floorTm != null && floorTm.isActiveAndEnabled)
                {
                    Vector3Int cell = floorTm.WorldToCell(worldPos);
                    if (floorTm.HasTile(cell))
                    {
                        hasFloor = true;
                        break;
                    }
                }
            }

            if (!hasFloor)
            {
                return false;
            }
        }

        // 3. Custom colliders check (e.g. for unit tests)
        if (_customOcclusionColliders != null)
        {
            for (int i = 0; i < _customOcclusionColliders.Length; i++)
            {
                var col = _customOcclusionColliders[i];
                if (col != null && col.isActiveAndEnabled && col.OverlapPoint(worldPos))
                {
                    return false;
                }
            }
        }

        return true;
    }

    public bool IsPointOccluded(Vector2 worldPos)
    {
        return !IsPointInOpenSpace(worldPos);
    }

    public void CullOccludedParticles(ParticleSystem ps)
    {
        if (!_cullOccludedParticles || ps == null || ps.particleCount == 0)
        {
            return;
        }

        if (_wallTilemaps == null && _floorTilemaps == null)
        {
            RefreshTilemaps();
        }

        if ((_wallTilemaps == null || _wallTilemaps.Length == 0) &&
            (_floorTilemaps == null || _floorTilemaps.Length == 0) &&
            (_customOcclusionColliders == null || _customOcclusionColliders.Length == 0))
        {
            return;
        }

        int maxParticles = ps.main.maxParticles;
        if (_particlesBuffer == null || _particlesBuffer.Length < maxParticles)
        {
            _particlesBuffer = new ParticleSystem.Particle[maxParticles];
        }

        int count = ps.GetParticles(_particlesBuffer);
        bool modified = false;

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = _particlesBuffer[i].position;
            Vector2 worldPos = ps.main.simulationSpace == ParticleSystemSimulationSpace.World
                ? (Vector2)pos
                : (Vector2)ps.transform.TransformPoint(pos);

            if (!IsPointInOpenSpace(worldPos))
            {
                _particlesBuffer[i].remainingLifetime = -1f;
                modified = true;
            }
        }

        if (modified)
        {
            ps.SetParticles(_particlesBuffer, count);
        }
    }

    private void LocateGlobalLight()
    {
        Light2D[] lights = FindObjectsOfType<Light2D>();
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i].lightType == Light2D.LightType.Global)
            {
                _globalLight = lights[i];
                break;
            }
        }
    }

    private void LocateVolume()
    {
        Volume[] volumes = FindObjectsOfType<Volume>();
        for (int i = 0; i < volumes.Length; i++)
        {
            if (volumes[i].isGlobal)
            {
                _volume = volumes[i];
                InitializeVignette();
                break;
            }
        }
    }

    private void InitializeVignette()
    {
        if (_volume == null)
        {
            return;
        }

        VolumeProfile profile = _volume.sharedProfile;
        if (profile == null)
        {
            profile = _volume.profile;
        }

        if (profile != null)
        {
            if (profile.components != null)
            {
                profile.components.RemoveAll(c => c == null);
            }

            if (!profile.TryGet(out _vignette))
            {
                _vignette = profile.Add<Vignette>(overrides: true);
            }
        }
    }

    public void ApplyInitialPhase(DungeonPressurePhase phase)
    {
        _hasResolvedInitialPhase = true;
        _lastPhase = phase;
        _periodicShakeTimer = 0f;

        if (_config == null)
        {
            return;
        }

        DungeonAmbientPhaseSettings settings = _config.GetSettings(phase);

        // Snap global light immediately without interpolation
        _targetGlobalColor = settings.GlobalLightColor;
        _targetGlobalIntensity = settings.GlobalLightIntensity;
        _currentGlobalColor = _targetGlobalColor;
        _currentGlobalIntensity = _targetGlobalIntensity;
        _globalTransitionProgress = 1f;

        if (_globalLight != null)
        {
            _globalLight.color = _currentGlobalColor;
            _globalLight.intensity = _currentGlobalIntensity;
        }

        // Apply continuous shake if this phase defines a sustained baseline (e.g. Collapse)
        if (CameraShakeController.Instance != null)
        {
            CameraShakeController.Instance.SetContinuousShake(settings.ContinuousShakeIntensity);
        }

        // Snap post-process vignette immediately
        _targetVignetteColor = settings.VignetteColor;
        _targetVignetteIntensity = settings.VignetteIntensity;
        _targetVignetteSmoothness = settings.VignetteSmoothness;
        _targetVignetteRounded = settings.VignetteRounded;

        _currentVignetteColor = _targetVignetteColor;
        _currentVignetteIntensity = _targetVignetteIntensity;
        _currentVignetteSmoothness = _targetVignetteSmoothness;
        _vignetteTransitionProgress = 1f;

        ApplyVignetteToVolume();
        ApplyParticleEmission(settings);

        // Notify props immediately
        DungeonLightPropRegistry.NotifyPhaseChanged(phase, immediate: true);
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (_pressureController == null)
        {
            InitializeComponents();
            if (_pressureController == null)
            {
                return;
            }
        }

        if (!_hasResolvedInitialPhase)
        {
            ApplyInitialPhase(_pressureController.Phase);
        }

        EvaluatePresentation(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        UpdateCameraParticlePosition();
        CullOccludedParticles(_dustParticles);
    }

    public void EvaluatePresentation(float deltaTime)
    {
        if (_pressureController != null)
        {
            DungeonPressurePhase newPhase = _pressureController.Phase;

            if (newPhase != _lastPhase)
            {
                OnPhaseChanged(newPhase);
                _lastPhase = newPhase;
            }
        }

        UpdatePeriodicShake(deltaTime);
        UpdateGlobalLightInterpolation(deltaTime);
        UpdateVignetteInterpolation(deltaTime);
        UpdateCameraParticlePosition();
        CullOccludedParticles(_dustParticles);
    }

    private void OnPhaseChanged(DungeonPressurePhase newPhase)
    {
        if (_config == null)
        {
            return;
        }

        _periodicShakeTimer = 0f;
        DungeonAmbientPhaseSettings settings = _config.GetSettings(newPhase);

        // Only trigger entry shake when pressure escalates forward
        if (newPhase > _lastPhase && settings.EntryShakeIntensity > 0f)
        {
            CameraShakeController.Instance?.RequestShake(settings.EntryShakeIntensity, settings.EntryShakeDuration);
        }

        // Maintain continuous baseline shake throughout this phase
        CameraShakeController.Instance?.SetContinuousShake(settings.ContinuousShakeIntensity);

        // Setup smooth transition for global ambient light
        _targetGlobalColor = settings.GlobalLightColor;
        _targetGlobalIntensity = settings.GlobalLightIntensity;
        _globalTransitionDuration = settings.GlobalTransitionDuration;
        _fromGlobalColor = _currentGlobalColor;
        _fromGlobalIntensity = _currentGlobalIntensity;

        if (_globalTransitionDuration <= 0f)
        {
            _currentGlobalColor = _targetGlobalColor;
            _currentGlobalIntensity = _targetGlobalIntensity;
            _globalTransitionProgress = 1f;
            if (_globalLight != null)
            {
                _globalLight.color = _currentGlobalColor;
                _globalLight.intensity = _currentGlobalIntensity;
            }
        }
        else
        {
            _globalTransitionProgress = 0f;
        }

        // Setup smooth transition for post-process vignette
        _targetVignetteColor = settings.VignetteColor;
        _targetVignetteIntensity = settings.VignetteIntensity;
        _targetVignetteSmoothness = settings.VignetteSmoothness;
        _targetVignetteRounded = settings.VignetteRounded;

        float vignetteDuration = settings.VignetteTransitionDuration > 0f
            ? settings.VignetteTransitionDuration
            : settings.GlobalTransitionDuration;

        _vignetteTransitionDuration = vignetteDuration;
        _fromVignetteColor = _currentVignetteColor;
        _fromVignetteIntensity = _currentVignetteIntensity;
        _fromVignetteSmoothness = _currentVignetteSmoothness;

        if (_vignetteTransitionDuration <= 0f)
        {
            _currentVignetteColor = _targetVignetteColor;
            _currentVignetteIntensity = _targetVignetteIntensity;
            _currentVignetteSmoothness = _targetVignetteSmoothness;
            _vignetteTransitionProgress = 1f;
            ApplyVignetteToVolume();
        }
        else
        {
            _vignetteTransitionProgress = 0f;
        }

        // Update particle systems
        ApplyParticleEmission(settings);

        // Notify all registered light props in the dungeon
        DungeonLightPropRegistry.NotifyPhaseChanged(newPhase, immediate: false);
    }

    private void UpdatePeriodicShake(float deltaTime)
    {
        if (_config == null)
        {
            return;
        }

        DungeonAmbientPhaseSettings settings = _config.GetSettings(_lastPhase);
        if (settings.PeriodicShakeIntervalSeconds <= 0f || settings.PeriodicShakeIntensity <= 0f)
        {
            return;
        }

        _periodicShakeTimer += deltaTime;
        if (_periodicShakeTimer >= settings.PeriodicShakeIntervalSeconds)
        {
            _periodicShakeTimer = 0f;
            CameraShakeController.Instance?.RequestShake(settings.PeriodicShakeIntensity, settings.PeriodicShakeDuration);
            if (settings.PeriodicShakeSfx.IsValid && AudioManager.Instance != null)
            {
                Vector3 playPos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioManager.Instance.PlaySfx(settings.PeriodicShakeSfx, playPos);
            }
        }
    }

    private void UpdateCameraParticlePosition()
    {
        if (!_followCamera) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 camPos = cam.transform.position;
        Vector3 targetPos = new Vector3(camPos.x, camPos.y, 0f);

        if (_dustParticles != null)
        {
            _dustParticles.transform.position = targetPos;
        }
    }

    private void ApplyParticleEmission(DungeonAmbientPhaseSettings settings)
    {
        if (_dustParticles != null)
        {
            var emission = _dustParticles.emission;
            emission.rateOverTime = settings.DustEmissionRate;
            if (settings.DustEmissionRate > 0.01f && !_dustParticles.isPlaying)
            {
                _dustParticles.Play();
            }
            else if (settings.DustEmissionRate <= 0.01f && _dustParticles.isPlaying)
            {
                _dustParticles.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }

    private void UpdateGlobalLightInterpolation(float deltaTime)
    {
        if (_globalLight == null || _globalTransitionProgress >= 1f)
        {
            return;
        }

        float rate = _globalTransitionDuration > 0.001f ? (deltaTime / _globalTransitionDuration) : 1f;
        _globalTransitionProgress = Mathf.Clamp01(_globalTransitionProgress + rate);

        _currentGlobalColor = Color.Lerp(_fromGlobalColor, _targetGlobalColor, _globalTransitionProgress);
        _currentGlobalIntensity = Mathf.Lerp(_fromGlobalIntensity, _targetGlobalIntensity, _globalTransitionProgress);

        _globalLight.color = _currentGlobalColor;
        _globalLight.intensity = _currentGlobalIntensity;
    }

    private void UpdateVignetteInterpolation(float deltaTime)
    {
        if (_vignetteTransitionProgress >= 1f)
        {
            return;
        }

        float rate = _vignetteTransitionDuration > 0.001f ? (deltaTime / _vignetteTransitionDuration) : 1f;
        _vignetteTransitionProgress = Mathf.Clamp01(_vignetteTransitionProgress + rate);

        _currentVignetteColor = Color.Lerp(_fromVignetteColor, _targetVignetteColor, _vignetteTransitionProgress);
        _currentVignetteIntensity = Mathf.Lerp(_fromVignetteIntensity, _targetVignetteIntensity, _vignetteTransitionProgress);
        _currentVignetteSmoothness = Mathf.Lerp(_fromVignetteSmoothness, _targetVignetteSmoothness, _vignetteTransitionProgress);

        ApplyVignetteToVolume();
    }

    private void ApplyVignetteToVolume()
    {
        if (_vignette == null)
        {
            InitializeVignette();
            if (_vignette == null)
            {
                return;
            }
        }

        bool active = _currentVignetteIntensity > 0.001f || _targetVignetteIntensity > 0.001f;
        _vignette.active = active;
        _vignette.color.overrideState = active;
        _vignette.color.value = _currentVignetteColor;

        _vignette.intensity.overrideState = active;
        _vignette.intensity.value = _currentVignetteIntensity;

        _vignette.smoothness.overrideState = active;
        _vignette.smoothness.value = _currentVignetteSmoothness;

        _vignette.rounded.overrideState = active;
        _vignette.rounded.value = _targetVignetteRounded;
    }

    public void ResetPresentation()
    {
        if (CameraShakeController.Instance != null)
        {
            CameraShakeController.Instance.SetContinuousShake(0f);
        }

        _currentVignetteIntensity = 0f;
        _targetVignetteIntensity = 0f;
        _vignetteTransitionProgress = 1f;
        ApplyVignetteToVolume();

        if (_dustParticles != null)
        {
            _dustParticles.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        DungeonLightPropRegistry.Clear();
        _periodicShakeTimer = 0f;
        _hasResolvedInitialPhase = false;
    }

#if UNITY_EDITOR
    [ContextMenu("Preview/Phase: Normal")]
    public void PreviewNormal() => PreviewPhase(DungeonPressurePhase.Normal);

    [ContextMenu("Preview/Phase: Reinforcements")]
    public void PreviewReinforcements() => PreviewPhase(DungeonPressurePhase.Reinforcements);

    [ContextMenu("Preview/Phase: Critical Pressure")]
    public void PreviewCriticalPressure() => PreviewPhase(DungeonPressurePhase.CriticalPressure);

    [ContextMenu("Preview/Phase: Collapse")]
    public void PreviewCollapse() => PreviewPhase(DungeonPressurePhase.Collapse);

    [ContextMenu("Preview/Clear Preview")]
    public void ClearPreview()
    {
        InitializeComponents();
        if (_config != null)
        {
            ApplyInitialPhase(DungeonPressurePhase.Normal);
        }
        ResetPresentation();
        UnityEditor.SceneView.RepaintAll();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }

    public void PreviewPhase(DungeonPressurePhase phase)
    {
        InitializeComponents();
        ApplyInitialPhase(phase);
        UnityEditor.SceneView.RepaintAll();
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
    }
#endif
}
