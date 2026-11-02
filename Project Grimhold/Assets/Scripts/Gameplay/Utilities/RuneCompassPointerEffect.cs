using UnityEngine;

/// <summary>
/// Componente de presentación para la Brújula Rúnica (Doc 14 UTL_RUNE_COMPASS).
/// Renderiza un puntero rúnico en forma de Sprite (SpriteRenderer) flotando alrededor del jugador
/// que rota dinámicamente y se mantiene apuntando en tiempo real hacia el Santuario de Extracción
/// más cercano mientras permanece visible, emitiendo un repique cristalino.
/// Se autodestruye suavemente con desvanecimiento al expirar los 5.0 segundos.
/// </summary>
[DisallowMultipleComponent]
public sealed class RuneCompassPointerEffect : MonoBehaviour
{
    private const float OrbitRadius = 1.35f;
    private const float FadeDuration = 1.0f;
    private const int SpriteResolution = 32;
    private const float SpritePixelsPerUnit = 20f;

    private static AudioClip s_provisionalChimeClip;
    private static Sprite s_provisionalPointerSprite;
    private static Material s_provisionalUnlitMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_provisionalChimeClip = null;
        s_provisionalPointerSprite = null;
        s_provisionalUnlitMaterial = null;
    }

    [Header("Optional Custom Pointer Visual")]
    [SerializeField]
    [Tooltip("Sprite personalizado para la flecha de la brújula; si es nulo se genera un sprite rúnico nítido procedural.")]
    private Sprite _customPointerSprite;

    private GameObject _pointerObject;
    private SpriteRenderer _spriteRenderer;
    private Vector2 _targetSanctuaryPos;
    private Vector2 _currentDirection = Vector2.up;
    private float _totalDuration = 5.0f;
    private float _remainingDuration;

    public void Initialize(Vector2 sanctuaryPosition, Vector2 fallbackDirection, float duration)
    {
        _targetSanctuaryPos = sanctuaryPosition;
        if (fallbackDirection.sqrMagnitude < 0.0001f)
        {
            fallbackDirection = Vector2.up;
        }

        _currentDirection = fallbackDirection.normalized;
        _totalDuration = Mathf.Max(0.5f, duration);
        _remainingDuration = _totalDuration;

        EnsurePointerRenderer();
        PlayChimeAudio();
        UpdateTrackingAndOrientation(1f);
    }

    public void Initialize(Vector2 directionVector, float duration)
    {
        Initialize(Vector2.zero, directionVector, duration);
    }

    private void EnsurePointerRenderer()
    {
        if (_pointerObject != null && _spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;
            return;
        }

        _pointerObject = new GameObject("RuneCompassPointerVisual");
        _pointerObject.transform.SetParent(transform, false);
        _pointerObject.transform.localPosition = Vector3.zero;

        _spriteRenderer = _pointerObject.AddComponent<SpriteRenderer>();
        _spriteRenderer.sprite = _customPointerSprite != null ? _customPointerSprite : GetOrCreatePointerSprite();
        _spriteRenderer.sortingLayerName = "VFX";
        _spriteRenderer.sortingOrder = 30;

        if (s_provisionalUnlitMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                ?? Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Particles/Standard Unlit");

            if (shader != null)
            {
                s_provisionalUnlitMaterial = new Material(shader)
                {
                    name = "RuneCompassPointerUnlitMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
        }

        if (s_provisionalUnlitMaterial != null)
        {
            _spriteRenderer.sharedMaterial = s_provisionalUnlitMaterial;
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

        UpdateTrackingAndOrientation(alpha);
    }

    private void UpdateTrackingAndOrientation(float alpha)
    {
        if (_pointerObject == null || _spriteRenderer == null)
        {
            return;
        }

        Vector2 playerPos = (Vector2)transform.position;

        // Si tenemos la posición del santuario, recalculamos el vector objetivo en tiempo real
        if (_targetSanctuaryPos != Vector2.zero)
        {
            Vector2 toSanctuary = _targetSanctuaryPos - playerPos;
            if (toSanctuary.sqrMagnitude > 0.001f)
            {
                _currentDirection = toSanctuary.normalized;
            }
        }

        // Posiciona el puntero orbitando alrededor del jugador hacia el objetivo
        Vector3 orbitPos = (Vector3)playerPos + (Vector3)(_currentDirection * OrbitRadius);
        _pointerObject.transform.position = orbitPos;

        // Rota el sprite para que su vector UP (+Y) apunte directamente hacia el santuario
        float angleDegrees = Mathf.Atan2(_currentDirection.y, _currentDirection.x) * Mathf.Rad2Deg - 90f;
        _pointerObject.transform.rotation = Quaternion.Euler(0f, 0f, angleDegrees);

        // Aplica opacidad con desvanecimiento en el último segundo
        _spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
    }

    private static Sprite GetOrCreatePointerSprite()
    {
        if (s_provisionalPointerSprite != null)
        {
            return s_provisionalPointerSprite;
        }

        int size = SpriteResolution;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "RuneCompassPointerTex",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        var pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        var outlineColor = new Color(0.04f, 0.18f, 0.28f, 0.95f);     // Contorno azul oscuro rúnico
        var coreCyan = new Color(0.25f, 0.92f, 0.95f, 0.98f);         // Núcleo cian celestial brillante
        var centerGlow = new Color(0.92f, 1.0f, 0.98f, 1.0f);         // Brillo blanco cristalino central
        var runeGold = new Color(1.0f, 0.88f, 0.35f, 0.95f);          // Detalle dorado en la base y muescas

        // Dibuja una flecha/aguja rúnica simétrica orientada hacia arriba (+Y)
        // Centro X en 15.5f
        for (int y = 3; y <= 29; y++)
        {
            float halfWidth;
            if (y >= 20)
            {
                // Punta de la flecha triangular afilada (de y=20 a y=29)
                halfWidth = (29 - y) * 0.9f;
            }
            else if (y >= 14)
            {
                // Guardas laterales / alas rúnicas ensanchadas (de y=14 a y=19)
                halfWidth = 2.0f + (y - 14) * 0.7f;
            }
            else if (y >= 8)
            {
                // Eje intermedio con diamante rúnico
                halfWidth = 1.6f + (y % 2 == 0 ? 0.3f : 0f);
            }
            else
            {
                // Base rúnica
                halfWidth = 2.2f;
            }

            int minX = Mathf.Clamp(Mathf.RoundToInt(15.5f - halfWidth), 0, size - 1);
            int maxX = Mathf.Clamp(Mathf.RoundToInt(15.5f + halfWidth), 0, size - 1);

            for (int x = minX; x <= maxX; x++)
            {
                int index = y * size + x;
                bool isBorder = (x == minX || x == maxX || y == 3 || y == 29);

                if (isBorder)
                {
                    pixels[index] = outlineColor;
                }
                else if (Mathf.Abs(x - 15.5f) < 0.8f && y >= 16)
                {
                    pixels[index] = centerGlow;
                }
                else if (y <= 7 || (y >= 14 && y <= 16 && Mathf.Abs(x - 15.5f) > 2.0f))
                {
                    pixels[index] = runeGold;
                }
                else
                {
                    pixels[index] = coreCyan;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        // Pivote centrado en (0.5, 0.5) para que rote armoniosamente orbitando al jugador
        s_provisionalPointerSprite = Sprite.Create(
            texture,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            SpritePixelsPerUnit);
        s_provisionalPointerSprite.hideFlags = HideFlags.HideAndDontSave;

        return s_provisionalPointerSprite;
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
        if (_pointerObject != null)
        {
            Destroy(_pointerObject);
            _pointerObject = null;
        }

        Destroy(this);
    }
}
