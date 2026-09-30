using System;
using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class TownNpcPresentationConfigurationTests
{
    private const string SheetPath = "Assets/Art/NPCs/NPCs-spritesheet.png";
    private static readonly string[] Parts = { "Legs", "Body", "Head", "LeftHand", "RightHand" };
    private static readonly string[] Suffixes = { "N", "NE", "NW", "S", "SE", "SW" };
    private static readonly CharacterVisualDirection[] Directions =
    {
        CharacterVisualDirection.North, CharacterVisualDirection.NorthEast,
        CharacterVisualDirection.NorthWest, CharacterVisualDirection.South,
        CharacterVisualDirection.SouthEast, CharacterVisualDirection.SouthWest
    };
    private static readonly int[] BaseOrders = { 0, 2, 6, 4, 30 };

    [TestCase("TownStashNpc", "TownStashNpcDialogue", typeof(TownStashNpcInteractable))]
    [TestCase("TownRaidNpc", "TownRaidNpcDialogue", typeof(TownRaidNpcInteractable))]
    [TestCase("Merchant", "TownMerchantNpcDialogue", typeof(TownMerchantNpcInteractable))]
    public void Prefab_HasConfiguredModularBodyAndOneInteractionEndpoint(
        string prefabName, string sequenceName, Type interactableType)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{prefabName}.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).OfType<IInteractable>().Count(), Is.EqualTo(1));
        Assert.That(prefab.GetComponent(interactableType), Is.Not.Null);
        Assert.That(prefab.GetComponent<DialogueInteractable>(), Is.Null);

        TownNpcDialogueTrigger trigger = prefab.GetComponent<TownNpcDialogueTrigger>();
        Assert.That(trigger, Is.Not.Null);
        Assert.That((object)trigger is IInteractable, Is.False);
        Assert.That(trigger.PrimarySequence, Is.Not.Null);
        Assert.That(trigger.PrimarySequence.name, Is.EqualTo(sequenceName));
        Assert.That(trigger.PrimarySequence.Lines, Has.Length.GreaterThan(0));

        TownNpcDirectionalView view = prefab.GetComponent<TownNpcDirectionalView>();
        Assert.That(view, Is.Not.Null);
        Assert.That(view.InitialFacing, Is.EqualTo(CharacterVisualDirection.South));
        Transform visualRoot = prefab.transform.Find("VisualRoot");
        Assert.That(visualRoot, Is.Not.Null);
        Assert.That(visualRoot.childCount, Is.EqualTo(Parts.Length));
        Assert.That(visualRoot.Cast<Transform>().Select(child => child.name), Is.EquivalentTo(Parts));
        Assert.That(visualRoot.localPosition, Is.EqualTo(new Vector3(0f, 0.75f, 0f)));
        Assert.That(prefab.GetComponentsInChildren<SpriteRenderer>(true), Has.Length.EqualTo(Parts.Length));

        SerializedObject serializedView = new SerializedObject(view);
        for (int partIndex = 0; partIndex < Parts.Length; partIndex++)
        {
            string part = Parts[partIndex];
            string field = char.ToLowerInvariant(part[0]) + part.Substring(1);
            Transform child = visualRoot.Find(part);
            Assert.That(child, Is.Not.Null, $"{prefabName}/{part}");
            Assert.That(child.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(child.localScale, Is.EqualTo(Vector3.one));
            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Assert.That(child.GetComponents<SpriteRenderer>(), Has.Length.EqualTo(1));
            Assert.That(renderer.sortingLayerName, Is.EqualTo("Characters"));
            Assert.That(renderer.sortingOrder, Is.EqualTo(BaseOrders[partIndex]));
            Assert.That(renderer.sharedMaterial.name, Is.EqualTo("Sprite-Lit-Default"));
            Assert.That(serializedView.FindProperty($"_{field}Renderer").objectReferenceValue, Is.SameAs(renderer));

            SerializedProperty set = serializedView.FindProperty($"_{field}Sprites");
            Assert.That(set, Is.Not.Null);
            for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                Sprite sprite = set.FindPropertyRelative(DirectionField(Directions[directionIndex]))
                    .objectReferenceValue as Sprite;
                Assert.That(sprite, Is.Not.Null, $"{prefabName}/{part}/{Suffixes[directionIndex]}");
                Assert.That(sprite.name, Is.EqualTo($"Player-{part}-{Suffixes[directionIndex]}_0"));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(48f, 48f)));
                if (Directions[directionIndex] == CharacterVisualDirection.South)
                {
                    Assert.That(renderer.sprite, Is.SameAs(sprite));
                }
            }
        }
    }

    [Test]
    public void CompositeSpritesheet_RemainsAvailableAsReferenceArt()
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
        Assert.That(AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().Count(), Is.EqualTo(18));
    }

    [Test]
    public void DirectionalView_UpdatesAndRestoresAllFiveParts()
    {
        GameObject npc = new GameObject("NPC");
        npc.SetActive(false);
        try
        {
            TownNpcDirectionalView view = npc.AddComponent<TownNpcDirectionalView>();
            SerializedObject serializedView = new SerializedObject(view);
            SpriteRenderer[] renderers = new SpriteRenderer[Parts.Length];
            for (int partIndex = 0; partIndex < Parts.Length; partIndex++)
            {
                string part = Parts[partIndex];
                string field = char.ToLowerInvariant(part[0]) + part.Substring(1);
                renderers[partIndex] = new GameObject(part).AddComponent<SpriteRenderer>();
                renderers[partIndex].transform.SetParent(npc.transform, false);
                serializedView.FindProperty($"_{field}Renderer").objectReferenceValue = renderers[partIndex];
                SerializedProperty set = serializedView.FindProperty($"_{field}Sprites");
                for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
                {
                    string path = $"Assets/Art/Character/{part}/Directions/Player-{part}-{Suffixes[directionIndex]}.png";
                    Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Single();
                    set.FindPropertyRelative(DirectionField(Directions[directionIndex])).objectReferenceValue = sprite;
                }
            }
            serializedView.ApplyModifiedPropertiesWithoutUndo();
            npc.SetActive(true);

            for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                CharacterVisualDirection direction = Directions[directionIndex];
                view.FaceTarget(npc.transform.position + (Vector3)CharacterVisualDirectionResolver.GetCanonicalVector(direction));
                Assert.That(view.CurrentFacing, Is.EqualTo(direction));
                for (int partIndex = 0; partIndex < Parts.Length; partIndex++)
                {
                    Assert.That(renderers[partIndex].sprite.name,
                        Is.EqualTo($"Player-{Parts[partIndex]}-{Suffixes[directionIndex]}_0"));
                }
                Assert.That(renderers[4].sortingOrder,
                    Is.EqualTo(direction == CharacterVisualDirection.NorthWest ? -2 : 30));
            }

            view.RestoreInitialFacing();
            Assert.That(view.CurrentFacing, Is.EqualTo(CharacterVisualDirection.South));
            for (int partIndex = 0; partIndex < Parts.Length; partIndex++)
            {
                Assert.That(renderers[partIndex].sprite.name, Is.EqualTo($"Player-{Parts[partIndex]}-S_0"));
            }
            Assert.That(renderers[4].sortingOrder, Is.EqualTo(30));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(npc);
        }
    }

    private static string DirectionField(CharacterVisualDirection direction)
    {
        string name = direction.ToString();
        return "_" + char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
