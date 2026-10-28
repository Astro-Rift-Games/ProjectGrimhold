using UnityEngine;

/// <summary>
/// Componente de presentación temporal que resalta visualmente un objeto detectado por el Detector Arcano.
/// Se destruye automáticamente al expirar su duración restaurando el color previo.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArcaneHighlightEffect : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Color _originalColor = Color.white;
    private float _remainingDuration;
    private bool _initialized;

    public void Initialize(float duration, Color highlightColor)
    {
        if (!_initialized)
        {
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer != null)
            {
                _originalColor = _renderer.color;
            }
            _initialized = true;
        }

        if (_renderer != null)
        {
            _renderer.color = highlightColor;
        }

        _remainingDuration = Mathf.Max(_remainingDuration, duration);
    }

    private void Update()
    {
        _remainingDuration -= Time.deltaTime;
        if (_remainingDuration <= 0f)
        {
            RestoreAndCleanup();
        }
    }

    private void OnDisable()
    {
        RestoreAndCleanup();
    }

    private void OnDestroy()
    {
        RestoreAndCleanup();
    }

    private void RestoreAndCleanup()
    {
        if (_renderer != null && _initialized)
        {
            _renderer.color = _originalColor;
        }

        Destroy(this);
    }
}
