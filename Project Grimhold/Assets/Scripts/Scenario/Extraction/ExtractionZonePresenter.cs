using UnityEngine;

/// <summary>
/// Optional presentation adapter for authored zone variants.
/// The Sanctuary prefab does not use this component: its visual renderer is
/// owned exclusively by <see cref="ExtractionSanctuaryPresenter"/>.
/// </summary>
[DisallowMultipleComponent]
public sealed class ExtractionZonePresenter : MonoBehaviour
{
    [SerializeField]
    private ExtractionZone _extractionZone;

    [SerializeField]
    private Collider2D _zoneCollider;

    [SerializeField]
    private SpriteRenderer _spriteRenderer;

    private Sprite _originalSprite;
    private Color _originalColor;
    private SpriteDrawMode _originalDrawMode;
    private Vector2 _originalSize;
    private bool _captured;
    private Sprite _runtimeFallbackSprite;

    private void Awake()
    {
        CacheReferences();
        CaptureOriginalState();
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureOriginalState();
        ApplyCurrentState();
    }

    private void LateUpdate()
    {
        ApplyCurrentState();
    }

    private void OnDisable()
    {
        RestoreOriginalState();
    }

    private void OnDestroy()
    {
        RestoreOriginalState();
    }

    private void CacheReferences()
    {
        if (_extractionZone == null)
        {
            _extractionZone = GetComponent<ExtractionZone>();
        }

        if (_zoneCollider == null)
        {
            _zoneCollider = GetComponent<Collider2D>();
        }

        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    private void CaptureOriginalState()
    {
        if (_captured || _spriteRenderer == null)
        {
            return;
        }

        _originalSprite = _spriteRenderer.sprite;
        _originalColor = _spriteRenderer.color;
        _originalDrawMode = _spriteRenderer.drawMode;
        _originalSize = _spriteRenderer.size;
        _captured = true;
    }

    private void ApplyCurrentState()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        CaptureOriginalState();
        if (_spriteRenderer.sprite == null)
        {
            _spriteRenderer.sprite = GetFallbackSprite();
        }

        bool available = _extractionZone != null && _extractionZone.IsAvailable;
        Color color = _originalColor;
        color.r = available ? 0.2f : 0.9f;
        color.g = available ? 0.9f : 0.2f;
        color.b = available ? 0.4f : 0.2f;
        _spriteRenderer.color = color;

        if (_zoneCollider is BoxCollider2D boxCollider)
        {
            _spriteRenderer.drawMode = SpriteDrawMode.Sliced;
            _spriteRenderer.size = boxCollider.size;
        }
    }

    private void RestoreOriginalState()
    {
        if (!_captured || _spriteRenderer == null)
        {
            return;
        }

        _spriteRenderer.sprite = _originalSprite;
        _spriteRenderer.color = _originalColor;
        _spriteRenderer.drawMode = _originalDrawMode;
        _spriteRenderer.size = _originalSize;
    }

    private Sprite GetFallbackSprite()
    {
        if (_runtimeFallbackSprite == null)
        {
            _runtimeFallbackSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        return _runtimeFallbackSprite;
    }
}
