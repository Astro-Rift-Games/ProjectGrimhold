using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class PlayerAbilityRuntimeCompositionTests
{
    private const int ProductionBindingCount = 9;
    private const string ProductionBindingOrder =
        "Charge, Seismic Strike, Trap, Arcane Projectile, Protective Orbs, Empower, Restoration, Purification and Vital Drain.";

    [TestCase(typeof(EmpowerAbilityBehaviour), "EmpowerDefinition", "empower")]
    [TestCase(typeof(RestorationAbilityBehaviour), "RestorationDefinition", "restoration")]
    [TestCase(typeof(PurificationAbilityBehaviour), "PurificationDefinition", "purification")]
    public void RaidAvatar_ComposesSupportAbilityOnceWithItsCanonicalDefinition(
        System.Type behaviourType, string definitionAsset, string expectedId)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var behaviours = prefab.GetComponents(behaviourType);
        Assert.That(behaviours, Has.Length.EqualTo(1));
        var behaviour = (AbilityExecutionBehaviour)behaviours[0];
        Assert.That(behaviour.Definition, Is.SameAs(AssetDatabase.LoadAssetAtPath<AbilityDefinition>(
            $"Assets/Scriptable Objects/Abilities/Definitions/{definitionAsset}.asset")));
        Assert.That(behaviour.Definition.Id, Is.EqualTo(expectedId));

        var bindings = new SerializedObject(prefab.GetComponent<PlayerAbilityRuntimeNetworkController>())
            .FindProperty("_executionBehaviours");
        int registered = 0;
        for (int i = 0; i < bindings.arraySize; i++)
            if (bindings.GetArrayElementAtIndex(i).objectReferenceValue == behaviour) registered++;
        Assert.That(registered, Is.EqualTo(1), "Registered exactly once.");
    }

    [Test]
    public void RaidAvatar_ComposesSupportEffectsOnItsNetworkRootAndFeedsEmpowerToBasicAttacks()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var character = prefab.GetComponent<PlayerCharacter>();
        var networkObject = prefab.GetComponent<NetworkObject>();
        var empower = prefab.GetComponents<EmpowerNetworkEffect>();
        var purification = prefab.GetComponents<PurificationNetworkEffect>();
        Assert.That(empower, Has.Length.EqualTo(1));
        Assert.That(purification, Has.Length.EqualTo(1));
        Assert.That(new SerializedObject(empower[0]).FindProperty("_character").objectReferenceValue, Is.SameAs(character));
        Assert.That(new SerializedObject(purification[0]).FindProperty("_character").objectReferenceValue, Is.SameAs(character));
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(empower[0]));
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(purification[0]));
        Assert.That(new SerializedObject(prefab.GetComponent<PlayerCombatNetworkController>())
                .FindProperty("_empowerEffect").objectReferenceValue, Is.SameAs(empower[0]),
            "Basic weapon attacks read the avatar's own Empower multiplier.");
    }

    [TestCase(false, false, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    public void Combat_EmpowerReferenceIsRequiredOnlyForComposedCapability(
        bool composeEffect, bool composeBehaviour, bool expectedValid)
    {
        var avatar = new GameObject("InactiveCombatConfiguration");
        avatar.SetActive(false);
        try
        {
            var character = avatar.AddComponent<PlayerCharacter>();
            // Movement requires a motor, which requires a concrete Collider2D (the base type is abstract).
            avatar.AddComponent<Rigidbody2D>();
            avatar.AddComponent<BoxCollider2D>();
            avatar.AddComponent<Kinematic2DMovementMotor>();
            var movement = avatar.AddComponent<PlayerMovementNetworkController>();
            Assert.That(movement, Is.Not.Null, "Combat's required movement dependency must be composed.");
            var combat = avatar.AddComponent<PlayerCombatNetworkController>();
            if (composeEffect) avatar.AddComponent<EmpowerNetworkEffect>();
            if (composeBehaviour) avatar.AddComponent<EmpowerAbilityBehaviour>();
            SetPrivate(combat, "_character", character);
            SetPrivate(combat, "_movementController", movement);
            SetPrivate(combat, "_attackOrigin", avatar.transform);
            if (!expectedValid)
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                    "PlayerCombatNetworkController requires an assigned same-avatar EmpowerNetworkEffect on an Empower-capable avatar.");
            Assert.That(ValidateCombat(combat), Is.EqualTo(expectedValid));
        }
        finally { Object.DestroyImmediate(avatar); }
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void Combat_EmpowerReferenceRejectsForeignEffectOrMalformedOwner(bool foreignEffect, bool foreignOwner)
    {
        var avatar = new GameObject("InactiveCombatConfiguration");
        var other = new GameObject("InactiveOtherAvatar");
        avatar.SetActive(false);
        other.SetActive(false);
        try
        {
            var character = avatar.AddComponent<PlayerCharacter>();
            var otherCharacter = other.AddComponent<PlayerCharacter>();
            avatar.AddComponent<Rigidbody2D>();
            avatar.AddComponent<BoxCollider2D>();
            avatar.AddComponent<Kinematic2DMovementMotor>();
            var movement = avatar.AddComponent<PlayerMovementNetworkController>();
            Assert.That(movement, Is.Not.Null, "Combat's required movement dependency must be composed.");
            var combat = avatar.AddComponent<PlayerCombatNetworkController>();
            var effect = (foreignEffect ? other : avatar).AddComponent<EmpowerNetworkEffect>();
            SetPrivate(effect, "_character", foreignOwner || foreignEffect ? otherCharacter : character);
            SetPrivate(combat, "_character", character);
            SetPrivate(combat, "_movementController", movement);
            SetPrivate(combat, "_attackOrigin", avatar.transform);
            SetPrivate(combat, "_empowerEffect", effect);
            bool expectedValid = !foreignEffect && !foreignOwner;
            if (!expectedValid)
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                    "PlayerCombatNetworkController requires its assigned EmpowerNetworkEffect to have a valid same-avatar owner.");
            Assert.That(ValidateCombat(combat), Is.EqualTo(expectedValid));
        }
        finally
        {
            Object.DestroyImmediate(avatar);
            Object.DestroyImmediate(other);
        }
    }

    [TestCase(typeof(EmpowerNetworkEffect))]
    [TestCase(typeof(PurificationNetworkEffect))]
    public void SupportEffect_ReadinessRejectsMissingForeignAndUnspawnedOwners(System.Type effectType)
    {
        var avatar = new GameObject("InactiveEffectConfiguration");
        var other = new GameObject("InactiveOtherAvatar");
        avatar.SetActive(false);
        other.SetActive(false);
        try
        {
            var character = avatar.AddComponent<PlayerCharacter>();
            var foreign = other.AddComponent<PlayerCharacter>();
            var effect = avatar.AddComponent(effectType);
            var ownerQuery = effectType.GetProperty("IsOwnerConfigured");
            var readinessQuery = effectType.GetProperty("IsReadyToApply");
            Assert.That(ownerQuery.GetValue(effect), Is.False);
            Assert.That(readinessQuery.GetValue(effect), Is.False);
            SetPrivate(effect, "_character", foreign);
            Assert.That(ownerQuery.GetValue(effect), Is.False);
            Assert.That(readinessQuery.GetValue(effect), Is.False);
            SetPrivate(effect, "_character", character);
            Assert.That(ownerQuery.GetValue(effect), Is.True);
            Assert.That(readinessQuery.GetValue(effect), Is.False, "An unspawned effect cannot write Fusion state.");
        }
        finally
        {
            Object.DestroyImmediate(avatar);
            Object.DestroyImmediate(other);
        }
    }

    private static void SetPrivate(object target, string name, object value) =>
        target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .SetValue(target, value);

    private static bool ValidateCombat(PlayerCombatNetworkController combat) =>
        (bool)typeof(PlayerCombatNetworkController)
            .GetMethod("ValidateDependencies", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(combat, null);

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
    public void RaidAvatar_EveryCatalogDefinitionResolvesExactlyOneSameRootBehaviour()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        var runtime = prefab.GetComponent<PlayerAbilityRuntimeNetworkController>();
        var catalog = (AbilityDefinitionCatalog)new SerializedObject(runtime).FindProperty("_catalog").objectReferenceValue;
        Assert.That(catalog.TryValidate(out string catalogError), Is.True, catalogError);
        Assert.That(catalog.DefinitionCount, Is.EqualTo(ProductionBindingCount), "One binding per MVP catalog ability.");

        var bindings = new SerializedObject(runtime).FindProperty("_executionBehaviours");
        Assert.That(bindings.arraySize, Is.EqualTo(ProductionBindingCount), ProductionBindingOrder);
        foreach (AbilityDefinition definition in catalog.Definitions)
        {
            int resolved = 0;
            for (int i = 0; i < bindings.arraySize; i++)
            {
                var behaviour = (AbilityExecutionBehaviour)bindings.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(behaviour, Is.Not.Null, $"Binding {i} is missing.");
                Assert.That(behaviour.gameObject, Is.SameAs(prefab), $"Binding {i} lives on the avatar root.");
                if (behaviour.Definition == definition) resolved++;
            }
            Assert.That(resolved, Is.EqualTo(1), $"'{definition.Id}' resolves exactly one concrete behaviour.");
        }
    }

    [Test]
    public void RaidAvatar_ArcaneProjectileSpawnsItsOwnNetworkPrefabSeparateFromTheRangedWeapon()
    {
        const string arcanePath = "Assets/Prefabs/Abilities/ArcaneProjectile.prefab";
        string arcaneGuid = AssetDatabase.AssetPathToGUID(arcanePath);
        Assert.That(arcaneGuid, Is.Not.Empty, "The ability owns a dedicated projectile prefab.");
        Assert.That(System.IO.File.ReadAllText("Assets/Prefabs/NetworkPlayer.prefab"),
            Does.Contain($"RawGuidValue: {arcaneGuid}"), "Arcane Projectile references its own prefab.");
        Assert.That(System.IO.File.ReadAllText("Assets/Scriptable Objects/RangePlayerAttackConfig.asset"),
            Does.Not.Contain(arcaneGuid), "The basic ranged weapon does not share the ability projectile.");

        var projectile = AssetDatabase.LoadAssetAtPath<GameObject>(arcanePath);
        Assert.That(AssetDatabase.GetLabels(projectile), Does.Contain("FusionPrefab"));
        var networkObject = projectile.GetComponent<NetworkObject>();
        Assert.That(networkObject, Is.Not.Null);
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(projectile.GetComponent<NetworkProjectile>()));
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(projectile.GetComponent<DamageResolver>()),
            "Unbounded ability flight resolves damage through the projectile's own resolver.");
        AssertHidesItsRenderersOutsideLocalVision(projectile);
    }

    [Test]
    public void TrapEntity_HidesItsRendererOutsideLocalVision()
    {
        var trap = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Abilities/NetworkTrap.prefab");
        AssertHidesItsRenderersOutsideLocalVision(trap);
    }

    private static void AssertHidesItsRenderersOutsideLocalVision(GameObject prefab)
    {
        var presenters = prefab.GetComponents<ProjectGrimhold.Gameplay.Visibility.EntityVisibilityPresenter>();
        Assert.That(presenters, Has.Length.EqualTo(1));
        var renderers = new SerializedObject(presenters[0]).FindProperty("_renderers");
        Assert.That(renderers.arraySize, Is.GreaterThan(0));
        for (int i = 0; i < renderers.arraySize; i++)
        {
            var renderer = renderers.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
            Assert.That(renderer, Is.Not.Null, $"Visibility renderer {i} is assigned.");
            Assert.That(renderer.transform.IsChildOf(prefab.transform), Is.True);
        }
        Assert.That(prefab.GetComponentsInChildren<SpriteRenderer>(true),
            Is.SubsetOf(Enumerate(renderers)), "Every sprite of the entity is hidden together.");
    }

    private static System.Collections.Generic.List<Object> Enumerate(SerializedProperty array)
    {
        var items = new System.Collections.Generic.List<Object>();
        for (int i = 0; i < array.arraySize; i++) items.Add(array.GetArrayElementAtIndex(i).objectReferenceValue);
        return items;
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
        Assert.That(bindings.arraySize, Is.EqualTo(ProductionBindingCount), ProductionBindingOrder);
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
        Assert.That(bindings.arraySize, Is.EqualTo(ProductionBindingCount), ProductionBindingOrder);

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

