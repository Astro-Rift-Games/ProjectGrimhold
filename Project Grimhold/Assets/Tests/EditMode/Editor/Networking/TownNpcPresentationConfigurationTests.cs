using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class TownNpcPresentationConfigurationTests
{
    private const string SheetPath = "Assets/Art/NPCs/NPCs-spritesheet.png";
    private const string ControllerPath = "Assets/Animations/NPCs/Shared/TownNpcIdle.controller";
    private static readonly string[] Parts = { "Legs", "Body", "Head", "LeftHand", "RightHand" };
    private static readonly string[] Suffixes = { "N", "NE", "NW", "S", "SE", "SW" };
    private static readonly int[] BaseOrders = { 0, 2, 6, 4, 30 };

    private sealed class NpcCase
    {
        public string Prefab;
        public string ClipPrefix;
        public string AssetFolder;
        public string Set;
        public string Sequence;
        public Type Interactable;
        public string[] Layers;
        public int[][] Groups;
    }

    private static readonly NpcCase[] Cases =
    {
        new NpcCase
        {
            Prefab = "TownStashNpc", ClipPrefix = "StashNpc", AssetFolder = "Stash",
            Set = "Blacksmith-Set", Sequence = "TownStashNpcDialogue",
            Interactable = typeof(TownStashNpcInteractable),
            Layers = new[] { "Body/TorsoVisual", "LeftHand/LeftGloveVisual", "RightHand/RightGloveVisual", "Legs/BootsVisual" },
            Groups = new[]
            {
                new[] { 2, 1, 3, 5, 4, 6 }, new[] { 8, 7, 9, 11, 10, 12 },
                new[] { 14, 13, 15, 17, 16, 18 }, new[] { 20, 19, 21, 23, 22, 0 }
            }
        },
        new NpcCase
        {
            Prefab = "TownRaidNpc", ClipPrefix = "RaidNpc", AssetFolder = "Raid",
            Set = "DungeonMaster-Set", Sequence = "TownRaidNpcDialogue",
            Interactable = typeof(TownRaidNpcInteractable),
            Layers = new[] { "Body/TorsoVisual", "LeftHand/LeftGloveVisual", "Head/HelmetVisual", "Legs/BootsVisual", "RightHand/RightGloveVisual" },
            Groups = new[]
            {
                new[] { 1, 0, 2, 4, 3, 5 }, new[] { 7, 6, 8, 10, 9, 11 },
                new[] { 13, 12, 14, 16, 15, 17 }, new[] { 19, 18, 20, 22, 21, 23 },
                new[] { 25, 24, 26, 28, 27, 29 }
            }
        },
        new NpcCase
        {
            Prefab = "Merchant", ClipPrefix = "MerchantNpc", AssetFolder = "Merchant",
            Set = "Merchant-Set", Sequence = "TownMerchantNpcDialogue",
            Interactable = typeof(TownMerchantNpcInteractable),
            Layers = new[] { "Head/HeadwearVisual", "Body/TorsoVisual", "Legs/BootsVisual" },
            Groups = new[]
            {
                new[] { 2, 1, 3, 5, 4, 6 }, new[] { 8, 7, 9, 11, 10, 12 },
                new[] { 14, 13, 15, 17, 16, 0 }
            }
        }
    };

    [Test]
    public void SharedController_HasOnlySixDirectionalIdleClips()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.layers, Has.Length.EqualTo(1));
        Assert.That(controller.layers[0].stateMachine.states, Has.Length.EqualTo(1));
        Assert.That(controller.layers[0].stateMachine.states[0].state.name, Is.EqualTo("Idle"));
        Assert.That(controller.parameters.Select(parameter => parameter.name),
            Is.EquivalentTo(new[] { "MoveX", "MoveY" }));
        Assert.That(controller.parameters.All(parameter => parameter.type == AnimatorControllerParameterType.Float), Is.True);
        BlendTree tree = controller.layers[0].stateMachine.states[0].state.motion as BlendTree;
        Assert.That(tree, Is.Not.Null);
        Assert.That(tree.blendType, Is.EqualTo(BlendTreeType.SimpleDirectional2D));
        Assert.That(tree.blendParameter, Is.EqualTo("MoveX"));
        Assert.That(tree.blendParameterY, Is.EqualTo("MoveY"));
        Assert.That(tree.children, Has.Length.EqualTo(6));
        Assert.That(tree.children.Select(child => child.motion.name),
            Is.EquivalentTo(Suffixes.Select(suffix => $"TownNpcIdle_{suffix}")));
    }

    [Test]
    public void Clips_LoopAndSynchronizePlayerBodyWithExclusiveNpcSet()
    {
        foreach (NpcCase npc in Cases)
        {
            AnimatorOverrideController overrides = LoadOverrides(npc);
            Assert.That(overrides.runtimeAnimatorController.name, Is.EqualTo("TownNpcIdle"));
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            overrides.GetOverrides(pairs);
            Assert.That(pairs, Has.Count.EqualTo(6), npc.Prefab);
            foreach (int directionIndex in Enumerable.Range(0, 6))
            {
                string suffix = Suffixes[directionIndex];
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    $"Assets/Animations/NPCs/{npc.AssetFolder}/Idle/{npc.ClipPrefix}_Idle_{suffix}.anim");
                AnimationClip player = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    $"Assets/Animations/Player/Idle/Idle_{suffix}.anim");
                Assert.That(clip, Is.Not.Null, npc.Prefab + "/" + suffix);
                Assert.That(player, Is.Not.Null);
                Assert.That(pairs.Any(pair => pair.Key.name == $"TownNpcIdle_{suffix}" && pair.Value == clip),
                    Is.True, npc.Prefab + "/" + suffix + " override");
                Assert.That(clip.frameRate, Is.EqualTo(8f));
                Assert.That(clip.length, Is.EqualTo(0.75f).Within(0.001f));
                Assert.That(AnimationUtility.GetAnimationClipSettings(clip).loopTime, Is.True);

                EditorCurveBinding[] spriteBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                Assert.That(spriteBindings.Select(binding => binding.path),
                    Is.EquivalentTo(Parts.Concat(npc.Layers)), npc.Prefab + "/" + suffix);
                foreach (string part in Parts)
                {
                    Sprite[] expected = AnimationUtility.GetObjectReferenceCurve(player,
                        SpriteBinding(part)).Select(frame => frame.value as Sprite).ToArray();
                    Sprite[] actual = AnimationUtility.GetObjectReferenceCurve(clip,
                        SpriteBinding(part)).Select(frame => frame.value as Sprite).ToArray();
                    Assert.That(actual, Is.EqualTo(expected), npc.Prefab + "/" + suffix + "/" + part);
                }
                for (int layerIndex = 0; layerIndex < npc.Layers.Length; layerIndex++)
                {
                    ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(clip,
                        SpriteBinding(npc.Layers[layerIndex]));
                    Assert.That(frames, Has.Length.EqualTo(6));
                    for (int frameIndex = 0; frameIndex < 6; frameIndex++)
                    {
                        Sprite sprite = frames[frameIndex].value as Sprite;
                        Assert.That(sprite, Is.Not.Null);
                        Assert.That(sprite.texture.name, Is.EqualTo(npc.Set));
                        int group = npc.Groups[layerIndex][directionIndex];
                        int x = ((group % 4) * 6 + frameIndex) * 96;
                        int y = sprite.texture.height - ((group / 4) + 1) * 96;
                        Assert.That(sprite.rect, Is.EqualTo(new Rect(x, y, 96, 96)),
                            npc.Prefab + "/" + suffix + "/" + npc.Layers[layerIndex] + "/" + frameIndex);
                        Assert.That(sprite.pivot, Is.EqualTo(new Vector2(48, 48)));
                        Assert.That(frames[frameIndex].time, Is.EqualTo(frameIndex / 8f).Within(0.0001f));
                    }
                }
                float handOrder = directionIndex == 2 ? -2f : 30f;
                AssertConstantSorting(clip, "RightHand", handOrder);
                if (npc.AssetFolder != "Merchant")
                {
                    AssertConstantSorting(clip, "RightHand/RightGloveVisual", handOrder + 1f);
                }
            }
        }
    }

    [Test]
    public void Prefabs_KeepInteractionAndUseOneAnimatorForEveryPart()
    {
        foreach (NpcCase npc in Cases)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{npc.Prefab}.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).OfType<IInteractable>().Count(), Is.EqualTo(1));
            Assert.That(prefab.GetComponent(npc.Interactable), Is.Not.Null);
            Assert.That(prefab.GetComponent<DialogueInteractable>(), Is.Null);
            TownNpcDialogueTrigger trigger = prefab.GetComponent<TownNpcDialogueTrigger>();
            Assert.That(trigger, Is.Not.Null);
            Assert.That(trigger.PrimarySequence.name, Is.EqualTo(npc.Sequence));
            Assert.That(trigger.PrimarySequence.Lines, Has.Length.GreaterThan(0));

            TownNpcDirectionalView view = prefab.GetComponent<TownNpcDirectionalView>();
            Transform root = prefab.transform.Find("VisualRoot");
            Assert.That(view, Is.Not.Null);
            Assert.That(view.InitialFacing, Is.EqualTo(CharacterVisualDirection.South));
            Assert.That(root.localPosition, Is.EqualTo(new Vector3(0f, 0.75f, 0f)));
            Assert.That(root.childCount, Is.EqualTo(5));
            Assert.That(root.Cast<Transform>().Select(child => child.name), Is.EquivalentTo(Parts));
            Animator animator = root.GetComponent<Animator>();
            Assert.That(animator, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Animator>(true), Has.Length.EqualTo(1));
            Assert.That(animator.runtimeAnimatorController, Is.SameAs(LoadOverrides(npc)));
            Assert.That(new SerializedObject(view).FindProperty("_animator").objectReferenceValue, Is.SameAs(animator));
            Assert.That(root.GetComponentsInChildren<NetworkBehaviour>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<SpriteRenderer>(true), Has.Length.EqualTo(5 + npc.Layers.Length));
            for (int partIndex = 0; partIndex < Parts.Length; partIndex++)
            {
                Transform part = root.Find(Parts[partIndex]);
                Assert.That(part, Is.Not.Null);
                SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sortingLayerName, Is.EqualTo("Characters"));
                Assert.That(renderer.sortingOrder, Is.EqualTo(BaseOrders[partIndex]));
                Assert.That(renderer.sharedMaterial.name, Is.EqualTo("Sprite-Lit-Default"));
                Assert.That(renderer.sprite, Is.Not.Null);
                AnimationClip southIdle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/Animations/Player/Idle/Idle_S.anim");
                Sprite southFrame = AnimationUtility.GetObjectReferenceCurve(southIdle,
                    SpriteBinding(Parts[partIndex]))[0].value as Sprite;
                Assert.That(renderer.sprite, Is.SameAs(southFrame));
            }
            foreach (string layer in npc.Layers)
            {
                Transform child = root.Find(layer);
                Assert.That(child, Is.Not.Null, npc.Prefab + "/" + layer);
                Assert.That(child.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(child.localScale, Is.EqualTo(Vector3.one));
                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                SpriteRenderer parent = child.parent.GetComponent<SpriteRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.sortingLayerName, Is.EqualTo("Characters"));
                Assert.That(renderer.sortingOrder, Is.EqualTo(parent.sortingOrder + 1));
                Assert.That(renderer.sharedMaterial, Is.SameAs(parent.sharedMaterial));
                Assert.That(renderer.sprite.texture.name, Is.EqualTo(npc.Set));
            }
        }
    }

    [Test]
    public void DirectionalView_UpdatesAndRestoresAnimatorParameters()
    {
        GameObject npc = new GameObject("NPC");
        npc.SetActive(false);
        try
        {
            Animator animator = new GameObject("VisualRoot").AddComponent<Animator>();
            animator.transform.SetParent(npc.transform, false);
            animator.runtimeAnimatorController = LoadOverrides(Cases[0]);
            TownNpcDirectionalView view = npc.AddComponent<TownNpcDirectionalView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.FindProperty("_animator").objectReferenceValue = animator;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            npc.SetActive(true);
            var directions = new[]
            {
                CharacterVisualDirection.North, CharacterVisualDirection.NorthEast,
                CharacterVisualDirection.NorthWest, CharacterVisualDirection.South,
                CharacterVisualDirection.SouthEast, CharacterVisualDirection.SouthWest
            };
            foreach (CharacterVisualDirection direction in directions)
            {
                Vector2 canonical = CharacterVisualDirectionResolver.GetCanonicalVector(direction);
                view.FaceTarget(npc.transform.position + (Vector3)canonical);
                Assert.That(view.CurrentFacing, Is.EqualTo(direction));
                Assert.That(animator.GetFloat("MoveX"), Is.EqualTo(canonical.x).Within(0.0001f));
                Assert.That(animator.GetFloat("MoveY"), Is.EqualTo(canonical.y).Within(0.0001f));
            }
            view.RestoreInitialFacing();
            Assert.That(view.CurrentFacing, Is.EqualTo(CharacterVisualDirection.South));
            Assert.That(animator.GetFloat("MoveX"), Is.Zero);
            Assert.That(animator.GetFloat("MoveY"), Is.EqualTo(-1f));
            npc.SetActive(false);
            Assert.That(view.CurrentFacing, Is.EqualTo(CharacterVisualDirection.South));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(npc);
        }
    }

    [Test]
    public void CompositeSpritesheet_RemainsAvailableAsReferenceArt()
    {
        TextureImporter importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
        Assert.That(AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().Count(), Is.EqualTo(18));
    }

    private static AnimatorOverrideController LoadOverrides(NpcCase npc) =>
        AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(
            $"Assets/Animations/NPCs/{npc.AssetFolder}/{npc.ClipPrefix}Idle.overrideController");

    private static EditorCurveBinding SpriteBinding(string path) => new EditorCurveBinding
    {
        path = path,
        type = typeof(SpriteRenderer),
        propertyName = "m_Sprite"
    };

    private static void AssertConstantSorting(AnimationClip clip, string path, float expected)
    {
        EditorCurveBinding binding = AnimationUtility.GetCurveBindings(clip)
            .Single(curve => curve.path == path && curve.propertyName == "m_SortingOrder");
        AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, binding);
        Assert.That(curve.keys.Select(key => key.value), Is.All.EqualTo(expected), clip.name + "/" + path);
    }
}
