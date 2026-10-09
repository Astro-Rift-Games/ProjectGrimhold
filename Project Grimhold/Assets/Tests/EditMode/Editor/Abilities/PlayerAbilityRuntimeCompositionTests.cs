using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class PlayerAbilityRuntimeCompositionTests
{
    [Test]
    public void RaidAvatar_HasOneRuntimeWithExplicitParticipantAndAuthorizedCatalog()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var runtimes = prefab.GetComponents<PlayerAbilityRuntimeNetworkController>();
        Assert.That(runtimes, Has.Length.EqualTo(1));
        var serialized = new SerializedObject(runtimes[0]);
        Assert.That(serialized.FindProperty("_participantLink").objectReferenceValue,
            Is.SameAs(prefab.GetComponent<RaidAvatarParticipantLink>()));
        var catalog = AssetDatabase.LoadAssetAtPath<AbilityDefinitionCatalog>(
            "Assets/Scriptable Objects/Abilities/Catalogs/AbilityDefinitionCatalog.asset");
        Assert.That(serialized.FindProperty("_catalog").objectReferenceValue, Is.SameAs(catalog));
        Assert.That(serialized.FindProperty("_playerCharacter").objectReferenceValue,
            Is.SameAs(prefab.GetComponent<PlayerCharacter>()));
        Assert.That(serialized.FindProperty("_staminaController").objectReferenceValue,
            Is.SameAs(prefab.GetComponent<PlayerStaminaNetworkController>()));
        Assert.That(serialized.FindProperty("_movementController").objectReferenceValue,
            Is.SameAs(prefab.GetComponent<PlayerMovementNetworkController>()),
            "The runtime reads the networked aim from the avatar's movement controller.");
        var finder = prefab.GetComponent<AbilityAreaTargetFinder>();
        Assert.That(finder, Is.Not.Null);
        Assert.That(serialized.FindProperty("_areaTargetFinder").objectReferenceValue, Is.SameAs(finder));
        var finderSerialized = new SerializedObject(finder);
        Assert.That(finderSerialized.FindProperty("_targetQuery").objectReferenceValue,
            Is.SameAs(prefab.GetComponent<Physics2DAttackTargetQuery>()),
            "Area abilities reuse the avatar's existing attack target query.");
        var combatSerialized = new SerializedObject(prefab.GetComponent<PlayerCombatNetworkController>());
        Assert.That(finderSerialized.FindProperty("_origin").objectReferenceValue,
            Is.SameAs(combatSerialized.FindProperty("_attackOrigin").objectReferenceValue),
            "The area is centered on the same point the equipped weapon uses.");
        Assert.That(finderSerialized.FindProperty("_targetLayerMask").intValue,
            Is.EqualTo(1 << LayerMask.NameToLayer("Character")),
            "Creature damage hitboxes live on the Character layer, like the melee target mask.");
        Assert.That(finder.IsConfigured, Is.True);
        Assert.That(serialized.FindProperty("_executionBehaviours").arraySize, Is.Zero,
            "Concrete effects are not implemented by TASK447; production must not compose a placeholder.");
        Assert.That(prefab.GetComponent<NetworkObject>().NetworkedBehaviours, Does.Contain(runtimes[0]));
    }
}
