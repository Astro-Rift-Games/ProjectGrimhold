using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// uGUI mesh renderer for a generated <see cref="MinimapLayout"/>. It draws static schematic
/// cells only and remains independent from gameplay, networking and scene discovery.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMinimapGraphic : MaskableGraphic
{
    [SerializeField]
    private MinimapLayout _layout;

    [SerializeField]
    private Color _floorColor = new Color(0.42f, 0.48f, 0.55f, 1f);

    [SerializeField]
    private Color _wallColor = new Color(0.12f, 0.15f, 0.19f, 1f);

    [SerializeField]
    private Color _obstacleColor = new Color(0.55f, 0.35f, 0.17f, 1f);

    private float _uiUnitsPerWorldUnit = 1f;

    /// <summary>Gets the generated layout consumed by this graphic.</summary>
    public MinimapLayout Layout => _layout;

    /// <summary>Gets the world pivot encoded by the generated layout.</summary>
    public Vector2 WorldPivotPosition => _layout != null
        ? _layout.WorldPivotPosition
        : Vector2.zero;

    /// <summary>
    /// Configures the local UI scale of the generated mesh and returns the matching RectTransform size.
    /// </summary>
    public bool TryConfigure(float uiUnitsPerWorldUnit, out Vector2 uiSize, out string error)
    {
        uiSize = Vector2.zero;
        error = null;
        if (_layout == null)
        {
            error = "RaidMinimapGraphic has no MinimapLayout reference.";
            return false;
        }

        if (!_layout.TryValidate(out error))
        {
            return false;
        }

        if (!IsPositiveFinite(uiUnitsPerWorldUnit))
        {
            error = "RaidMinimapGraphic requires positive UI units per world unit.";
            return false;
        }

        _uiUnitsPerWorldUnit = uiUnitsPerWorldUnit;
        uiSize = _layout.WorldSize * _uiUnitsPerWorldUnit;
        if (!IsPositiveFinite(uiSize.x) || !IsPositiveFinite(uiSize.y))
        {
            error = "RaidMinimapGraphic produced an invalid UI size.";
            return false;
        }

        SetVerticesDirty();
        error = null;
        return true;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (_layout == null || !_layout.TryValidate(out _) ||
            !IsPositiveFinite(_uiUnitsPerWorldUnit))
        {
            return;
        }

        Vector2 cellSize = _layout.CellSize * _uiUnitsPerWorldUnit;
        Vector2 totalSize = _layout.WorldSize * _uiUnitsPerWorldUnit;
        Vector2 start = -totalSize * 0.5f;
        Vector2Int minimumCell = _layout.MinimumCell;
        Vector2Int size = _layout.SizeInCells;

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                MinimapLayoutCellFlags flags = _layout.GetCellFlags(
                    minimumCell + new Vector2Int(x, y));
                if (flags == MinimapLayoutCellFlags.None)
                {
                    continue;
                }

                Vector2 bottomLeft = start + new Vector2(x * cellSize.x, y * cellSize.y);
                if ((flags & MinimapLayoutCellFlags.Floor) != 0)
                {
                    AddQuad(vertexHelper, bottomLeft, cellSize, _floorColor);
                }

                if ((flags & MinimapLayoutCellFlags.Wall) != 0)
                {
                    AddQuad(vertexHelper, bottomLeft, cellSize, _wallColor);
                }

                if ((flags & MinimapLayoutCellFlags.Obstacle) != 0)
                {
                    AddQuad(vertexHelper, bottomLeft, cellSize, _obstacleColor);
                }
            }
        }
    }

    private static void AddQuad(
        VertexHelper vertexHelper,
        Vector2 bottomLeft,
        Vector2 size,
        Color color)
    {
        int startIndex = vertexHelper.currentVertCount;
        vertexHelper.AddVert(bottomLeft, color, Vector2.zero);
        vertexHelper.AddVert(bottomLeft + new Vector2(0f, size.y), color, Vector2.up);
        vertexHelper.AddVert(bottomLeft + size, color, Vector2.one);
        vertexHelper.AddVert(bottomLeft + new Vector2(size.x, 0f), color, Vector2.right);
        vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
        vertexHelper.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
    }

    private static bool IsPositiveFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
