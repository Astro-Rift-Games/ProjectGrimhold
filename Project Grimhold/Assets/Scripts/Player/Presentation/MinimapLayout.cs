using System;
using UnityEngine;

/// <summary>
/// Immutable runtime description of the graybox cells rendered by the local minimap.
/// The editor generator is its only writer; gameplay and network simulation never mutate it.
/// </summary>
public sealed class MinimapLayout : ScriptableObject
{
    [SerializeField]
    private Vector2Int _minimumCell;

    [SerializeField]
    private Vector2Int _sizeInCells;

    [SerializeField]
    private Vector2 _cellSize;

    [SerializeField]
    private Vector2 _worldPivotPosition;

    [SerializeField]
    [TextArea]
    private string _encodedCellFlags;

    [SerializeField]
    private string _sourceHash;

    private byte[] _cellFlags;

    /// <summary>Gets the lowest serialized grid coordinate included by this layout.</summary>
    public Vector2Int MinimumCell => _minimumCell;

    /// <summary>Gets the layout dimensions in grid cells.</summary>
    public Vector2Int SizeInCells => _sizeInCells;

    /// <summary>Gets the world-space size of one grid cell.</summary>
    public Vector2 CellSize => _cellSize;

    /// <summary>Gets the world-space position represented by the centered minimap pivot.</summary>
    public Vector2 WorldPivotPosition => _worldPivotPosition;

    /// <summary>Gets the deterministic signature of the Floor, Walls and Obstacles source cells.</summary>
    public string SourceHash => _sourceHash;

    /// <summary>Gets the total layout size in world units.</summary>
    public Vector2 WorldSize => Vector2.Scale(_sizeInCells, _cellSize);

    /// <summary>
    /// Returns the schematic flags for one absolute grid cell. Cells outside the generated
    /// bounds are empty and never cause a fallback representation.
    /// </summary>
    public MinimapLayoutCellFlags GetCellFlags(Vector2Int cell)
    {
        int x = cell.x - _minimumCell.x;
        int y = cell.y - _minimumCell.y;
        if (x < 0 || y < 0 || x >= _sizeInCells.x || y >= _sizeInCells.y)
        {
            return MinimapLayoutCellFlags.None;
        }

        EnsureDecodedCellFlags();
        int index = y * _sizeInCells.x + x;
        return index >= 0 && index < _cellFlags.Length
            ? (MinimapLayoutCellFlags)_cellFlags[index]
            : MinimapLayoutCellFlags.None;
    }

    /// <summary>Validates the serialized layout before a view consumes its geometry.</summary>
    public bool TryValidate(out string error)
    {
        if (_sizeInCells.x <= 0 || _sizeInCells.y <= 0 ||
            !IsPositiveFinite(_cellSize.x) || !IsPositiveFinite(_cellSize.y) ||
            !IsFinite(_worldPivotPosition.x) || !IsFinite(_worldPivotPosition.y))
        {
            error = "MinimapLayout has invalid bounds, cell size or world pivot.";
            return false;
        }

        long expectedLength = (long)_sizeInCells.x * _sizeInCells.y;
        EnsureDecodedCellFlags();
        if (_cellFlags == null || _cellFlags.Length != expectedLength ||
            string.IsNullOrEmpty(_sourceHash))
        {
            error = "MinimapLayout has incomplete generated cell data.";
            return false;
        }

        error = null;
        return true;
    }

#if UNITY_EDITOR
    /// <summary>Writes editor-generated data that has already been validated from the source Tilemaps.</summary>
    public void ApplyGeneratedData(
        Vector2Int minimumCell,
        Vector2Int sizeInCells,
        Vector2 cellSize,
        Vector2 worldPivotPosition,
        byte[] cellFlags,
        string sourceHash)
    {
        _minimumCell = minimumCell;
        _sizeInCells = sizeInCells;
        _cellSize = cellSize;
        _worldPivotPosition = worldPivotPosition;
        _encodedCellFlags = EncodeCellFlags(cellFlags);
        _cellFlags = cellFlags;
        _sourceHash = sourceHash;
    }
#endif

    private void EnsureDecodedCellFlags()
    {
        if (_cellFlags != null || string.IsNullOrEmpty(_encodedCellFlags) ||
            _sizeInCells.x <= 0 || _sizeInCells.y <= 0)
        {
            return;
        }

        int expectedLength = _sizeInCells.x * _sizeInCells.y;
        byte[] decoded = new byte[expectedLength];
        int destinationIndex = 0;
        string[] runs = _encodedCellFlags.Split(',');
        for (int runIndex = 0; runIndex < runs.Length; runIndex++)
        {
            string[] parts = runs[runIndex].Split(':');
            if (parts.Length != 2 || !byte.TryParse(parts[0], out byte value) ||
                !int.TryParse(parts[1], out int count) || count <= 0 ||
                destinationIndex > expectedLength - count)
            {
                _cellFlags = null;
                return;
            }

            for (int cellIndex = 0; cellIndex < count; cellIndex++)
            {
                decoded[destinationIndex++] = value;
            }
        }

        _cellFlags = destinationIndex == expectedLength ? decoded : null;
    }

#if UNITY_EDITOR
    private static string EncodeCellFlags(byte[] cellFlags)
    {
        if (cellFlags == null || cellFlags.Length == 0)
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(cellFlags.Length);
        byte value = cellFlags[0];
        int count = 0;
        for (int index = 0; index < cellFlags.Length; index++)
        {
            if (cellFlags[index] == value && count < ushort.MaxValue)
            {
                count++;
                continue;
            }

            AppendRun(builder, value, count);
            value = cellFlags[index];
            count = 1;
        }

        AppendRun(builder, value, count);
        return builder.ToString();
    }

    private static void AppendRun(System.Text.StringBuilder builder, byte value, int count)
    {
        if (builder.Length > 0)
        {
            builder.Append(',');
        }

        builder.Append(value);
        builder.Append(':');
        builder.Append(count);
    }
#endif

    private static bool IsPositiveFinite(float value)
    {
        return IsFinite(value) && value > 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}

/// <summary>Layered schematic content derived from the authorized graybox Tilemaps.</summary>
[Flags]
public enum MinimapLayoutCellFlags : byte
{
    None = 0,
    Floor = 1 << 0,
    Wall = 1 << 1,
    Obstacle = 1 << 2
}
