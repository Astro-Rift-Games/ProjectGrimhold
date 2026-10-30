using System.Collections.Generic;
using UnityEditor.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>Resolves only the permitted Tilemaps into ordered, bounded uGUI mesh buffers.</summary>
internal static class MinimapTileArtBuilder
{
    public static bool TryBuild(Tilemap[] maps, Vector2 pivot, Vector2 worldSize,
        out MinimapMeshBatch[] batches, out Sprite[] sprites, out Vector2 renderSize,
        out string error)
    {
        batches = null;
        sprites = null;
        renderSize = worldSize;
        error = null;
        var orderedMaps = new List<Tilemap>(maps);
        // List.Sort is not stable: the authored whitelist order breaks equal renderer keys.
        orderedMaps.Sort((left, right) => CompareMaps(left, right, maps));
        var result = new List<MinimapMeshBatch>();
        var sourceSprites = new List<Sprite>();
        var seenSprites = new HashSet<Sprite>();
        var positions = new List<Vector2>();
        var uvs = new List<Vector2>();
        var colors = new List<Color32>();
        var indices = new List<int>();
        Texture2D texture = null;
        Vector2 halfSize = worldSize * 0.5f;

        foreach (Tilemap map in orderedMaps)
        {
            TilemapRenderer renderer = map.GetComponent<TilemapRenderer>();
            if (renderer == null || !renderer.enabled || !map.gameObject.activeInHierarchy)
            {
                error = $"Minimap source {map.name} requires an active TilemapRenderer.";
                return false;
            }

            var cells = new List<Vector3Int>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) cells.Add(cell);

            // Unity's SortOrder starts at the named corner (lowest XY for BottomLeft).
            bool right = renderer.sortOrder == TilemapRenderer.SortOrder.BottomRight ||
                renderer.sortOrder == TilemapRenderer.SortOrder.TopRight;
            bool top = renderer.sortOrder == TilemapRenderer.SortOrder.TopLeft ||
                renderer.sortOrder == TilemapRenderer.SortOrder.TopRight;
            cells.Sort((a, b) =>
            {
                int y = a.y.CompareTo(b.y) * (top ? -1 : 1);
                if (y != 0) return y;
                int x = a.x.CompareTo(b.x) * (right ? -1 : 1);
                return x != 0 ? x : a.z.CompareTo(b.z);
            });

            foreach (Vector3Int cell in cells)
            {
                Sprite sprite = map.GetSprite(cell);
                Texture2D sourceTexture = sprite != null ? SpriteUtility.GetSpriteTexture(sprite, false) : null;
                if (sprite == null || sourceTexture == null)
                {
                    error = $"Minimap source {map.name} at {cell} has no tile sprite/texture.";
                    return false;
                }

                // Never freeze the Editor's packed preview atlas/UVs: build packing can change it.
                // Keep source textures here; the generator later rebinds batches to the averaged atlas.
                Vector2[] vertices = SpriteUtility.GetSpriteMesh(sprite, false);
                Vector2[] spriteUvs = SpriteUtility.GetSpriteUVs(sprite, false);
                ushort[] triangles = SpriteUtility.GetSpriteIndices(sprite, false);
                if (vertices.Length > MinimapMeshBatch.MaxVertexCount)
                {
                    error = $"Minimap sprite {sprite.name} exceeds the uGUI vertex limit.";
                    return false;
                }

                if (texture != sourceTexture ||
                    positions.Count + vertices.Length > MinimapMeshBatch.MaxVertexCount)
                {
                    Flush(result, texture, positions, uvs, colors, indices);
                    texture = sourceTexture;
                }

                if (seenSprites.Add(sprite)) sourceSprites.Add(sprite);
                Vector3 anchor = map.CellToLocalInterpolated((Vector3)cell + map.tileAnchor);
                Matrix4x4 matrix = map.transform.localToWorldMatrix *
                    Matrix4x4.Translate(anchor) * map.orientationMatrix * map.GetTransformMatrix(cell);
                Color32 tint = map.color * map.GetColor(cell);
                int start = positions.Count;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector2 position = (Vector2)matrix.MultiplyPoint3x4(vertices[i]) - pivot;
                    positions.Add(position);
                    uvs.Add(spriteUvs[i]);
                    colors.Add(tint);
                    halfSize = Vector2.Max(halfSize, new Vector2(Mathf.Abs(position.x), Mathf.Abs(position.y)));
                }

                for (int i = 0; i < triangles.Length; i++) indices.Add(start + triangles[i]);
            }
        }

        Flush(result, texture, positions, uvs, colors, indices);
        batches = result.ToArray();
        sprites = sourceSprites.ToArray();
        renderSize = halfSize * 2f;
        if (batches.Length == 0)
        {
            error = "Dungeon minimap has no visible tile art.";
            return false;
        }

        foreach (MinimapMeshBatch batch in batches)
            if (!batch.TryValidate(out error)) return false;
        return true;
    }

    private static int CompareMaps(Tilemap a, Tilemap b, Tilemap[] whitelist)
    {
        var left = a.GetComponent<TilemapRenderer>();
        var right = b.GetComponent<TilemapRenderer>();
        if (left == null || right == null) return System.Array.IndexOf(whitelist, a)
            .CompareTo(System.Array.IndexOf(whitelist, b));
        int layer = SortingLayer.GetLayerValueFromID(left.sortingLayerID)
            .CompareTo(SortingLayer.GetLayerValueFromID(right.sortingLayerID));
        if (layer != 0) return layer;
        int order = left.sortingOrder.CompareTo(right.sortingOrder);
        return order != 0 ? order : System.Array.IndexOf(whitelist, a)
            .CompareTo(System.Array.IndexOf(whitelist, b));
    }

    private static void Flush(List<MinimapMeshBatch> batches, Texture2D texture,
        List<Vector2> positions, List<Vector2> uvs, List<Color32> colors, List<int> indices)
    {
        if (positions.Count == 0) return;
        batches.Add(new MinimapMeshBatch(texture, positions.ToArray(), uvs.ToArray(),
            colors.ToArray(), indices.ToArray()));
        positions.Clear();
        uvs.Clear();
        colors.Clear();
        indices.Clear();
    }
}
