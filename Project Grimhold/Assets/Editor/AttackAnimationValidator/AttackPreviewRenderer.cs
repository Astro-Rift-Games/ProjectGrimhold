#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Owns camera/target, borrows rig. Render returns a caller-owned readable texture.</summary>
public sealed class AttackPreviewRenderer : IDisposable
{
    private readonly AttackPreviewRig _rig;
    private Camera _camera;
    private RenderTexture _target;
    private bool _disposed;
    public AttackPreviewRenderer(AttackPreviewRig rig, float orthographicSize = 1.5f)
    {
        _rig = rig ?? throw new ArgumentNullException(nameof(rig));
        if (!float.IsFinite(orthographicSize) || orthographicSize <= 0) throw new ArgumentOutOfRangeException(nameof(orthographicSize));
        var go = new GameObject("Attack preview camera") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            SceneManager.MoveGameObjectToScene(go, rig.Scene);
            _camera = go.AddComponent<Camera>();
            _camera.scene = rig.Scene;
            _camera.enabled = false;
            _camera.orthographic = true;
            _camera.orthographicSize = orthographicSize;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Background;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 30f;
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
        }
        catch { Object.DestroyImmediate(go); throw; }
    }
    public Color Background => new Color(0.12f, 0.12f, 0.12f, 1f);
    public Texture2D Render(float seconds, int width, int height)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(AttackPreviewRenderer));
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        var previous = RenderTexture.active;
        Texture2D result = null;
        try
        {
            _rig.Evaluate(seconds);
            EnsureTarget(width, height);
            _camera.transform.SetPositionAndRotation(_rig.VisualRoot.position + new Vector3(0, 0.3f, -10), Quaternion.identity);
            _camera.Render();
            RenderTexture.active = _target;
            result = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            result.Apply(false, false);
            return result;
        }
        catch { if (result != null) Object.DestroyImmediate(result); throw; }
        finally { RenderTexture.active = previous; }
    }
    private void EnsureTarget(int width, int height)
    {
        if (_target != null && _target.width == width && _target.height == height) return;
        ReleaseTarget();
        _target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
        if (!_target.Create())
        {
            ReleaseTarget();
            throw new InvalidOperationException($"Could not create attack preview render target ({width}x{height}).");
        }
        _camera.targetTexture = _target;
    }
    /// <summary>Maps a world marker to a GUI preview rectangle after rendering.</summary>
    public Vector2 Project(Vector3 world, Rect rect)
    {
        var p = _camera.WorldToViewportPoint(world);
        return new Vector2(rect.x + p.x * rect.width, rect.y + (1 - p.y) * rect.height);
    }
    private void ReleaseTarget()
    {
        if (_camera != null) _camera.targetTexture = null;
        if (_target == null) return;
        _target.Release();
        Object.DestroyImmediate(_target);
        _target = null;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ReleaseTarget();
        if (_camera != null) Object.DestroyImmediate(_camera.gameObject);
        _camera = null;
    }
}
#endif
