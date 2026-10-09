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
        Assert.That(prefab.GetComponent<NetworkObject>().NetworkedBehaviours, Does.Contain(runtimes[0]));
    }

    [Test]
    public void RaidAvatar_ComposesChargeAndSeismicStrikeOnceEachWithTheirCanonicalDefinitions()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var runtime = prefab.GetComponent<PlayerAbilityRuntimeNetworkController>();
        var charge = prefab.GetComponents<ChargeAbilityBehaviour>();
        var seismic = prefab.GetComponents<SeismicStrikeAbilityBehaviour>();
        Assert.That(charge, Has.Length.EqualTo(1));
        Assert.That(seismic, Has.Length.EqualTo(1));
        Assert.That(charge[0].Definition,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(
                "Assets/Scriptable Objects/Abilities/Definitions/ChargeDefinition.asset")));
        Assert.That(seismic[0].Definition,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(
                "Assets/Scriptable Objects/Abilities/Definitions/SeismicStrikeDefinition.asset")));
        Assert.That(charge[0].Definition.Id, Is.EqualTo("charge"));
        Assert.That(seismic[0].Definition.Id, Is.EqualTo("seismic_strike"));

        var bindings = new SerializedObject(runtime).FindProperty("_executionBehaviours");
        Assert.That(bindings.arraySize, Is.EqualTo(3), "Charge, Seismic Strike and Trap.");
        var registered = new[]
        {
            bindings.GetArrayElementAtIndex(0).objectReferenceValue,
            bindings.GetArrayElementAtIndex(1).objectReferenceValue
        };
        Assert.That(registered, Does.Contain(charge[0]));
        Assert.That(registered, Does.Contain(seismic[0]));
        Assert.That(registered[0], Is.Not.SameAs(registered[1]), "No behaviour is registered twice.");

        // Mirrors the runtime's own binding validation: canonical catalog definitions, no duplicates, same avatar.
        var catalog = (AbilityDefinitionCatalog)new SerializedObject(runtime).FindProperty("_catalog").objectReferenceValue;
        foreach (AbilityExecutionBehaviour behaviour in registered)
        {
            Assert.That(behaviour.gameObject, Is.SameAs(prefab));
            Assert.That(catalog.TryGetId(behaviour.Definition, out _), Is.True);
        }
        Assert.That(((AbilityExecutionBehaviour)registered[0]).Definition,
            Is.Not.SameAs(((AbilityExecutionBehaviour)registered[1]).Definition));
    }

    [Test]
    public void RaidAvatar_ChargeTargetsTheLayersOfTheCreatureDamageColliders()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var chargeMask = new SerializedObject(prefab.GetComponent<ChargeAbilityBehaviour>())
            .FindProperty("_targetLayerMask").intValue;
        var finderMask = new SerializedObject(prefab.GetComponent<AbilityAreaTargetFinder>())
            .FindProperty("_targetLayerMask").intValue;
        Assert.That(chargeMask, Is.Not.Zero);
        Assert.That(chargeMask, Is.EqualTo(finderMask), "Charge and the area finder both target the creature hitbox layers.");
        Assert.That(chargeMask, Is.EqualTo(1 << LayerMask.NameToLayer("Character")));
        var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/NetworkEnemy.prefab");
        Assert.That(enemy.layer, Is.EqualTo(LayerMask.NameToLayer("Character")),
            "The creature root hitbox layer is the one both abilities target.");
    }

    [Test]
    public void RaidAvatar_ComposesTrapOnceWithItsCanonicalDefinitionPrefabAndLayers()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var runtime = prefab.GetComponent<PlayerAbilityRuntimeNetworkController>();
        var traps = prefab.GetComponents<TrapAbilityBehaviour>();
        Assert.That(traps, Has.Length.EqualTo(1));
        Assert.That(traps[0].Definition,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(
                "Assets/Scriptable Objects/Abilities/Definitions/TrapDefinition.asset")));
        Assert.That(traps[0].Definition.Id, Is.EqualTo("trap"));

        var bindings = new SerializedObject(runtime).FindProperty("_executionBehaviours");
        int registered = 0;
        for (int i = 0; i < bindings.arraySize; i++)
            if (bindings.GetArrayElementAtIndex(i).objectReferenceValue == traps[0]) registered++;
        Assert.That(registered, Is.EqualTo(1), "Registered exactly once.");
        Assert.That(bindings.arraySize, Is.EqualTo(3));

        var serialized = new SerializedObject(traps[0]);
        Assert.That(System.IO.File.ReadAllText("Assets/Prefabs/NetworkPlayer.prefab"),
            Does.Contain("RawGuidValue: f350a40a118648c4e8e6edf70efaa3b5"), "The Trap behaviour references the NetworkTrap prefab.");
        Assert.That(serialized.FindProperty("_groundBlockingMask").intValue,
            Is.EqualTo(LayerMask.GetMask("WorldCollision", "Obstacles")));
        Assert.That(serialized.FindProperty("_targetLayerMask").intValue, Is.EqualTo(LayerMask.GetMask("Character")));
        var trapPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Abilities/NetworkTrap.prefab");
        Assert.That(AssetDatabase.AssetPathToGUID("Assets/Prefabs/Abilities/NetworkTrap.prefab"),
            Is.EqualTo("f350a40a118648c4e8e6edf70efaa3b5"));
        Assert.That(trapPrefab.GetComponent<NetworkTrap>(), Is.Not.Null);
    }
}

