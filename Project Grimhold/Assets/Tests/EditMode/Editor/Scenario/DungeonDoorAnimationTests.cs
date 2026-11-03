using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
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
    public void Controller_HasExactlyFourExplicitStates_WithoutGameplayTransitions()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimationFolder + "DungeonDoor.controller");
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.layers, Has.Length.EqualTo(1));
        Assert.That(controller.parameters, Is.Empty);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        Assert.That(machine.states.Select(s => s.state.name), Is.EquivalentTo(new[]
        {
            "Door_Closed", "Door_Open_North", "Door_Opening_North", "Door_Closing_North"
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

    [TestCase("Door_Closed", false, 3)]
    [TestCase("Door_Open_North", false, 0)]
    [TestCase("Door_Opening_North", true, 3)]
    [TestCase("Door_Closing_North", true, 0)]
    public void Clip_ContainsOnlyOrderedSpriteFrames_WithExactDurationAndLoopPolicy(
        string name, bool transition, int firstFrame)
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
        const string sheet = "Door-Open-Sheet";
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(ArtFolder + sheet + ".png").OfType<Sprite>().ToArray();
        for (int i = 0; i < keys.Length; i++)
        {
            int frame = transition ? (firstFrame == 3 ? 3 - Mathf.Min(i, 3) : Mathf.Min(i, 3)) : firstFrame;
            Assert.That(keys[i].time, Is.EqualTo(transition ? i * 0.1f : i * 0.3f).Within(0.00001f));
            Sprite expected = sprites.Single(s => s.name == sheet + "_" + frame);
            Assert.That(expected, Is.Not.Null);
            Assert.That(keys[i].value, Is.SameAs(expected));
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

    [TestCase(DungeonDoorState.Closed, "Door_Closed")]
    [TestCase(DungeonDoorState.Opening, "Door_Opening_North")]
    [TestCase(DungeonDoorState.Open, "Door_Open_North")]
    [TestCase(DungeonDoorState.Closing, "Door_Closing_North")]
    public void Presenter_MapsLifecycleToNorthAnimation(DungeonDoorState state, string expectedState)
    {
        MethodInfo resolve = typeof(DungeonDoorPresenter).GetMethod("ResolveAnimationState",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.That(resolve, Is.Not.Null);
        Assert.That(resolve.Invoke(null, new object[] { state }),
            Is.EqualTo(Animator.StringToHash("Base Layer." + expectedState)));
    }

    [Test]
    public void DiscardedDoorVariants_AndMeshGenerator_AreRemoved()
    {
        string[] discarded =
        {
            ArtFolder + "Door-Open-North-Anchored.asset",
            ArtFolder + "Door-Open-South-Anchored.asset",
            "Assets/Editor/DungeonDoorSpriteMeshBuilder.cs",
            AnimationFolder + "Door_Open_South.anim",
            AnimationFolder + "Door_Opening_South.anim",
            AnimationFolder + "Door_Closing_South.anim"
        };
        foreach (string path in discarded)
        {
            Assert.That(System.IO.File.Exists(path), Is.False, path);
            Assert.That(System.IO.File.Exists(path + ".meta"), Is.False, path + ".meta");
        }
    }

    [Test]
    public void NorthArtwork_MatchesApprovedV2_WithoutImportChanges()
    {
        string path = ArtFolder + "Door-Open-Sheet.png";
        using (SHA256 hash = SHA256.Create())
        {
            string actual = System.BitConverter.ToString(hash.ComputeHash(System.IO.File.ReadAllBytes(path)))
                .Replace("-", string.Empty);
            Assert.That(actual, Is.EqualTo("AAC39ABF2B80157B86691BFE96808B54E14127C88610D303AC5627D03F5E8C36"),
                "Keep the exact approved v2 artwork, without the rejected fixed-strip fold or mesh deformation.");
        }

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo("d3dfd902744cafb41b2514ed1620e588"));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        Assert.That(sprites, Has.Length.EqualTo(4));
        foreach (Sprite sprite in sprites)
        {
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(64, 47)));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(128, 128)));
        }
    }
}
