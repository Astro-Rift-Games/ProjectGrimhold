using System.Collections.Generic;
using UnityEngine;

/// <summary>Controller-owned local target pause. Restores pooled/inactive Animators as well.</summary>
public sealed class HitstopTargetFreeze
{
    private readonly List<Animator> _animators = new();
    private readonly List<float> _speeds = new();
    private float _delaySeconds;
    private float _holdSeconds;
    private bool _frozen;
    public bool IsActive => _holdSeconds > 0f;

    public void Freeze(Transform targetRoot, float delaySeconds, float holdSeconds)
    {
        Restore();
        if (targetRoot == null || float.IsNaN(delaySeconds) || float.IsInfinity(delaySeconds) ||
            float.IsNaN(holdSeconds) || float.IsInfinity(holdSeconds) || holdSeconds <= 0f) return;
        targetRoot.GetComponentsInChildren(true, _animators);
        for (int i = 0; i < _animators.Count; i++) _speeds.Add(_animators[i].speed);
        _delaySeconds = Mathf.Max(0f, delaySeconds);
        _holdSeconds = holdSeconds;
        if (_delaySeconds <= 0f) ApplyFreeze();
    }

    public void Tick(float deltaSeconds)
    {
        if (_holdSeconds <= 0f) return;
        float elapsed = Mathf.Max(0f, deltaSeconds);
        if (_delaySeconds > 0f)
        {
            float delay = Mathf.Min(elapsed, _delaySeconds);
            _delaySeconds -= delay;
            elapsed -= delay;
            if (_delaySeconds > 0f) return;
        }
        _holdSeconds -= elapsed;
        if (_holdSeconds <= 0f) Restore();
        else if (!_frozen) ApplyFreeze();
    }

    public void Restore()
    {
        if (_frozen)
            for (int i = 0; i < _animators.Count; i++)
                if (_animators[i] != null) _animators[i].speed = _speeds[i];
        _animators.Clear();
        _speeds.Clear();
        _delaySeconds = 0f;
        _holdSeconds = 0f;
        _frozen = false;
    }

    private void ApplyFreeze()
    {
        for (int i = 0; i < _animators.Count; i++)
        {
            if (_animators[i] == null) continue;
            // Capture at the actual pause, not earlier while a delayed confirmation waits for contact.
            _speeds[i] = _animators[i].speed;
            _animators[i].speed = 0f;
        }
        _frozen = true;
    }
}
