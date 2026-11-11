#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEditor;
using UnityEngine;

public sealed class TownTrainerNpcPrefabTests
{
    private const string PrefabPath = "Assets/Prefabs/TownTrainerNpc.prefab";

    [Test]
    public void SocialPlayerPrefab_HasSerializedTrainerBindingAndDependencies()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SocialPlayer.prefab");
        Assert.That(player, Is.Not.Null);
        var binder = player.GetComponent<TownInventoryBinder>();
        Assert.That(binder, Is.Not.Null);
        var serializedBinder = new SerializedObject(binder);
        var trainer = serializedBinder.FindProperty("_trainerPresenter").objectReferenceValue as TownTrainerPresenter;
        Assert.That(trainer, Is.Not.Null, "The trainer must be serialized, never silently added at runtime.");
        Assert.That(trainer.gameObject, Is.EqualTo(binder.gameObject));
        Assert.That(player.GetComponents<TownTrainerPresenter>().Length, Is.EqualTo(1));
        var serializedTrainer = new SerializedObject(trainer);
        Assert.That(serializedTrainer.FindProperty("_interactionController").objectReferenceValue,
            Is.EqualTo(player.GetComponent<PlayerInteractionNetworkController>()));
        Assert.That(serializedTrainer.FindProperty("_dialoguePresenter").objectReferenceValue,
            Is.EqualTo(player.GetComponent<DialoguePresenter>()));
        Assert.That(serializedTrainer.FindProperty("_lootCatalog").objectReferenceValue,
            Is.EqualTo(serializedBinder.FindProperty("_lootCatalog").objectReferenceValue));
        // The view is deliberately built on first interaction; no view prefab is required.
        Assert.That(serializedTrainer.FindProperty("_view").objectReferenceValue, Is.Null);
        var runtimeOwner = new GameObject("Trainer runtime view factory test");
        try
        {
            var runtime = runtimeOwner.AddComponent<TownTrainerPresenter>();
            runtime.BindEndpoint(null, null, serializedTrainer.FindProperty("_lootCatalog").objectReferenceValue as LootDefinitionCatalog);
            Assert.That(runtime.View, Is.Null);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, "[TownTrainerPresenter] Falta el endpoint de armas de entrenamiento.");
            runtime.OpenForTarget(null);
            Assert.That(runtime.View, Is.Not.Null, "An intentionally null prefab view must be created at runtime.");
            Assert.That(runtime.IsOpen, Is.True);
        }
        finally { Object.DestroyImmediate(runtimeOwner); }
    }

    [Test]
    public void TrainerNpcPrefab_HasNetworkObjectAndTrainerInteractable()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(prefab, Is.Not.Null, "TownTrainerNpc.prefab must exist in Assets/Prefabs/");

        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        Assert.That(networkObject, Is.Not.Null, "TownTrainerNpc must have a NetworkObject.");

        TownTrainerNpcInteractable interactable = prefab.GetComponent<TownTrainerNpcInteractable>();
        Assert.That(interactable, Is.Not.Null, "TownTrainerNpc must have TownTrainerNpcInteractable component.");
        Assert.That(interactable, Is.InstanceOf<IInteractable>(), "TownTrainerNpcInteractable must implement IInteractable.");

        InteractionPromptMetadata prompt = prefab.GetComponent<InteractionPromptMetadata>();
        Assert.That(prompt, Is.Not.Null, "TownTrainerNpc must have InteractionPromptMetadata.");
        Assert.That(prompt.PromptText, Is.Not.Null.And.Not.Empty);

        TownNpcDialogueTrigger trigger = prefab.GetComponent<TownNpcDialogueTrigger>();
        Assert.That(trigger, Is.Not.Null, "TownTrainerNpc must have TownNpcDialogueTrigger.");
        Assert.That(trigger.PrimarySequence, Is.Not.Null);
        Assert.That(trigger.PrimarySequence.name, Is.EqualTo("TownTrainerNpcDialogue"));

        TownNpcDirectionalView directionalView = prefab.GetComponent<TownNpcDirectionalView>();
        Assert.That(directionalView, Is.Not.Null, "TownTrainerNpc must have TownNpcDirectionalView.");

        Transform visualRoot = prefab.transform.Find("VisualRoot");
        Assert.That(visualRoot, Is.Not.Null, "TownTrainerNpc must contain VisualRoot identical to other town NPCs.");
        var stash = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TownStashNpc.prefab");
        Assert.That(stash, Is.Not.Null);
        var stashVisual = stash.transform.Find("VisualRoot");
        Assert.That(stashVisual, Is.Not.Null);
        var trainerRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);
        var stashRenderers = stashVisual.GetComponentsInChildren<SpriteRenderer>(true);
        Assert.That(trainerRenderers.Length, Is.GreaterThan(0).And.EqualTo(stashRenderers.Length));
        for (int index = 0; index < trainerRenderers.Length; index++)
            Assert.That(trainerRenderers[index].sprite, Is.EqualTo(stashRenderers[index].sprite),
                "Trainer must temporarily reuse the stash NPC's art.");
    }
}
#endif
