using UnityEngine;

/// <summary>Owns only captured visual state. Independent of Animators, Fusion and authoritative transforms.</summary>
public sealed class HitReactionPlayback
{
    private readonly Transform _visual;
    private readonly SpriteRenderer[] _renderers;
    private readonly Vector3 _basePosition;
    private readonly Vector3 _baseScale;
    private readonly Color[] _baseColors;
    private readonly float _retriggerGuardSeconds;
    private MeleeHitReactionProfile _profile;
    private Vector2 _direction;
    private float _elapsed;
    private float _delay;
    private float _hold;
    private float _sinceLastPlay = float.PositiveInfinity;

    public bool IsActive => _profile != null;

    public HitReactionPlayback(Transform visual, SpriteRenderer[] renderers, float retriggerGuardSeconds = .05f)
    {
        _visual = visual;
        _renderers = renderers != null ? (SpriteRenderer[])renderers.Clone() : new SpriteRenderer[0];
        _baseColors = new Color[_renderers.Length];
        _retriggerGuardSeconds = Mathf.Max(0f, retriggerGuardSeconds);
        if (_visual != null)
        {
            _basePosition = _visual.localPosition;
            _baseScale = _visual.localScale;
        }
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _baseColors[i] = _renderers[i].color;
    }

    /// <summary>True also for a guarded hit: it is absorbed without restarting the current contact.</summary>
    public bool Play(MeleeHitReactionProfile profile, Vector2 direction, float delaySeconds, float holdSeconds)
    {
        if (profile == null || !profile.TryValidate(out _) || !IsFinite(delaySeconds) || !IsFinite(holdSeconds)) return false;
        if (IsActive && _sinceLastPlay < _retriggerGuardSeconds) return true;
        RestoreVisuals();
        _profile = profile;
        _direction = HitReactionMath.NormalizeDirection(direction);
        if (_visual != null && _visual.parent != null)
            _direction = HitReactionMath.NormalizeDirection(_visual.parent.InverseTransformVector(_direction));
        _delay = Mathf.Max(0f, delaySeconds);
        _hold = Mathf.Max(0f, holdSeconds);
        _elapsed = 0f;
        _sinceLastPlay = 0f;
        Apply();
        return true;
    }

    /// <summary>Suppresses only the nearby Health observation, not subsequent unrelated damage.</summary>
    public bool AbsorbsHealthFeedback(float mergeWindowSeconds) => _sinceLastPlay <= mergeWindowSeconds;

    public void Tick(float deltaSeconds)
    {
        if (!IsFinite(deltaSeconds) || deltaSeconds < 0f) return;
        _sinceLastPlay += deltaSeconds;
        if (!IsActive) return;
        _elapsed += deltaSeconds;
        Apply();
    }

    public void Restore()
    {
        RestoreVisuals();
        _profile = null;
        _elapsed = 0f;
        _sinceLastPlay = float.PositiveInfinity;
    }

    private void Apply()
    {
        if (_elapsed < _delay) return;
        float recovery = Mathf.Max(0f, _elapsed - _delay - _hold) / _profile.RecoverySeconds;
        if (recovery >= 1f)
        {
            // Keep the short Health merge window even when a light reaction has already finished.
            RestoreVisuals();
            _profile = null;
            return;
        }
        float weight = (1f - recovery) * (1f - recovery) * (1f - recovery);
        if (_visual != null)
        {
            _visual.localPosition = _basePosition + HitReactionMath.Offset(_direction,
                _profile.NudgePixels, _profile.PixelsPerUnit, weight);
            _visual.localScale = Vector3.Scale(_baseScale, HitReactionMath.Scale(_direction,
                _profile.Compression, _profile.StretchRatio, weight));
        }
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null)
                _renderers[i].color = Color.Lerp(_baseColors[i], _profile.FlashColor, _profile.FlashStrength * weight);
    }

    private void RestoreVisuals()
    {
        if (_visual != null)
        {
            _visual.localPosition = _basePosition;
            _visual.localScale = _baseScale;
        }
        for (int i = 0; i < _renderers.Length; i++)
            if (_renderers[i] != null) _renderers[i].color = _baseColors[i];
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
