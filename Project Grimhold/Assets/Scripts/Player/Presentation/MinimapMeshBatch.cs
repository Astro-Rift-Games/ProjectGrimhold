using System;
using UnityEngine;

/// <summary>Immutable, texture-contiguous tile geometry generated in the Editor.</summary>
[Serializable]
public sealed class MinimapMeshBatch
{
    // VertexHelper uses a 16-bit mesh. Leave headroom rather than truncating a large dungeon.
    public const int MaxVertexCount = 60000;

    [SerializeField] private Texture2D _texture;
    [SerializeField] private Vector2[] _positions;
    [SerializeField] private Vector2[] _uvs;
    [SerializeField] private Color32[] _colors;
    [SerializeField] private int[] _indices;

    public Texture2D Texture => _texture;
    public int VertexCount => _positions?.Length ?? 0;
    public int IndexCount => _indices?.Length ?? 0;
    public Vector2 GetPosition(int index) => _positions[index];
    public Vector2 GetUv(int index) => _uvs[index];
    public Color32 GetColor(int index) => _colors[index];
    public int GetIndex(int index) => _indices[index];

    public MinimapMeshBatch(Texture2D texture, Vector2[] positions, Vector2[] uvs,
        Color32[] colors, int[] indices)
    {
        _texture = texture;
        _positions = positions;
        _uvs = uvs;
        _colors = colors;
        _indices = indices;
    }

    public bool TryValidate(out string error)
    {
        if (_texture == null || VertexCount < 3 || VertexCount > MaxVertexCount ||
            _uvs == null || _uvs.Length != VertexCount || _colors == null ||
            _colors.Length != VertexCount || IndexCount == 0 || IndexCount % 3 != 0)
        {
            error = "Minimap tile-art batch has missing texture or invalid mesh buffers.";
            return false;
        }

        for (int i = 0; i < VertexCount; i++)
        {
            if (!IsFinite(_positions[i]) || !IsFinite(_uvs[i]))
            {
                error = "Minimap tile-art batch contains non-finite geometry or UVs.";
                return false;
            }
        }

        for (int i = 0; i < IndexCount; i++)
        {
            if (_indices[i] < 0 || _indices[i] >= VertexCount)
            {
                error = "Minimap tile-art batch contains an out-of-range triangle index.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y);
    }
}
