using System;
using UnityEngine;

/// <summary>Samples generated left-hand art and owns only a transient overlay, never the live Animator.</summary>
public sealed class OneHandPoseSampler : IDisposable
{
    private readonly GameObject _root;
    private readonly Transform _source;
    private readonly Transform _grip;
    private readonly SpriteRenderer _hand;
    private readonly SpriteRenderer _vfx;
    private Transform _destinationHand, _destinationGrip;
    private SpriteRenderer _destinationRenderer;
    private PoseState _baseHand, _baseGrip;
    private Sprite _baseSprite;
    private int _baseSortingOrder;
    private bool _applied;

    public OneHandPoseSampler()
    {
        _root = new GameObject("OneHandPoseSample") { hideFlags = HideFlags.HideAndDontSave };
        // Object-reference curves (sprites) require an Animator even when SampleAnimation is called explicitly.
        _root.AddComponent<Animator>();
        var pivot = new GameObject("LeftHandPivot").transform;
        pivot.SetParent(_root.transform, false);
        _source = new GameObject("LeftHand").transform;
        _source.SetParent(pivot, false);
        _hand = _source.gameObject.AddComponent<SpriteRenderer>();
        _hand.enabled = false;
        _grip = new GameObject("OffHandGrip").transform;
        _grip.SetParent(_source, false);
        var vfx = new GameObject("AttackVfx");
        vfx.transform.SetParent(_root.transform, false);
        _vfx = vfx.AddComponent<SpriteRenderer>();
        _vfx.enabled = false;
    }

    public void Sample(AnimationClip clip, float seconds, Transform destinationHand, Transform destinationGrip, SpriteRenderer renderer)
    {
        Restore();
        if (clip == null || destinationHand == null || destinationGrip == null) return;
        _destinationHand = destinationHand;
        _destinationGrip = destinationGrip;
        _destinationRenderer = renderer;
        _baseHand = new PoseState(destinationHand);
        _baseGrip = new PoseState(destinationGrip);
        if (renderer != null) { _baseSprite = renderer.sprite; _baseSortingOrder = renderer.sortingOrder; }
        ResetRig();
        clip.SampleAnimation(_root, seconds);
        _hand.enabled = false;
        _vfx.enabled = false;
        // The generator has already retargeted both the trajectory and the drawn left grip.
        destinationHand.SetLocalPositionAndRotation(_source.localPosition, _source.localRotation);
        destinationHand.localScale = _source.localScale;
        destinationGrip.SetLocalPositionAndRotation(_grip.localPosition, _grip.localRotation);
        destinationGrip.localScale = _grip.localScale;
        if (renderer != null && _hand.sprite != null)
        {
            renderer.sprite = _hand.sprite;
            renderer.sortingOrder = _hand.sortingOrder;
        }
        _applied = true;
    }

    /// <summary>Release before the next Animator evaluation, or immediately on disable/cancellation.</summary>
    public void Restore()
    {
        if (!_applied) return;
        if (_destinationHand != null) _baseHand.Apply(_destinationHand);
        if (_destinationGrip != null) _baseGrip.Apply(_destinationGrip);
        if (_destinationRenderer != null)
        {
            _destinationRenderer.sprite = _baseSprite;
            _destinationRenderer.sortingOrder = _baseSortingOrder;
        }
        // enabled belongs to armor/downed visibility, not to this overlay.
        _applied = false;
    }

    private void ResetRig()
    {
        _source.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        _source.localScale = Vector3.one;
        _grip.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        _grip.localScale = Vector3.one;
        _hand.sprite = null;
        _hand.sortingOrder = 0;
        _hand.enabled = false;
        _vfx.sprite = null;
        _vfx.enabled = false;
        _vfx.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        _vfx.transform.localScale = Vector3.one;
    }

    public Sprite SampleVfx(AnimationClip clip, float seconds)
    {
        ResetRig();
        clip.SampleAnimation(_root, seconds);
        _hand.enabled = false;
        _vfx.enabled = false;
        return _vfx.sprite;
    }

    public void Dispose()
    {
        Restore();
        if (_root == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
        else UnityEngine.Object.DestroyImmediate(_root);
    }

    private readonly struct PoseState
    {
        private readonly Vector3 _position, _scale;
        private readonly Quaternion _rotation;
        public PoseState(Transform t) { _position = t.localPosition; _rotation = t.localRotation; _scale = t.localScale; }
        public void Apply(Transform t) { t.SetLocalPositionAndRotation(_position, _rotation); t.localScale = _scale; }
    }
}
