using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Local-only Town progression. Feedback consumes confirmed application facts, never Results state.</summary>
[DisallowMultipleComponent]
public sealed class TownProgressionView : MonoBehaviour
{
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _statusText;
    [SerializeField] private Image _progressFill;
    [SerializeField] private RectTransform _pulseRoot;
    [SerializeField, Min(.01f)] private float _initialPulseDuration = .18f;
    [SerializeField, Min(.01f)] private float _fillDuration = 1.1f;
    [SerializeField, Min(.01f)] private float _completionPulseDuration = .24f;
    [SerializeField, Min(1f)] private float _initialPulseScale = 1.08f;
    [SerializeField, Min(1f)] private float _completionPulseScale = 1.15f;

    private readonly Queue<ExperienceApplicationResult> _pending = new();
    private ExperienceCurve _curve;
    private ExperienceApplicationResult _active;
    private TownProgressionPresentation _latest;
    private bool _animating;
    private float _elapsed;
    private Vector3 _baseScale;
    private enum AnimationPhase { InitialPulse, Fill, CompletedLevelPulse }
    private AnimationPhase _phase;
    private int _displayLevel;
    private long _segmentStartExperience;

    public TMP_Text LevelText => _levelText;
    public TMP_Text StatusText => _statusText;
    public Image ProgressFill => _progressFill;

    private void Awake()
    {
        if (_pulseRoot != null) _baseScale = _pulseRoot.localScale;
        else Debug.LogError($"{nameof(TownProgressionView)} requires its serialized pulse root.", this);
    }

    public void Present(in TownProgressionPresentation presentation)
    {
        _latest = presentation;
        if (!_animating) Render(presentation);
        gameObject.SetActive(true);
    }

    public void EnqueueConfirmedFeedback(ExperienceCurve curve, ExperienceApplicationResult result)
    {
        if (!TownProgressionFeedbackAnimation.TrySample(curve, result, 0, out _)) return;
        _curve = curve;
        _pending.Enqueue(result);
        if (!_animating) BeginNext();
    }

    private void BeginNext()
    {
        if (_pending.Count == 0)
        {
            _animating = false;
            if (_pulseRoot != null) _pulseRoot.localScale = _baseScale;
            Render(_latest);
            return;
        }
        _active = _pending.Dequeue();
        _elapsed = 0;
        _animating = true;
        _phase = AnimationPhase.InitialPulse;
        _displayLevel = _active.PreviousLevel;
        _segmentStartExperience = _active.PreviousExperience;
        if (TownProgressionFeedbackAnimation.TrySample(_curve, _active, 0, out var start)) Render(start);
    }

    private void Update()
    {
        if (!_animating) return;
        _elapsed += Time.unscaledDeltaTime;
        if (_phase == AnimationPhase.InitialPulse)
        {
            Pulse(_initialPulseDuration, _initialPulseScale);
            if (_elapsed >= Mathf.Max(.01f, _initialPulseDuration)) NextPhase(AnimationPhase.Fill);
            return;
        }
        if (_phase == AnimationPhase.CompletedLevelPulse)
        {
            Pulse(_completionPulseDuration, _completionPulseScale);
            if (_elapsed < Mathf.Max(.01f, _completionPulseDuration)) return;
            _displayLevel++;
            _segmentStartExperience = 0;
            if (_displayLevel >= _curve.MaximumLevel ||
                _displayLevel == _active.ResultingLevel && _active.ResultingExperience == 0)
                BeginNext();
            else NextPhase(AnimationPhase.Fill);
            return;
        }

        if (!_curve.TryGetRequiredExperience(_displayLevel, out long required))
        {
            BeginNext();
            return;
        }
        bool completesLevel = _displayLevel < _active.ResultingLevel;
        long target = completesLevel ? required : _active.ResultingExperience;
        float progress = Mathf.Clamp01(_elapsed / Mathf.Max(.01f, _fillDuration));
        long experience = _segmentStartExperience +
            (long)((target - _segmentStartExperience) * (double)progress);
        if (TownProgressionPresentation.TryCreate(_curve, _displayLevel,
                System.Math.Min(required - 1, experience), out var frame)) Render(frame);
        if (progress < 1f) return;
        if (completesLevel)
        {
            // Hold a visibly full bar for this level's pulse before resetting it for the next level.
            if (_progressFill != null) _progressFill.fillAmount = 1f;
            NextPhase(AnimationPhase.CompletedLevelPulse);
        }
        else BeginNext();
    }

    private void Pulse(float duration, float scale)
    {
        if (_pulseRoot == null) return;
        float t = Mathf.Clamp01(_elapsed / Mathf.Max(.01f, duration));
        _pulseRoot.localScale = _baseScale * (1 + (scale - 1) * Mathf.Sin(Mathf.PI * t));
    }

    private void NextPhase(AnimationPhase phase)
    {
        _phase = phase;
        _elapsed = 0;
        if (_pulseRoot != null) _pulseRoot.localScale = _baseScale;
    }
    private void Render(in TownProgressionPresentation presentation)
    {
        if (_levelText != null) _levelText.text = $"Nivel {presentation.Level}";
        if (_statusText != null)
            _statusText.text = presentation.IsMaximumLevel ? "Nivel máximo"
                : $"XP {presentation.CurrentExperience} / {presentation.RequiredExperience}";
        if (_progressFill != null) _progressFill.fillAmount = presentation.NormalizedProgress;
    }

    // Menu visibility pauses Update without discarding the queued confirmed transition.
    // Only destruction/unbinding ends this view's lifecycle; showing the menu must not consume it again.
    public void Hide() => gameObject.SetActive(false);
}
