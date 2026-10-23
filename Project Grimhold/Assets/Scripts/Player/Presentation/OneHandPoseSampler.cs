using System;
using UnityEngine;

/// <summary>Samples existing directional art on an inert, owned rig without rewriting clips or the live Animator.</summary>
public sealed class OneHandPoseSampler : IDisposable
{
    private readonly GameObject _root;
    private readonly Transform _source;
    private readonly Transform _grip;
    private readonly SpriteRenderer _hand;
    private readonly SpriteRenderer _vfx;

    public OneHandPoseSampler()
    {
        _root = new GameObject("OneHandPoseSample") { hideFlags = HideFlags.HideAndDontSave };
        // Object-reference curves (sprites) require an Animator even when SampleAnimation is called explicitly.
        _root.AddComponent<Animator>();
        var pivot = new GameObject("RightHandPivot").transform;
        pivot.SetParent(_root.transform, false);
        _source = new GameObject("RightHand").transform;
        _source.SetParent(pivot, false);
        _hand = _source.gameObject.AddComponent<SpriteRenderer>();
        _hand.enabled = false;
        _grip = new GameObject("MainHandGrip").transform;
        _grip.SetParent(_source, false);
        var vfx = new GameObject("AttackVfx");
        vfx.transform.SetParent(_root.transform, false);
        _vfx = vfx.AddComponent<SpriteRenderer>();
        _vfx.enabled = false;
    }

    public void Sample(AnimationClip clip, float seconds, Transform destinationHand, Transform destinationGrip, SpriteRenderer renderer)
    {
        clip.SampleAnimation(_root, seconds);
        _hand.enabled = false;
        _vfx.enabled = false;
        // Directional trajectories already express the requested attack direction in VisualRoot space.
        destinationHand.SetLocalPositionAndRotation(_source.localPosition, _source.localRotation);
        destinationHand.localScale = _source.localScale;
        destinationGrip.SetLocalPositionAndRotation(_grip.localPosition, _grip.localRotation);
        if (renderer != null && _hand.sprite != null)
        {
            renderer.sprite = _hand.sprite;
            renderer.sortingOrder = _hand.sortingOrder;
        }
    }

    public Sprite SampleVfx(AnimationClip clip, float seconds)
    {
        clip.SampleAnimation(_root, seconds);
        _hand.enabled = false;
        _vfx.enabled = false;
        return _vfx.sprite;
    }

    public void Dispose()
    {
        if (_root == null) return;
        if (Application.isPlaying) UnityEngine.Object.Destroy(_root);
        else UnityEngine.Object.DestroyImmediate(_root);
    }
}
