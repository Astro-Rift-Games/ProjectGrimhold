using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TorchFlameAnimationTests
{
    private const string ArtFolder = "Assets/Art/Props/";
    private const string TorchLitPath = ArtFolder + "Torch-Lit.png";
    private const string TorchUnlitPath = ArtFolder + "Torch-Unlit.png";
    private const string TorchLitBasePath = ArtFolder + "Torch-Lit-Base.png";
    private const string TorchFlameSheetPath = ArtFolder + "Torch-Flame-Sheet.png";

    private const string TorchLitGuid = "c43a0c5368c4f2e428d1d2510ba209a7";
    private const string TorchUnlitGuid = "4567c6a5cd0530d4884ef69ab072f2a0";
    private const string TorchLitBaseGuid = "805d7d084208f9549a83d5778d83930b";
    private const string TorchFlameSheetGuid = "86b2a720157a62f4a99268defffb2623";

    private const string AnimationFolder = "Assets/Animations/Props/";
    private const string ClipPath = AnimationFolder + "Torch_Flame_Flicker.anim";
    private const string ControllerPath = AnimationFolder + "TorchFlame.controller";

    private const string PrefabFolder = "Assets/Prefabs/Lighting Props/";
    private const string TorchPrefabPath = PrefabFolder + "Torch.prefab";
    private const string BaseLightPropPrefabPath = PrefabFolder + "DungeonLightProp.prefab";
    private const string DefaultTorchConfigPath = "Assets/Scriptable Objects/DefaultTorchLightPropConfig.asset";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

    private const float ExpectedSampleRate = 7f;
    private const int FlameFrameCount = 10;
    private static readonly Vector2 ExpectedImageOrigin = new Vector2(64f, 64.5f);

    [Test]
    public void OriginalTorchArt_PreservesGuidsAndSubSprites()
    {
        Assert.That(AssetDatabase.AssetPathToGUID(TorchLitPath), Is.EqualTo(TorchLitGuid));
        Assert.That(AssetDatabase.AssetPathToGUID(TorchUnlitPath), Is.EqualTo(TorchUnlitGuid));

        Sprite lit = LoadSingleSprite(TorchLitPath, "Torch-Lit_0");
        Sprite unlit = LoadSingleSprite(TorchUnlitPath, "Torch-Unlit_0");
        Assert.That(lit.rect, Is.EqualTo(new Rect(58f, 51f, 12f, 27f)));
        Assert.That(lit.rect.position + lit.pivot, Is.EqualTo(ExpectedImageOrigin));
        Assert.That(unlit.rect, Is.EqualTo(new Rect(58f, 51f, 12f, 15f)));
    }

    [Test]
    public void TorchLitBase_UsesProjectPixelArtImportSettings_AndAlignedPivot()
    {
        Assert.That(AssetDatabase.AssetPathToGUID(TorchLitBasePath), Is.EqualTo(TorchLitBaseGuid));

        var importer = AssetImporter.GetAtPath(TorchLitBasePath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        AssertPixelArtImportSettings(importer);

        Sprite baseSprite = LoadSingleSprite(TorchLitBasePath, "Torch-Lit-Base_0");
        Assert.That(baseSprite.rect, Is.EqualTo(new Rect(58f, 51f, 12f, 15f)));
        Assert.That((baseSprite.rect.position + baseSprite.pivot - ExpectedImageOrigin).magnitude,
            Is.LessThan(0.001f),
            "Torch-Lit-Base_0 pivot must keep the sconce at the exact same image origin as Torch-Lit_0.");
    }

    [Test]
    public void TorchFlameSheet_UsesProjectPixelArtImportSettings_AndUniformAlignedFrames()
    {
        Assert.That(AssetDatabase.AssetPathToGUID(TorchFlameSheetPath), Is.EqualTo(TorchFlameSheetGuid));

        var importer = AssetImporter.GetAtPath(TorchFlameSheetPath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        AssertPixelArtImportSettings(importer);

        Sprite[] sprites = LoadSheetSprites(TorchFlameSheetPath, "Torch-Flame-Sheet", FlameFrameCount);
        for (int i = 0; i < FlameFrameCount; i++)
        {
            Assert.That(sprites[i].rect, Is.EqualTo(new Rect(i * 128f, 0f, 128f, 128f)), sprites[i].name);
            Assert.That((sprites[i].pivot - ExpectedImageOrigin).magnitude, Is.LessThan(0.001f), sprites[i].name);
        }
    }

    [Test]
    public void Clip_ContainsTenLoopingFlameFrames_WithoutTransformsOrEvents()
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
        Assert.That(clip, Is.Not.Null, ClipPath);
        Assert.That(clip.legacy, Is.False);
        Assert.That(clip.frameRate, Is.EqualTo(ExpectedSampleRate));
        Assert.That(clip.length, Is.EqualTo(FlameFrameCount / ExpectedSampleRate).Within(0.0001f));
        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);
        Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty);
        Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty);

        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        Assert.That(bindings, Has.Length.EqualTo(1));
        Assert.That(bindings[0].type, Is.EqualTo(typeof(SpriteRenderer)));
        Assert.That(bindings[0].propertyName, Is.EqualTo("m_Sprite"));
        Assert.That(bindings[0].path, Is.Empty, "Animator lives on FlameVisual itself.");

        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        Assert.That(keys, Has.Length.EqualTo(FlameFrameCount));
        Sprite[] sprites = LoadSheetSprites(TorchFlameSheetPath, "Torch-Flame-Sheet", FlameFrameCount);
        for (int i = 0; i < FlameFrameCount; i++)
        {
            Assert.That(keys[i].time, Is.EqualTo(i / ExpectedSampleRate).Within(0.0001f));
            Assert.That(keys[i].value, Is.SameAs(sprites[i]));
        }
    }

    [Test]
    public void Controller_HasSingleDefaultLoopingFlickerState()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.That(controller, Is.Not.Null, ControllerPath);
        Assert.That(controller.layers, Has.Length.EqualTo(1));
        Assert.That(controller.parameters, Is.Empty);

        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        Assert.That(machine.states, Has.Length.EqualTo(1));
        Assert.That(machine.anyStateTransitions, Is.Empty);
        Assert.That(machine.defaultState, Is.Not.Null);
        Assert.That(machine.defaultState.name, Is.EqualTo("Torch_Flame_Flicker"));
        Assert.That(machine.defaultState.speed, Is.EqualTo(1f));
        Assert.That(machine.defaultState.transitions, Is.Empty);
        Assert.That(machine.defaultState.behaviours, Is.Empty);
        Assert.That(machine.defaultState.motion, Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath)));
    }

    [Test]
    public void TorchPrefab_IsConfiguredWithBaseSpriteAndAnimatedFlameVisual_WithoutAffectingSiblings()
    {
        GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefabPath);
        Assert.That(torchPrefab, Is.Not.Null);

        Sprite litBase = LoadSingleSprite(TorchLitBasePath, "Torch-Lit-Base_0");
        Sprite unlit = LoadSingleSprite(TorchUnlitPath, "Torch-Unlit_0");
        Sprite firstFlame = LoadSheetSprites(TorchFlameSheetPath, "Torch-Flame-Sheet", FlameFrameCount)[0];
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        DungeonLightPropConfig config = AssetDatabase.LoadAssetAtPath<DungeonLightPropConfig>(DefaultTorchConfigPath);

        DungeonLightProp lightProp = torchPrefab.GetComponent<DungeonLightProp>();
        Assert.That(lightProp, Is.Not.Null);
        Assert.That(lightProp.Config, Is.SameAs(config));
        Assert.That(lightProp.LitSprite, Is.SameAs(litBase));
        Assert.That(lightProp.ExtinguishedSprite, Is.SameAs(unlit));

        SpriteRenderer rootRenderer = torchPrefab.GetComponent<SpriteRenderer>();
        Assert.That(rootRenderer, Is.Not.Null);
        Assert.That(rootRenderer.sprite, Is.SameAs(litBase));
        Assert.That(rootRenderer.sortingLayerName, Is.EqualTo("Traps"));
        Assert.That(rootRenderer.sortingOrder, Is.EqualTo(1));

        Transform flameTransform = torchPrefab.transform.Find("FlameVisual");
        Assert.That(flameTransform, Is.Not.Null);
        Assert.That(lightProp.FlameObject, Is.SameAs(flameTransform.gameObject));
        Assert.That(flameTransform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(flameTransform.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(flameTransform.localScale, Is.EqualTo(Vector3.one));

        SpriteRenderer flameRenderer = flameTransform.GetComponent<SpriteRenderer>();
        Assert.That(flameRenderer, Is.Not.Null);
        Assert.That(flameRenderer.sprite, Is.SameAs(firstFlame));
        Assert.That(flameRenderer.sortingLayerName, Is.EqualTo("Traps"),
            "FlameVisual must render above Default dungeon tiles and above the torch sconce (Order 1).");
        Assert.That(flameRenderer.sortingOrder, Is.EqualTo(2));

        Animator animator = flameTransform.GetComponent<Animator>();
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller));
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
        Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));

        string[] siblingPaths =
        {
            BaseLightPropPrefabPath,
            PrefabFolder + "Brazier.prefab",
            PrefabFolder + "Candle.prefab",
            PrefabFolder + "Multiple Candles.prefab"
        };
        foreach (string siblingPath in siblingPaths)
        {
            GameObject sibling = AssetDatabase.LoadAssetAtPath<GameObject>(siblingPath);
            Assert.That(sibling, Is.Not.Null, siblingPath);
            Assert.That(sibling.GetComponentsInChildren<Animator>(true), Is.Empty, siblingPath);
            Transform siblingFlame = sibling.transform.Find("FlameVisual");
            Assert.That(siblingFlame, Is.Not.Null, siblingPath);
            Assert.That(siblingFlame.GetComponent<SpriteRenderer>().sprite, Is.Null, siblingPath);
        }
    }

    [Test]
    public void TorchPrefab_ExtinguishesAndRelightsCleanly_WhileAdvancingLoopFrames()
    {
        GameObject torchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TorchPrefabPath);
        Sprite litBase = LoadSingleSprite(TorchLitBasePath, "Torch-Lit-Base_0");
        Sprite unlit = LoadSingleSprite(TorchUnlitPath, "Torch-Unlit_0");
        Sprite[] flameSprites = LoadSheetSprites(TorchFlameSheetPath, "Torch-Flame-Sheet", FlameFrameCount);

        GameObject instance = Object.Instantiate(torchPrefab);
        try
        {
            DungeonLightProp prop = instance.GetComponent<DungeonLightProp>();
            SpriteRenderer rootRenderer = instance.GetComponent<SpriteRenderer>();
            Transform flame = instance.transform.Find("FlameVisual");
            SpriteRenderer flameRenderer = flame.GetComponent<SpriteRenderer>();
            Animator animator = flame.GetComponent<Animator>();

            prop.ApplyPhase(DungeonPressurePhase.Normal, immediate: true);
            Assert.That(rootRenderer.sprite, Is.SameAs(litBase));
            Assert.That(flame.gameObject.activeSelf, Is.True);

            animator.Update(0f);
            Assert.That(flameRenderer.sprite, Is.SameAs(flameSprites[0]));

            animator.Update(2.5f / ExpectedSampleRate);
            Assert.That(flameRenderer.sprite, Is.SameAs(flameSprites[2]));

            animator.Update(5f / ExpectedSampleRate);
            Assert.That(flameRenderer.sprite, Is.SameAs(flameSprites[7]));

            animator.Update(3f / ExpectedSampleRate);
            Assert.That(flameRenderer.sprite, Is.SameAs(flameSprites[0]));

            prop.ApplyPhase(DungeonPressurePhase.Collapse, immediate: true);
            Assert.That(rootRenderer.sprite, Is.SameAs(unlit));
            Assert.That(flame.gameObject.activeSelf, Is.False);

            prop.ApplyPhase(DungeonPressurePhase.Normal, immediate: true);
            Assert.That(rootRenderer.sprite, Is.SameAs(litBase));
            Assert.That(flame.gameObject.activeSelf, Is.True);
            animator.Update(0.05f);
            Assert.That(flameSprites, Does.Contain(flameRenderer.sprite));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void GameplayScene_AllTorchesInheritLitBaseAndAnimatedFlame_WithoutStaleStaticOverrides()
    {
        Sprite litBase = LoadSingleSprite(TorchLitBasePath, "Torch-Lit-Base_0");
        Sprite unlit = LoadSingleSprite(TorchUnlitPath, "Torch-Unlit_0");
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

        Scene previousActive = SceneManager.GetActiveScene();
        Scene preview = EditorSceneManager.OpenPreviewScene(GameplayScenePath);
        try
        {
            List<DungeonLightProp> torches = preview.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<DungeonLightProp>(true))
                .Where(prop => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prop.gameObject) == TorchPrefabPath)
                .ToList();

            Assert.That(torches, Has.Count.EqualTo(17));
            foreach (DungeonLightProp torch in torches)
            {
                SpriteRenderer rootRenderer = torch.GetComponent<SpriteRenderer>();
                Transform flame = torch.transform.Find("FlameVisual");
                Assert.That(rootRenderer.sprite, Is.SameAs(litBase), torch.name);
                Assert.That(torch.LitSprite, Is.SameAs(litBase), torch.name);
                Assert.That(torch.ExtinguishedSprite, Is.SameAs(unlit), torch.name);
                Assert.That(flame, Is.Not.Null, torch.name);
                Assert.That(flame.localPosition, Is.EqualTo(Vector3.zero), torch.name);

                SpriteRenderer flameRenderer = flame.GetComponent<SpriteRenderer>();
                Assert.That(flameRenderer.sortingLayerName, Is.EqualTo("Traps"), torch.name);
                Assert.That(flameRenderer.sortingOrder, Is.EqualTo(2), torch.name);

                Animator animator = flame.GetComponent<Animator>();
                Assert.That(animator, Is.Not.Null, torch.name);
                Assert.That(animator.runtimeAnimatorController, Is.SameAs(controller), torch.name);
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (previousActive.IsValid() && previousActive.isLoaded)
            {
                SceneManager.SetActiveScene(previousActive);
            }
        }
    }

    private static void AssertPixelArtImportSettings(TextureImporter importer)
    {
        Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16f));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.mipmapEnabled, Is.False);
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.Tight));
    }

    private static Sprite LoadSingleSprite(string path, string expectedName)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        Assert.That(sprites, Has.Length.EqualTo(1), path);
        Assert.That(sprites[0].name, Is.EqualTo(expectedName), path);
        return sprites[0];
    }

    private static Sprite[] LoadSheetSprites(string path, string sheetName, int count)
    {
        Sprite[] all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        Assert.That(all, Has.Length.EqualTo(count), path);
        var ordered = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            string expectedName = $"{sheetName}_{i}";
            ordered[i] = all.Single(s => s.name == expectedName);
        }

        return ordered;
    }
}
