#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Single editor-thread scheduler. A render is non-preemptible; budget is checked between
/// renders. Owns returned textures, evicts per texture, and never retains evicted native resources.</summary>
public sealed class AttackPreviewFrameCache : IDisposable
{
    private readonly Func<AttackPreviewCombination, float, Texture2D> _render;
    private readonly Func<double> _clock;
    private readonly int _samples, _maxTextures;
    private readonly float _tail;
    private readonly Queue<(AttackPreviewCombination cell, int sample, float seconds)> _pending = new Queue<(AttackPreviewCombination, int, float)>();
    private readonly HashSet<(string, int)> _queued = new HashSet<(string, int)>();
    private readonly Dictionary<(string, int), (Texture2D texture, LinkedListNode<(string, int)> node)> _textures =
        new Dictionary<(string, int), (Texture2D, LinkedListNode<(string, int)>)>();
    private readonly LinkedList<(string, int)> _lru = new LinkedList<(string, int)>();
    private bool _disposed;
    public AttackPreviewFrameCache(Func<AttackPreviewCombination, float, Texture2D> render,
        Func<double> clockMilliseconds, int sampleCount = 16, int maxTextures = 256, float tailSeconds = 0.1f)
    {
        _render = render ?? throw new ArgumentNullException(nameof(render));
        _clock = clockMilliseconds ?? throw new ArgumentNullException(nameof(clockMilliseconds));
        if (sampleCount < 2 || maxTextures < 1 || !float.IsFinite(tailSeconds) || tailSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(sampleCount));
        _samples = sampleCount; _maxTextures = maxTextures; _tail = tailSeconds;
    }
    public void Request(AttackPreviewCombination cell, float durationSeconds)
    {
        ThrowIfDisposed();
        if (cell == null) throw new ArgumentNullException(nameof(cell));
        if (!float.IsFinite(durationSeconds) || durationSeconds < 0) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        for (int i = 0; i < _samples; i++)
        {
            var key = (cell.Id, i);
            if (_textures.ContainsKey(key) || !_queued.Add(key)) continue;
            _pending.Enqueue((cell, i, (durationSeconds + _tail) * i / (_samples - 1)));
        }
    }
    public Texture2D Get(string cellId, float normalizedTime)
    {
        ThrowIfDisposed();
        int sample = Mathf.RoundToInt(Mathf.Clamp01(normalizedTime) * (_samples - 1));
        if (!_textures.TryGetValue((cellId, sample), out var entry)) return null;
        _lru.Remove(entry.node); _lru.AddLast(entry.node);
        return entry.texture;
    }
    public int TextureCount => _textures.Count;
    public int PendingCount => _pending.Count;
    public void Tick(double budgetMilliseconds = 8)
    {
        ThrowIfDisposed();
        double start = _clock();
        while (_pending.Count > 0 && _clock() - start < budgetMilliseconds)
        {
            var work = _pending.Dequeue();
            var key = (work.cell.Id, work.sample);
            _queued.Remove(key);
            var texture = _render(work.cell, work.seconds);
            if (texture == null) continue;
            while (_textures.Count >= _maxTextures)
            {
                var oldest = _lru.First;
                Object.DestroyImmediate(_textures[oldest.Value].texture);
                _textures.Remove(oldest.Value); _lru.RemoveFirst();
            }
            var node = _lru.AddLast(key);
            _textures.Add(key, (texture, node));
        }
    }
    public void Invalidate()
    {
        foreach (var entry in _textures.Values) if (entry.texture != null) Object.DestroyImmediate(entry.texture);
        _textures.Clear(); _lru.Clear(); _queued.Clear(); _pending.Clear();
    }
    private void ThrowIfDisposed()
    { if (_disposed) throw new ObjectDisposedException(nameof(AttackPreviewFrameCache)); }
    public void Dispose()
    {
        if (_disposed) return;
        Invalidate(); _disposed = true;
    }
}
#endif
