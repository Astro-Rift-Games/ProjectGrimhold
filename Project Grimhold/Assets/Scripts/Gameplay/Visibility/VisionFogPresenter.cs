using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Local-only fog presentation reading the active viewer from the visibility system.
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

        private static readonly int FogColorId = Shader.PropertyToID("_FogColor");
        private static readonly int InsideDensityId = Shader.PropertyToID("_InsideDensity");
        private static readonly int NoiseScaleId = Shader.PropertyToID("_NoiseScale");
        private static readonly int NoiseStrengthId = Shader.PropertyToID("_NoiseStrength");
        private static readonly int WindVelocityId = Shader.PropertyToID("_WindVelocity");
        private static readonly int PixelsPerUnitId = Shader.PropertyToID("_PixelsPerUnit");
        private static readonly int RadialSoftnessId = Shader.PropertyToID("_RadialSoftness");
        private static readonly int AngularSoftnessId = Shader.PropertyToID("_AngularSoftness");
        private static readonly int EdgeNoiseAmplitudeId = Shader.PropertyToID("_EdgeNoiseAmplitude");
        private static readonly int FarFogStartId = Shader.PropertyToID("_FarFogStart");
        private static readonly int FarFogEndId = Shader.PropertyToID("_FarFogEnd");
        private static readonly int FarFogDensityId = Shader.PropertyToID("_FarFogDensity");
        private static readonly int VisionOriginId = Shader.PropertyToID("_VisionOrigin");
        private static readonly int VisionFacingId = Shader.PropertyToID("_VisionFacing");
        private static readonly int VisionNearRadiusId = Shader.PropertyToID("_VisionNearRadius");
        private static readonly int VisionHalfAngleId = Shader.PropertyToID("_VisionHalfAngle");
        private static readonly int VisionClearStrengthId = Shader.PropertyToID("_VisionClearStrength");

        private MaterialPropertyBlock _block;
        private Vector2 _origin;
        private Vector2 _facing = Vector2.up;
        private bool _hasFacing;
        private float _nearRadius;
        private float _halfAngle;
        private float _clearStrength;

        private void Awake()
        {
            if (_visibilitySystem == null || _camera == null || _renderer == null || _settings == null)
            {
                Debug.LogError($"{nameof(VisionFogPresenter)} requires a visibility system, camera, quad renderer and fog settings.", this);
                enabled = false;
                return;
            }

            _block = new MaterialPropertyBlock();
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
                _nearRadius = query.NearRadius;
                _halfAngle = Mathf.Acos(Mathf.Clamp(query.ConeHalfAngleCosine, -1f, 1f));
            }

            _block.SetColor(FogColorId, _settings.FogColor);
            _block.SetFloat(InsideDensityId, _settings.InsideDensity);
            _block.SetFloat(NoiseScaleId, _settings.NoiseScale);
            _block.SetFloat(NoiseStrengthId, _settings.NoiseStrength);
            _block.SetVector(WindVelocityId, new Vector4(_settings.WindVelocity.x, _settings.WindVelocity.y, 0f, 0f));
            _block.SetFloat(PixelsPerUnitId, _settings.PixelsPerUnit);
            _block.SetFloat(RadialSoftnessId, _settings.RadialSoftness);
            _block.SetFloat(AngularSoftnessId, _settings.AngularSoftnessDegrees * Mathf.Deg2Rad);
            _block.SetFloat(EdgeNoiseAmplitudeId, _settings.EdgeNoiseAmplitude);
            _block.SetFloat(FarFogStartId, _settings.FarFogStart);
            _block.SetFloat(FarFogEndId, _settings.FarFogEnd);
            _block.SetFloat(FarFogDensityId, _settings.FarFogDensity);
            _block.SetVector(VisionOriginId, new Vector4(_origin.x, _origin.y, 0f, 0f));
            _block.SetVector(VisionFacingId, new Vector4(_facing.x, _facing.y, 0f, 0f));
            _block.SetFloat(VisionNearRadiusId, _nearRadius);
            _block.SetFloat(VisionHalfAngleId, _halfAngle);
            _block.SetFloat(VisionClearStrengthId, _clearStrength);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
