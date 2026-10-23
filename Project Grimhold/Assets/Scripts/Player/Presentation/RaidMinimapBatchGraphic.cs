using UnityEngine;
using UnityEngine.UI;

/// <summary>One ordered texture/vertex-limit continuation of the minimap, not one object per tile.</summary>
[DisallowMultipleComponent]
public sealed class RaidMinimapBatchGraphic : MaskableGraphic
{
    private MinimapMeshBatch _batch;
    private float _scale;

    public override Texture mainTexture => _batch?.Texture != null ? _batch.Texture : base.mainTexture;

    public void Configure(MinimapMeshBatch batch, float scale, Color tint)
    {
        _batch = batch;
        _scale = scale;
        color = tint;
        raycastTarget = false;
        SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        Populate(vertices, _batch, _scale, color);
    }

    internal static void Populate(VertexHelper vertices, MinimapMeshBatch batch, float scale, Color tint)
    {
        vertices.Clear();
        if (batch == null) return;
        for (int i = 0; i < batch.VertexCount; i++)
            vertices.AddVert(batch.GetPosition(i) * scale, (Color)batch.GetColor(i) * tint, batch.GetUv(i));
        for (int i = 0; i < batch.IndexCount; i += 3)
            vertices.AddTriangle(batch.GetIndex(i), batch.GetIndex(i + 1), batch.GetIndex(i + 2));
    }
}
