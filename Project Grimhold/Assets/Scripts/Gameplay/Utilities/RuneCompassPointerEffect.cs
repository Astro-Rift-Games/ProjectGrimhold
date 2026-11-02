using UnityEngine;

/// <summary>
/// Componente de presentación para la Brújula Rúnica (Doc 14 UTL_RUNE_COMPASS).
/// Dibuja un puntero rúnico direccional flotante (flecha dorada/cyan) centrado en el jugador
/// que apunta hacia el Santuario de Extracción más cercano y emite un repique cristalino.
/// Se autodestruye suavemente al expirar la duración.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuneCompassPointerEffect : MonoBehaviour
{
    private const int ArrowPointCount = 5;
    private const float ShaftStartDist = 0.7f;
    private const float ArrowTipDist = 2.4f;
    private const float BarbDist = 1.8f;
    private const float BarbWidth = 0.45f;
    private const float FadeDuration = 1.0f;

    private static AudioClip s_provisionalChimeClip;
    private static Material s_provisionalMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_provisionalChimeClip = null;
        s_provisionalMaterial = null;
    }

    private LineRenderer _lineRenderer;
    private Vector2 _direction = Vector2.up;
    private Vector2 _perpendicular = Vector2.right;
    private float _totalDuration = 5.0f;
    private float _remainingDuration;
    private readonly Vector3[] _arrowPositions = new Vector3[ArrowPointCount];

    public void Initialize(Vector2 directionVector, float duration)
    {
        if (directionVector.sqrMagnitude < 0.0001f)
        {
            directionVector = Vector2.up;
        }

        _direction = directionVector.normalized;
        _perpendicular = new Vector2(-_direction.y, _direction.x);
        _totalDuration = Mathf.Max(0.5f, duration);
        _remainingDuration = _totalDuration;

        EnsureLineRenderer();
        PlayChimeAudio();
        UpdateGeometry(1f);
    }

    private void EnsureLineRenderer()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = true;
            return;
        }

        var pointerObj = new GameObject("RuneCompassDirectionPointerVisual");
        pointerObj.transform.SetParent(transform, false);
        pointerObj.transform.localPosition = Vector3.zero;

        _lineRenderer = pointerObj.AddComponent<LineRenderer>();
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.loop = false;
        _lineRenderer.positionCount = ArrowPointCount;
        _lineRenderer.startWidth = 0.14f;
        _lineRenderer.endWidth = 0.14f;
        _lineRenderer.numCapVertices = 4;
        _lineRenderer.numCornerVertices = 4;
        _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _lineRenderer.receiveShadows = false;
        _lineRenderer.sortingLayerName = "VFX";
        _lineRenderer.sortingOrder = 20;

        if (s_provisionalMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Particles/Standard Unlit");

            if (shader != null)
            {
                s_provisionalMaterial = new Material(shader)
                {
                    name = "RuneCompassPointerMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        if (s_provisionalMaterial != null)
        {
            _lineRenderer.sharedMaterial = s_provisionalMaterial;
        }
    }

    private void LateUpdate()
    {
        _remainingDuration -= Time.deltaTime;
        if (_remainingDuration <= 0f)
        {
            CleanupAndDestroy();
            return;
        }

        float alpha = _remainingDuration < FadeDuration
            ? Mathf.Clamp01(_remainingDuration / FadeDuration)
            : 1.0f;

        UpdateGeometry(alpha);
    }

    private void UpdateGeometry(float alpha)
    {
        if (_lineRenderer == null)
        {
            return;
        }

        Vector3 center = transform.position;
        Vector3 shaftStart = center + (Vector3)(_direction * ShaftStartDist);
        Vector3 tip = center + (Vector3)(_direction * ArrowTipDist);
        Vector3 leftBarb = center + (Vector3)(_direction * BarbDist + _perpendicular * BarbWidth);
        Vector3 rightBarb = center + (Vector3)(_direction * BarbDist - _perpendicular * BarbWidth);

        // 5-point continuous path: shaft base -> tip -> left barb -> tip -> right barb
        _arrowPositions[0] = shaftStart;
        _arrowPositions[1] = tip;
        _arrowPositions[2] = leftBarb;
        _arrowPositions[3] = tip;
        _arrowPositions[4] = rightBarb;

        _lineRenderer.SetPositions(_arrowPositions);

        var glowColor = new Color(0.35f, 0.95f, 0.85f, alpha * 0.9f);
        _lineRenderer.startColor = glowColor;
        _lineRenderer.endColor = glowColor;
    }

    private void PlayChimeAudio()
    {
        AudioClip clip = GetOrCreateProvisionalChimeClip();
        if (clip == null) return;

        AudioManager audioManager = AudioManager.Instance;
        if (audioManager != null)
        {
            audioManager.PlaySfx(clip, transform.position, 0.85f, 1.05f);
            return;
        }

        AudioSource.PlayClipAtPoint(clip, transform.position, 0.85f);
    }

    private static AudioClip GetOrCreateProvisionalChimeClip()
    {
        if (s_provisionalChimeClip != null)
        {
            return s_provisionalChimeClip;
        }

        const int sampleRate = 22050;
        const float durationSeconds = 0.5f;
        int sampleCount = Mathf.CeilToInt(sampleRate * durationSeconds);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-8.0f * t);
            float fundamental = Mathf.Sin(2f * Mathf.PI * 1174.66f * t) * 0.55f; // D6
            float octave = Mathf.Sin(2f * Mathf.PI * 2349.32f * t) * 0.25f * Mathf.Exp(-12f * t);
            float shimmer = Mathf.Sin(2f * Mathf.PI * 3520.0f * t) * 0.15f * Mathf.Exp(-15f * t);
            samples[i] = (fundamental + octave + shimmer) * envelope;
        }

        s_provisionalChimeClip = AudioClip.Create(
            "ProvisionalRuneCompassChime",
            sampleCount,
            1,
            sampleRate,
            false);
        s_provisionalChimeClip.hideFlags = HideFlags.HideAndDontSave;
        s_provisionalChimeClip.SetData(samples, 0);
        return s_provisionalChimeClip;
    }

    private void OnDisable() => CleanupAndDestroy();

    private void OnDestroy() => CleanupAndDestroy();

    private void CleanupAndDestroy()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.enabled = false;
            if (_lineRenderer.gameObject != gameObject)
            {
                Destroy(_lineRenderer.gameObject);
            }
        }

        Destroy(this);
    }
}
