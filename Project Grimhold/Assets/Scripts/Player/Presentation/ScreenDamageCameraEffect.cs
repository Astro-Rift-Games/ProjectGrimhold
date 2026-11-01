using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Camera-owned material and owner-scoped lease; never changes a shared material.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Camera))]
public sealed class ScreenDamageCameraEffect : MonoBehaviour
{
    [SerializeField] private Material _materialTemplate;
    private Material _material;
    private Camera _camera;
    private Object _owner;
    private Transform _avatar;
    private ScreenDamageConfig _config;
    private float _weight;
    private bool _reportedMissingMaterial;

    internal Material RuntimeMaterial => _material;

    public void Apply(Object owner, Transform avatar, ScreenDamageConfig config, float weight)
    {
        if (owner == null || avatar == null || config == null || !float.IsFinite(weight) || weight <= .0001f)
        {
            Clear(owner);
            return;
        }
        _owner = owner;
        _avatar = avatar;
        _config = config;
        _weight = Mathf.Clamp01(weight);
    }

    public void Clear(Object owner)
    {
        if (_owner != owner) return;
        Release();
    }

    internal bool TryGetMaterial(Camera camera, out Material material)
    {
        material = null;
        if (_camera == null) _camera = GetComponent<Camera>();
        if (!isActiveAndEnabled || camera != _camera || camera.cameraType != CameraType.Game ||
            _owner == null || _avatar == null || _config == null || _weight <= .0001f)
        {
            if (_owner == null || _avatar == null || _config == null) Release();
            return false;
        }
        if (_config.BlurPixels <= 0f && _config.ChromaticPixels <= 0f) return false;
        if (_materialTemplate == null)
        {
            if (!_reportedMissingMaterial)
            {
                Debug.LogError("ScreenDamageCameraEffect is missing its material template.", this);
                _reportedMissingMaterial = true;
            }
            return false;
        }
        _material ??= new Material(_materialTemplate) { name = "Local Screen Damage (Runtime)", hideFlags = HideFlags.HideAndDontSave };
        Vector3 avatar = camera.WorldToViewportPoint(_avatar.position);
        Vector2 cursor = Mouse.current != null
            ? (Vector2)camera.ScreenToViewportPoint(Mouse.current.position.ReadValue())
            : new Vector2(-10f, -10f);
        _material.SetFloat("_EffectWeight", _weight);
        _material.SetFloat("_BlurPixels", _config.BlurPixels);
        _material.SetFloat("_ChromaticPixels", _config.ChromaticPixels);
        _material.SetFloat("_ClearCenter", _config.ClearCenter);
        _material.SetFloat("_ViewportAspect", camera.aspect);
        _material.SetVector("_AvatarClear", new Vector4(avatar.x, avatar.y, _config.AvatarClearRadius, .04f));
        _material.SetVector("_CursorClear", new Vector4(cursor.x, cursor.y, _config.CursorClearRadius, .025f));
        material = _material;
        return true;
    }

    private void Release()
    {
        _owner = null;
        _avatar = null;
        _config = null;
        _weight = 0f;
        if (_material != null)
        {
            // Disable immediately even when destruction is deferred until the end of a frame.
            _material.SetFloat("_EffectWeight", 0f);
            if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material);
            _material = null;
        }
    }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
