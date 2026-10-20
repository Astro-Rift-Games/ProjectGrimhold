using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectGrimhold.EditorTools.RuleTiles
{
    public static class RuleTileBuilderSpriteCatalog
    {
        public static Sprite[] Resolve(Object[] sources)
        {
            var sprites = new HashSet<Sprite>();
            if (sources == null) return Array.Empty<Sprite>();
            foreach (var source in sources)
            {
                if (source is Sprite sprite) sprites.Add(sprite);
                else if (source is Texture2D)
                    foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(source)))
                        if (asset is Sprite child) sprites.Add(child);
            }
            var result = new List<Sprite>(sprites);
            result.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            return result.ToArray();
        }

        public static void DrawSprite(Rect rect, Sprite sprite)
        {
            if (!sprite) return;
            var texture = AssetPreview.GetAssetPreview(sprite);
            if (texture) GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit, true);
            else
            {
                // Source sprites need not be Read/Write enabled; only their texture coordinates are used.
                var bounds = sprite.rect;
                var uv = new Rect(bounds.x / sprite.texture.width, bounds.y / sprite.texture.height,
                    bounds.width / sprite.texture.width, bounds.height / sprite.texture.height);
                var scale = Mathf.Min(rect.width / bounds.width, rect.height / bounds.height);
                var size = bounds.size * scale;
                GUI.DrawTextureWithTexCoords(new Rect(rect.center - size * 0.5f, size), sprite.texture, uv, true);
            }
        }
    }
}
