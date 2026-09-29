using System.Linq;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class TownNpcPresentationConfigurationTests
{
    private const string SheetPath = "Assets/Art/NPCs/NPCs-spritesheet.png";
    private static readonly string[] Directions = { "N", "NE", "NW", "S", "SE", "SW" };
    private static readonly CharacterVisualDirection[] FacingDirections =
    {
        CharacterVisualDirection.North,
        CharacterVisualDirection.NorthEast,
        CharacterVisualDirection.NorthWest,
        CharacterVisualDirection.South,
        CharacterVisualDirection.SouthEast,
        CharacterVisualDirection.SouthWest
    };

    [TestCase("TownStashNpc", "StashNpc", "TownStashNpcDialogue", typeof(TownStashNpcInteractable))]
    [TestCase("TownRaidNpc", "RaidNpc", "TownRaidNpcDialogue", typeof(TownRaidNpcInteractable))]
    [TestCase("Merchant", "MerchantNpc", "TownMerchantNpcDialogue", typeof(TownMerchantNpcInteractable))]
    public void TownNpcPrefab_HasOneInteractionEndpointAndCompleteDirectionalDialogue(
        string prefabName, string spritePrefix, string sequenceName, System.Type interactableType)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/{prefabName}.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);

        MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        Assert.That(behaviours.OfType<IInteractable>().Count(), Is.EqualTo(1));
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
        SpriteRenderer renderer = prefab.GetComponentsInChildren<SpriteRenderer>(true).Single();
        SerializedObject serializedView = new SerializedObject(view);
        Assert.That(serializedView.FindProperty("_spriteRenderer").objectReferenceValue, Is.SameAs(renderer));

        for (int i = 0; i < Directions.Length; i++)
        {
            Sprite sprite = view.GetSprite(FacingDirections[i]);
            Assert.That(sprite, Is.Not.Null, $"{prefabName} {Directions[i]}");
            Assert.That(sprite.name, Is.EqualTo($"{spritePrefix}-{Directions[i]}"));
        }

        Assert.That(renderer.sprite, Is.SameAs(view.GetSprite(CharacterVisualDirection.South)));
    }

    [Test]
    public void Spritesheet_HasAlignedUncompressedSixViewsForEachNpc()
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        Assert.That(importer, Is.Not.Null);
        Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
        Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(48));
        Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));

        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToArray();
        Assert.That(sprites, Has.Length.EqualTo(18));
        string[] prefixes = { "StashNpc", "RaidNpc", "MerchantNpc" };
        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites.Single(s => s.name == $"{prefixes[i / 6]}-{Directions[i % 6]}");
            Assert.That(sprite.rect, Is.EqualTo(new Rect((i % 4) * 288 + 108, 1248 - (i / 4) * 288, 72, 96)));
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(36, 12)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(48));
        }
    }

    [Test]
    public void DirectionalView_RestoresAuthoredFacingAfterTemporaryTurn()
    {
        GameObject npc = new GameObject("NPC");
        try
        {
            SpriteRenderer renderer = npc.AddComponent<SpriteRenderer>();
            TownNpcDirectionalView view = npc.AddComponent<TownNpcDirectionalView>();
            SerializedObject serializedView = new SerializedObject(view);
            serializedView.FindProperty("_spriteRenderer").objectReferenceValue = renderer;
            serializedView.FindProperty("_south").objectReferenceValue =
                AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().Single(s => s.name == "StashNpc-S");
            serializedView.FindProperty("_north").objectReferenceValue =
                AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().Single(s => s.name == "StashNpc-N");
            serializedView.ApplyModifiedPropertiesWithoutUndo();

            view.RestoreInitialFacing();
            Sprite initialSprite = renderer.sprite;
            view.FaceTarget(npc.transform.position + Vector3.up);
            Assert.That(view.CurrentFacing, Is.EqualTo(CharacterVisualDirection.North));
            Assert.That(renderer.sprite.name, Is.EqualTo("StashNpc-N"));

            view.RestoreInitialFacing();
            Assert.That(view.CurrentFacing, Is.EqualTo(CharacterVisualDirection.South));
            Assert.That(renderer.sprite, Is.SameAs(initialSprite));
        }
        finally
        {
            Object.DestroyImmediate(npc);
        }
    }
}
