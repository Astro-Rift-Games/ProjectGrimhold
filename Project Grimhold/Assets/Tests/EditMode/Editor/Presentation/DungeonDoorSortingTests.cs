using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DungeonDoorSortingTests
{
    private const string PlayerPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string EnemyPath = "Assets/Prefabs/Enemies/NetworkEnemy.prefab";
    private const string RendererPath = "Assets/Settings/Renderer2D.asset";

    private static readonly string[] CharacterPrefabs =
    {
        PlayerPath,
        EnemyPath,
        "Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab",
        "Assets/Prefabs/Enemies/Slimes/BlueSlime.prefab",
        "Assets/Prefabs/Enemies/Slimes/GreenSlime.prefab",
        "Assets/Prefabs/Enemies/Slimes/RedSlime.prefab"
    };

    [Test]
    public void DoorView_SortsWithCharactersAtItsBasePivot()
    {
        GameObject door = LoadPrefab("Assets/Prefabs/Doors/DungeonDoor.prefab");
        SpriteRenderer view = door.GetComponentInChildren<SpriteRenderer>();
        Assert.That(view.sortingLayerName, Is.EqualTo("Characters"));
        Assert.That(view.sortingOrder, Is.Zero);
        Assert.That(view.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
        Assert.That(door.transform.InverseTransformPoint(view.transform.position), Is.EqualTo(Vector3.zero));
        Assert.That(view.transform.rotation, Is.EqualTo(door.transform.rotation));
        Assert.That(view.transform.lossyScale, Is.EqualTo(door.transform.lossyScale));
        Assert.That(view.sprite.pivot, Is.EqualTo(new Vector2(64f, 47f)));
        Animator animator = view.GetComponent<Animator>();
        Assert.That(animator, Is.Not.Null);
        Assert.That(new SerializedObject(door.GetComponent<DungeonDoorPresenter>())
            .FindProperty("_animator").objectReferenceValue, Is.SameAs(animator));
    }

    [Test]
    public void PlayerRootToDrawnFeetOffset_MatchesDoorCalibration()
    {
        Assert.That(MeasurePlayerRootToFeetOffset(), Is.EqualTo(0.6875f).Within(0.00001f));
    }

    [Test]
    public void DoorSortAnchor_MatchesPlayerRootToDrawnFeetOffset()
    {
        GameObject door = LoadPrefab("Assets/Prefabs/Doors/DungeonDoor.prefab");
        Transform anchor = door.transform.Find("SortAnchor");
        Assert.That(anchor, Is.Not.Null);
        SortingGroup group = anchor.GetComponent<SortingGroup>();
        Assert.That(group, Is.Not.Null);
        Assert.That(group.enabled, Is.True);
        Assert.That(group.sortingLayerName, Is.EqualTo("Characters"));
        Assert.That(group.sortingOrder, Is.Zero);
        Assert.That(group.sortAtRoot, Is.False);
        Assert.That(anchor.position.y - door.transform.position.y,
            Is.EqualTo(MeasurePlayerRootToFeetOffset()).Within(0.00001f));
        Assert.That(anchor.localPosition, Is.EqualTo(new Vector3(0f, 0.6875f, 0f)));
        Assert.That(anchor.Find("View").localPosition, Is.EqualTo(new Vector3(0f, -0.6875f, 0f)));
    }

    [TestCase(-0.1f, true)]
    [TestCase(0.1f, false)]
    public void PlayerFeetAcrossDoorBase_SortOnTheCorrectSide(float feetFromBase, bool inFront)
    {
        GameObject door = LoadPrefab("Assets/Prefabs/Doors/DungeonDoor.prefab");
        SpriteRenderer view = door.GetComponentInChildren<SpriteRenderer>();
        SortingGroup group = view.GetComponentInParent<SortingGroup>(true);
        float doorSortY = group != null ? group.transform.position.y : view.transform.position.y;
        float playerRootY = door.transform.position.y + feetFromBase + MeasurePlayerRootToFeetOffset();
        // With Custom Axis (0,1,0), the lower Y sorts in front at equal layer/order.
        Assert.That(playerRootY < doorSortY, Is.EqualTo(inFront));
    }

    [Test]
    public void DoorBlockerAndTrigger_MatchWallDepthAndPreserveRootRelativeTransforms()
    {
        GameObject door = LoadPrefab("Assets/Prefabs/Doors/DungeonDoor.prefab");
        foreach (string name in new[] { "Blocker", "InteractionTrigger" })
        {
            Transform child = door.transform.Find(name);
            Assert.That(child.parent, Is.SameAs(door.transform));
            Assert.That(child.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(child.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(child.localScale, Is.EqualTo(Vector3.one));
            BoxCollider2D collider = child.GetComponent<BoxCollider2D>();
            Assert.That(collider.offset, Is.EqualTo(new Vector2(0f, 0.5f)));
            Assert.That(collider.size, Is.EqualTo(name == "Blocker"
                ? new Vector2(2.125f, 1f) : new Vector2(2.5f, 2.5f)));
            Assert.That(collider.isTrigger, Is.EqualTo(name == "InteractionTrigger"));
            Assert.That(child.gameObject.layer, Is.EqualTo(name == "Blocker" ? 6 : 8));
        }
    }

    [Test]
    public void InteractionTrigger_IsReachableFromPlayerRootBlockedOnEitherSide()
    {
        GameObject door = LoadPrefab("Assets/Prefabs/Doors/DungeonDoor.prefab");
        BoxCollider2D blocker = door.transform.Find("Blocker").GetComponent<BoxCollider2D>();
        BoxCollider2D trigger = door.transform.Find("InteractionTrigger").GetComponent<BoxCollider2D>();
        PlayerInteractionConfig config = AssetDatabase.LoadAssetAtPath<PlayerInteractionConfig>(
            "Assets/Scriptable Objects/PlayerInteractionConfig.asset");
        // Actual player body spans root..root+0.5; allow movement skin outside either face.
        float southRoot = blocker.offset.y - blocker.size.y * 0.5f - 0.5f - 0.02f;
        float northRoot = blocker.offset.y + blocker.size.y * 0.5f + 0.02f;
        float min = trigger.offset.y - trigger.size.y * 0.5f;
        float max = trigger.offset.y + trigger.size.y * 0.5f;
        Assert.That(min, Is.EqualTo(-0.75f));
        Assert.That(max, Is.EqualTo(1.75f));
        foreach (float root in new[] { southRoot, northRoot })
            Assert.That(Mathf.Abs(root - Mathf.Clamp(root, min, max)), Is.LessThan(config.MaximumDistance));
    }

    [TestCaseSource(nameof(CharacterPrefabs))]
    public void RaidCharacterRoot_SortsAsOneUnitAtItsRoot(string path)
    {
        SortingGroup group = LoadPrefab(path).GetComponent<SortingGroup>();
        Assert.That(group, Is.Not.Null, path);
        Assert.That(group.enabled, Is.True, path);
        Assert.That(group.sortingLayerName, Is.EqualTo("Characters"), path);
        Assert.That(group.sortingOrder, Is.Zero, path);
        Assert.That(group.sortAtRoot, Is.False, path);
    }

    [TestCaseSource(nameof(CharacterPrefabs))]
    public void DamageParticles_KeepTheirIndependentWorldLayer(string path)
    {
        GameObject character = LoadPrefab(path);
        ParticleSystemRenderer particles = character.transform.Find("DamageParticles")
            .GetComponent<ParticleSystemRenderer>();
        string layer = path == PlayerPath ? "VFX" : "Default";
        Assert.That(particles.sortingLayerName, Is.EqualTo(layer), path);
        Assert.That(particles.sortingOrder, Is.Zero, path);
        SortingGroup group = particles.GetComponent<SortingGroup>();
        Assert.That(group, Is.Not.Null, path);
        Assert.That(group.sortAtRoot, Is.True, path);
        Assert.That(group.sortingLayerName, Is.EqualTo(layer), path);
        Assert.That(group.sortingOrder, Is.Zero, path);
    }

    [Test]
    public void SingleRendererDummy_SortsWithCharactersAtItsFeet()
    {
        GameObject dummy = LoadPrefab("Assets/Prefabs/Sandbox/TrainingDummy.prefab");
        Assert.That(dummy.GetComponentsInChildren<Renderer>(true), Has.Length.EqualTo(1));
        Assert.That(dummy.GetComponent<SortingGroup>(), Is.Null);
        SpriteRenderer body = dummy.transform.Find("Body").GetComponent<SpriteRenderer>();
        Assert.That(body.sortingLayerName, Is.EqualTo("Characters"));
        Assert.That(body.sortingOrder, Is.Zero);
        Assert.That(body.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot));
        Assert.That(body.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(body.sprite.pivot.y, Is.Zero);
    }

    [Test]
    public void UsedRenderer2D_SortsByCustomYAxis()
    {
        RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
        Assert.That(pipeline, Is.Not.Null);
        var pipelineSettings = new SerializedObject(pipeline);
        int index = pipelineSettings.FindProperty("m_DefaultRendererIndex").intValue;
        Object renderer = pipelineSettings.FindProperty("m_RendererDataList")
            .GetArrayElementAtIndex(index).objectReferenceValue;
        Assert.That(AssetDatabase.GetAssetPath(renderer), Is.EqualTo(RendererPath));
        var settings = new SerializedObject(renderer);
        Assert.That(settings.FindProperty("m_TransparencySortMode").intValue,
            Is.EqualTo((int)TransparencySortMode.CustomAxis));
        Assert.That(settings.FindProperty("m_TransparencySortAxis").vector3Value,
            Is.EqualTo(Vector3.up));
    }

    [Test]
    public void PlayerSprites_PreserveTheirAuthoredRelativeOrders()
    {
        GameObject player = LoadPrefab(PlayerPath);
        var paths = new[]
        {
            "Legs", "Legs/BootsVisual", "Body", "Body/ArmorVisual", "Head", "Head/HelmetVisual",
            "RightHandPivot/RightHand", "RightHandPivot/RightHand/RightGloveVisual",
            "RightHandPivot/RightHand/MainHandGrip/MainHandWeaponVisual/WeaponSprite",
            "LeftHandPivot/LeftHand", "LeftHandPivot/LeftHand/LeftGloveVisual",
            "LeftHandPivot/LeftHand/OffHandGrip/OffHandVisual", "AttackVfx"
        };
        var orders = new[] { 0, 1, 2, 3, 6, 7, 30, 31, 2, 4, 5, 2, 21 };
        for (int i = 0; i < paths.Length; i++)
        {
            SpriteRenderer renderer = player.transform.Find("VisualRoot/" + paths[i])
                .GetComponent<SpriteRenderer>();
            Assert.That(renderer.sortingLayerName, Is.EqualTo("Characters"), paths[i]);
            Assert.That(renderer.sortingOrder, Is.EqualTo(orders[i]), paths[i]);
        }
    }

    [TestCaseSource(nameof(CharacterPrefabs))]
    public void CharacterBody_PreservesItsAuthoredOrder(string path)
    {
        GameObject character = LoadPrefab(path);
        string bodyPath = path == PlayerPath ? "VisualRoot/Body" : "Body";
        SpriteRenderer body = character.transform.Find(bodyPath).GetComponent<SpriteRenderer>();
        Assert.That(body.sortingLayerName, Is.EqualTo("Characters"), path);
        Assert.That(body.sortingOrder, Is.EqualTo(path == PlayerPath ? 2 : 1), path);
        if (path != PlayerPath)
        {
            SpriteRenderer weapon = character.transform.Find("CombatVisuals/WeaponPivot/WeaponSprite")
                .GetComponent<SpriteRenderer>();
            Assert.That(weapon.sortingLayerName, Is.EqualTo("Characters"), path);
            Assert.That(weapon.sortingOrder, Is.Zero, path);
        }
    }

    [Test]
    public void PlayerCanvases_PreserveTheirIndependentUiComposition()
    {
        GameObject player = LoadPrefab(PlayerPath);
        Canvas hud = player.transform.Find("LocalGameplayHud").GetComponent<Canvas>();
        Assert.That(hud.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        Canvas settings = hud.transform.Find("RaidMenuPanel/SettingsPanel").GetComponent<Canvas>();
        Assert.That(settings.renderMode, Is.EqualTo(RenderMode.WorldSpace));
        Assert.That(settings.rootCanvas, Is.SameAs(settings));
        Assert.That(settings.sortingLayerName, Is.EqualTo("Default"));
        Assert.That(settings.sortingOrder, Is.EqualTo(100));
    }

    private static float MeasurePlayerRootToFeetOffset()
    {
        GameObject player = LoadPrefab(PlayerPath);
        SpriteRenderer legs = player.transform.Find("VisualRoot/Legs").GetComponent<SpriteRenderer>();
        Sprite sprite = legs.sprite;
        var texture = new Texture2D(2, 2);
        try
        {
            // Decode source pixels without changing the importer's read/write setting.
            Assert.That(ImageConversion.LoadImage(texture,
                System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture))), Is.True);
            Rect rect = sprite.rect;
            for (int y = 0; y < rect.height; y++)
            {
                for (int x = 0; x < rect.width; x++)
                {
                    if (texture.GetPixel((int)rect.x + x, (int)rect.y + y).a <= 0.01f) continue;
                    float spriteFootY = (y - sprite.pivot.y) / sprite.pixelsPerUnit;
                    float worldFootY = legs.transform.TransformPoint(new Vector3(0f, spriteFootY, 0f)).y;
                    return player.GetComponent<SortingGroup>().transform.position.y - worldFootY;
                }
            }
            Assert.Fail("Player legs sprite must contain drawn feet.");
            return 0f;
        }
        finally
        {
            Object.DestroyImmediate(texture);
        }
    }

    private static GameObject LoadPrefab(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Assert.That(prefab, Is.Not.Null, path);
        return prefab;
    }
}
