using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// uGUI mesh renderer for a generated <see cref="MinimapLayout"/>. It draws static textured
/// tile art only and remains independent from gameplay, networking and scene discovery.
/// </summary>
[DisallowMultipleComponent]
public sealed class RaidMinimapGraphic : MaskableGraphic
{
    [SerializeField]
    private MinimapLayout _layout;

    // Retain legacy serialized fields so existing HUD prefab overrides need no migration.
    // Tile colours now come exclusively from the authored sprite/Tilemap data.
    [SerializeField, HideInInspector]
    private Color _floorColor = new Color(0.42f, 0.48f, 0.55f, 1f);

    [SerializeField, HideInInspector]
    private Color _wallColor = new Color(0.12f, 0.15f, 0.19f, 1f);

    [SerializeField, HideInInspector]
    private Color _obstacleColor = new Color(0.55f, 0.35f, 0.17f, 1f);

    private float _uiUnitsPerWorldUnit = 1f;
    private bool _configured;
    private readonly List<RaidMinimapBatchGraphic> _batches = new List<RaidMinimapBatchGraphic>();

    public override Texture mainTexture => _layout != null && _layout.ArtBatchCount > 0
        ? _layout.GetArtBatch(0)?.Texture : base.mainTexture;

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
        _configured = false;
        foreach (RaidMinimapBatchGraphic batch in _batches)
            if (batch != null) batch.gameObject.SetActive(false);
        canvasRenderer.Clear();
        SetAllDirty();
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
        uiSize = _layout.RenderSize * _uiUnitsPerWorldUnit;
        if (!IsPositiveFinite(uiSize.x) || !IsPositiveFinite(uiSize.y))
        {
            error = "RaidMinimapGraphic produced an invalid UI size.";
            return false;
        }

        _configured = true;
        RefreshBatches();
        SetAllDirty();
        error = null;
        return true;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        RaidMinimapBatchGraphic.Populate(vertexHelper,
            _configured && _layout != null && _layout.ArtBatchCount > 0 ? _layout.GetArtBatch(0) : null,
            _uiUnitsPerWorldUnit, color);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (_configured) RefreshBatches();
    }

    protected override void OnDisable()
    {
        foreach (RaidMinimapBatchGraphic batch in _batches)
            if (batch != null) batch.gameObject.SetActive(false);
        base.OnDisable();
    }

    private void RefreshBatches()
    {
        int required = _layout.ArtBatchCount - 1;
        for (int i = 0; i < required; i++)
        {
            if (i == _batches.Count)
            {
                var child = new GameObject("TileArtBatch", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(RaidMinimapBatchGraphic));
                child.hideFlags = HideFlags.DontSave | HideFlags.HideInHierarchy;
                child.layer = gameObject.layer;
                child.transform.SetParent(transform, false);
                var rect = (RectTransform)child.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                _batches.Add(child.GetComponent<RaidMinimapBatchGraphic>());
            }

            RaidMinimapBatchGraphic graphic = _batches[i];
            graphic.Configure(_layout.GetArtBatch(i + 1), _uiUnitsPerWorldUnit, color);
            graphic.gameObject.SetActive(isActiveAndEnabled);
        }

        for (int i = required; i < _batches.Count; i++) _batches[i].gameObject.SetActive(false);
    }
    private static bool IsPositiveFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
    }
}
