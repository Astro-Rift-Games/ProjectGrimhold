using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class DungeonDoorAnimationTests
{
    private const string AnimationFolder = "Assets/Animations/Doors/";
    private const string ArtFolder = "Assets/Art/Props/Doors/";
    private const string PrefabPath = "Assets/Prefabs/Doors/DungeonDoor.prefab";

    [Test]
    public void Controller_HasExactlySevenExplicitStates_WithoutGameplayTransitions()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimationFolder + "DungeonDoor.controller");
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.layers, Has.Length.EqualTo(1));
        Assert.That(controller.parameters, Is.Empty);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        Assert.That(machine.states.Select(s => s.state.name), Is.EquivalentTo(new[]
        {
            "Door_Closed", "Door_Open_North", "Door_Opening_North", "Door_Closing_North",
            "Door_Open_South", "Door_Opening_South", "Door_Closing_South"
        }));
        Assert.That(machine.defaultState.name, Is.EqualTo("Door_Closed"));
        Assert.That(machine.anyStateTransitions, Is.Empty);
        foreach (ChildAnimatorState state in machine.states)
        {
            Assert.That(state.state.transitions, Is.Empty);
            Assert.That(state.state.behaviours, Is.Empty);
            Assert.That(state.state.speed, Is.EqualTo(1f));
            Assert.That(AssetDatabase.GetAssetPath(state.state.motion),
                Is.EqualTo(AnimationFolder + state.state.name + ".anim"));
        }
    }

    [TestCase("Door_Closed", false, false, 3)]
    [TestCase("Door_Open_North", false, false, 0)]
    [TestCase("Door_Opening_North", false, true, 3)]
    [TestCase("Door_Closing_North", false, true, 0)]
    [TestCase("Door_Open_South", true, false, 0)]
    [TestCase("Door_Opening_South", true, true, 3)]
    [TestCase("Door_Closing_South", true, true, 0)]
    public void Clip_ContainsOnlyOrderedSpriteFrames_WithExactDurationAndLoopPolicy(
        string name, bool south, bool transition, int firstFrame)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimationFolder + name + ".anim");
        Assert.That(clip, Is.Not.Null, name);
        Assert.That(clip.legacy, Is.False);
        Assert.That(clip.frameRate, Is.EqualTo(10f));
        Assert.That(clip.length, Is.EqualTo(0.4f).Within(0.00001f));
        Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.EqualTo(!transition));
        Assert.That(AnimationUtility.GetCurveBindings(clip), Is.Empty, "No transforms or gameplay properties.");
        Assert.That(AnimationUtility.GetAnimationEvents(clip), Is.Empty, "No gameplay animation events.");
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        Assert.That(bindings, Has.Length.EqualTo(1));
        Assert.That(bindings[0].type, Is.EqualTo(typeof(SpriteRenderer)));
        Assert.That(bindings[0].propertyName, Is.EqualTo("m_Sprite"));
        Assert.That(bindings[0].path, Is.Empty, "Animator is on the View itself.");
        ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        // Unity holds the last sprite key for one sample, ending at 0.4s.
        Assert.That(keys, Has.Length.EqualTo(transition ? 4 : 2));
        string sheet = south ? "Door-Open-South-Sheet" : "Door-Open-Sheet";
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(ArtFolder + sheet + ".png").OfType<Sprite>().ToArray();
        for (int i = 0; i < keys.Length; i++)
        {
            int frame = transition ? (firstFrame == 3 ? 3 - Mathf.Min(i, 3) : Mathf.Min(i, 3)) : firstFrame;
            Assert.That(keys[i].time, Is.EqualTo(transition ? i * 0.1f : i * 0.3f).Within(0.00001f));
            Assert.That(keys[i].value, Is.SameAs(sprites.Single(s => s.name == sheet + "_" + frame)));
        }
    }

    [Test]
    public void Prefab_AnimatorIsPresentationOnly_AndAssignedToPresenter()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Animator animator = prefab.GetComponentInChildren<Animator>(true);
        Assert.That(animator, Is.Not.Null);
        Assert.That(animator.gameObject, Is.SameAs(prefab.GetComponentInChildren<SpriteRenderer>().gameObject));
        Assert.That(animator.runtimeAnimatorController, Is.SameAs(
            AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimationFolder + "DungeonDoor.controller")));
        Assert.That(animator.applyRootMotion, Is.False);
        Assert.That(animator.updateMode, Is.EqualTo(AnimatorUpdateMode.Normal));
        Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
        Assert.That(new SerializedObject(prefab.GetComponent<DungeonDoorPresenter>())
            .FindProperty("_animator").objectReferenceValue, Is.SameAs(animator));
        Assert.That(new SerializedObject(prefab.GetComponent<NetworkDoor>())
            .FindProperty("_transitionDuration").floatValue, Is.EqualTo(0.4f));
    }

    [Test]
    public void SouthOpenFrame_KeepsHingeAnchoredAtWallAndExtendsLeafSevenPixelsSouth_WithoutImportChanges()
    {
        var north = new Texture2D(2, 2);
        var south = new Texture2D(2, 2);
        try
        {
            Assert.That(ImageConversion.LoadImage(north, System.IO.File.ReadAllBytes(ArtFolder + "Door-Open-Sheet.png")), Is.True);
            Assert.That(ImageConversion.LoadImage(south, System.IO.File.ReadAllBytes(ArtFolder + "Door-Open-South-Sheet.png")), Is.True);
            Color32[] a = north.GetPixels32();
            Color32[] b = south.GetPixels32();
            RectInt closed = OpaqueBounds(a, north.width, cellX: 384);
            RectInt northOpen = OpaqueBounds(a, north.width, cellX: 0);
            RectInt southOpen = OpaqueBounds(b, south.width, cellX: 0);

            // Texture rows grow upward: yMax is the top edge, yMin the bottom edge.
            Assert.That(southOpen.xMin, Is.EqualTo(northOpen.xMin), "Hinge column must not move.");
            Assert.That(southOpen.xMax, Is.EqualTo(northOpen.xMax));
            Assert.That(southOpen.yMax, Is.EqualTo(closed.yMax), "Hinge top stays anchored at the wall.");
            Assert.That(southOpen.yMin, Is.EqualTo(closed.yMin - 7), "The leaf extends 7 px toward the camera.");
            for (int y = southOpen.yMin; y <= southOpen.yMax; y++)
            {
                Assert.That(RowExistsInNorthStrip(a, north.width, b, south.width, y, northOpen), Is.True,
                    $"South row {y} must reuse a north edge-on row (no new pixels).");
            }
            string path = ArtFolder + "Door-Open-South-Sheet.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo("5b3833bbd70339549a79829c7c08054b"));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            foreach (Sprite sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
            {
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(64, 47)));
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(128, 128)));
            }
        }
        finally
        {
            Object.DestroyImmediate(north);
            Object.DestroyImmediate(south);
        }
    }

    private static RectInt OpaqueBounds(Color32[] pixels, int width, int cellX)
    {
        int xMin = int.MaxValue, yMin = int.MaxValue, xMax = int.MinValue, yMax = int.MinValue;
        for (int y = 0; y < 128; y++)
        {
            for (int x = cellX; x < cellX + 128; x++)
            {
                if (pixels[y * width + x].a == 0)
                {
                    continue;
                }

                xMin = Mathf.Min(xMin, x - cellX);
                xMax = Mathf.Max(xMax, x - cellX);
                yMin = Mathf.Min(yMin, y);
                yMax = Mathf.Max(yMax, y);
            }
        }

        Assert.That(xMax, Is.GreaterThanOrEqualTo(xMin), $"Cell at x={cellX} is empty.");
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static bool RowExistsInNorthStrip(Color32[] north, int northWidth, Color32[] south, int southWidth, int southRow, RectInt strip)
    {
        for (int y = strip.yMin; y <= strip.yMax; y++)
        {
            bool same = true;
            for (int x = 0; x < 128 && same; x++)
            {
                same = north[y * northWidth + x].Equals(south[southRow * southWidth + x]);
            }

            if (same)
            {
                return true;
            }
        }

        return false;
    }
}
