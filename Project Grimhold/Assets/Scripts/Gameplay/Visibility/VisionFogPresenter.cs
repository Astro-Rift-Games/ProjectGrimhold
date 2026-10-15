using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Local-only fog presentation reading the active viewer from the visibility system, with an
    /// occlusion-aware clear area rendered via <see cref="VisionFogMask"/>.
    /// Never decides entity visibility; fails closed (no viewer means full fog).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VisionFogPresenter : MonoBehaviour
    {
        [SerializeField] private EntityVisibilitySystem _visibilitySystem;
        [SerializeField] private Camera _camera;
        [Tooltip("Renderer on a centered built-in 1x1 Quad, not parented under a scaled transform.")]
        [SerializeField] private Renderer _renderer;
        [SerializeField] private VisionFogSettings _settings;
        [Tooltip("Material using Hidden/Grimhold/Visibility/VisionFogMask; referenced so the shader ships in builds.")]
        [SerializeField] private Material _maskMaterial;

        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int InsideDensityId = Shader.PropertyToID("_InsideDensity");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int WindVelocityId = Shader.PropertyToID("_WindVelocity");
        private static readonly int PixelsPerUnitId = Shader.PropertyToID("_PixelsPerUnit");
        private static readonly int VisionMaskId = Shader.PropertyToID("_VisionMask");
        private static readonly int MaskWorldRectId = Shader.PropertyToID("_MaskWorldRect");
        private static readonly int EdgeBreakupId = Shader.PropertyToID("_EdgeBreakup");
        private static readonly int FarFogStartId = Shader.PropertyToID("_FarFogStart");
        private static readonly int FarFogEndId = Shader.PropertyToID("_FarFogEnd");
        private static readonly int FarFogDensityId = Shader.PropertyToID("_FarFogDensity");
        private static readonly int VisionOriginId = Shader.PropertyToID("_VisionOrigin");
        private static readonly int VisionClearStrengthId = Shader.PropertyToID("_VisionClearStrength");

        private MaterialPropertyBlock _block;
        private Vector2 _origin;
        private Vector2 _facing = Vector2.up;
        private bool _hasFacing;
        private VisionFogMask _mask;
        private float _clearStrength;

        private void Awake()
        {
            if (_visibilitySystem == null || _camera == null || _renderer == null || _settings == null || _maskMaterial == null)
            {
                Debug.LogError($"{nameof(VisionFogPresenter)} requires a visibility system, camera, quad renderer, fog settings and mask material.", this);
                enabled = false;
                return;
            }

            _block = new MaterialPropertyBlock();
            _mask = new VisionFogMask(_maskMaterial);
        }

        private void OnDestroy()
        {
            _mask?.Dispose();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
        {
            if (renderingCamera != _camera)
            {
                return;
            }

            // Follow after camera LateUpdate; the quad must have no scaled parent.
            Transform fogTransform = _renderer.transform;
            Vector3 cameraPosition = _camera.transform.position;
            fogTransform.position = new Vector3(cameraPosition.x, cameraPosition.y, fogTransform.position.z);
            fogTransform.rotation = Quaternion.identity;
            Vector2 coverage = VisionFogRules.OrthographicCoverage(
                _camera.orthographicSize, _camera.aspect, _settings.CoverageMargin);
            fogTransform.localScale = new Vector3(coverage.x, coverage.y, 1f);
        }

        private void LateUpdate()
        {
            bool hasViewer = _visibilitySystem.TryBuildActiveQuery(out VisionQuery query);
            float previousClearStrength = _clearStrength;
            _clearStrength = VisionFogRules.StepClearStrength(
                _clearStrength, hasViewer, _settings.ClearFadeSeconds, Time.deltaTime);

            if (hasViewer)
            {
                _origin = query.Origin;
                _facing = previousClearStrength <= 0f || !_hasFacing
                    ? query.Facing
                    : VisionFogRules.SmoothFacing(_facing, query.Facing, _settings.FacingSharpness, Time.deltaTime);
                _hasFacing = true;
                float coneAngleDegrees = 2f * Mathf.Acos(Mathf.Clamp(query.ConeHalfAngleCosine, -1f, 1f)) * Mathf.Rad2Deg;
                Vector2 coverage = VisionFogRules.OrthographicCoverage(
                    _camera.orthographicSize, _camera.aspect, _settings.CoverageMargin);
                Vector3 cameraPosition = _camera.transform.position;
                Rect worldRect = new Rect(cameraPosition.x - coverage.x * 0.5f,
                    cameraPosition.y - coverage.y * 0.5f, coverage.x, coverage.y);
                int width = Mathf.Max(16, Mathf.RoundToInt(_camera.pixelWidth * _settings.MaskResolutionScale));
                int height = Mathf.Max(16, Mathf.RoundToInt(_camera.pixelHeight * _settings.MaskResolutionScale));
                _mask.Render(in query, _facing, coneAngleDegrees, worldRect, width, height, _settings);
            }

            _block.SetColor(FogColorId, _settings.FogColor);
            _block.SetFloat(InsideDensityId, _settings.InsideDensity);
            _block.SetFloat(NoiseScaleId, _settings.NoiseScale);
            _block.SetFloat(NoiseStrengthId, _settings.NoiseStrength);
            _block.SetVector(WindVelocityId, new Vector4(_settings.WindVelocity.x, _settings.WindVelocity.y, 0f, 0f));
            _block.SetFloat(PixelsPerUnitId, _settings.PixelsPerUnit);
            _block.SetVector(MaskWorldRectId, _mask.WorldRect);
            if (_mask.Texture != null)
            {
                _block.SetTexture(VisionMaskId, _mask.Texture);
            }
            _block.SetFloat(EdgeBreakupId, _settings.EdgeBreakup);
            _block.SetFloat(FarFogStartId, _settings.FarFogStart);
            _block.SetFloat(FarFogEndId, _settings.FarFogEnd);
            _block.SetFloat(FarFogDensityId, _settings.FarFogDensity);
            _block.SetVector(VisionOriginId, new Vector4(_origin.x, _origin.y, 0f, 0f));
            _block.SetFloat(VisionClearStrengthId, _clearStrength);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
