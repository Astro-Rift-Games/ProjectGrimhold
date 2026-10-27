using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>One-off exact importer for the approved charcoal artwork; never changes its pixels.</summary>
public static class CharcoalUiAtlasImporter
{
    public const string AtlasPath = "Assets/Art/UI/Charcoal/grimhold-ui-charcoal.png";
    [Serializable] private sealed class Metadata { public Slice[] sprites; }
    [Serializable] private sealed class Slice { public string name; public Box unityRect; public Border border; }
    [Serializable] private sealed class Box { public float x, y, w, h; }
    [Serializable] private sealed class Border { public float left, right, top, bottom; }

    public static void Import()
    {
        AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 100;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = 2048;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var metadata = JsonUtility.FromJson<Metadata>(File.ReadAllText("Assets/Art/UI/Charcoal/grimhold-ui-charcoal.slices.json"));
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var existing = provider.GetSpriteRects().ToDictionary(s => s.name, s => s.spriteID);
        var rects = metadata.sprites.Select(s => new SpriteRect
        {
            name = s.name,
            spriteID = existing.TryGetValue(s.name, out var id) ? id : GUID.Generate(),
            rect = new Rect(s.unityRect.x, s.unityRect.y, s.unityRect.w, s.unityRect.h),
            border = new Vector4(s.border.left, s.border.bottom, s.border.right, s.border.top),
            alignment = SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f)
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
            rects.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();
    }
}
