using System.Collections.Generic;
using System.IO;
using Fusion;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Spawning;
using Assert = NUnit.Framework.Assert;

public sealed class SessionCompositionConfigurationTests
{
    private const string SocialPlayerPath = "Assets/Prefabs/SocialPlayer.prefab";
    private const string SystemsPath = "Assets/Prefabs/Systems.prefab";
    private const string TownRaidNpcPath = "Assets/Prefabs/TownRaidNpc.prefab";
    private const string TownRaidPreparationPath = "Assets/Prefabs/TownRaidPreparation.prefab";
    private const string TownRaidPreparationGuid = "a4c85a62e2f24d0ba0fcdb7dca91ce44";
    private const string TownRaidPreparationViewPath = "Assets/Resources/TownRaidPreparationView.prefab";
    private const string RaidParticipantPath = "Assets/Prefabs/NetworkRaidParticipant.prefab";
    private const string BaseRaidAvatarPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string MeleeRaidAvatarPath = "Assets/Prefabs/NetworkPlayerMelee.prefab";
    private const string RangedRaidAvatarPath = "Assets/Prefabs/NetworkPlayerRanged.prefab";
    private const string MainMenuCanvasPath = "Assets/Prefabs/MainMenu Canvas.prefab";
    private const string TownScenePath = "Assets/Scenes/Lobby-Town.unity";
    private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

    [Test]
    public void NetworkScenes_AreEnabledAndResolvableByConfiguredName()
    {
        Assert.That(NetworkSceneBuildIndexResolver.Resolve("Lobby-Town"), Is.GreaterThanOrEqualTo(0));
        Assert.That(NetworkSceneBuildIndexResolver.Resolve("Gameplay"), Is.GreaterThanOrEqualTo(0));
        Assert.That(NetworkSceneBuildIndexResolver.Resolve("Missing-TASK-57-Scene"), Is.EqualTo(-1));
    }

    [Test]
    public void SystemsPrefab_OwnsBothLaunchersAndOneCoordinator()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SystemsPath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<SessionConnectionCoordinator>(true), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<HubSessionLauncher>(true), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<FusionSessionLauncher>(true), Has.Length.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<DirectRaidDevelopmentStarter>(true), Has.Length.EqualTo(1));
        string participantGuid = AssetDatabase.AssetPathToGUID(RaidParticipantPath);
        Assert.That(File.ReadAllText(SystemsPath), Does.Contain($"RawGuidValue: {participantGuid}"));
        Assert.That(File.ReadAllText(SystemsPath), Does.Not.Contain("_maxPlayers:"));
        Assert.That(RaidSessionRules.MaxParticipants, Is.EqualTo(16));
    }

    [Test]
    public void RaidParticipantAndAvatars_HaveSeparatedNetworkComposition()
    {
        GameObject participantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RaidParticipantPath);

        Assert.That(participantPrefab, Is.Not.Null);
        NetworkObject participantObject = participantPrefab.GetComponent<NetworkObject>();
        NetworkRaidParticipant participant = participantPrefab.GetComponent<NetworkRaidParticipant>();
        PlayerExpeditionExperienceLedger[] ledgers =
            participantPrefab.GetComponents<PlayerExpeditionExperienceLedger>();
        PlayerExpeditionProgressionResolver[] resolvers =
            participantPrefab.GetComponents<PlayerExpeditionProgressionResolver>();
        Assert.That(participantObject, Is.Not.Null);
        Assert.That(participant, Is.Not.Null);
        Assert.That(ledgers, Has.Length.EqualTo(1));
        Assert.That(resolvers, Has.Length.EqualTo(1));
        Assert.That(participantPrefab.GetComponent<PlayerCharacter>(), Is.Null);
        Assert.That(participantObject.NetworkedBehaviours, Does.Contain(participant));
        Assert.That(participantObject.NetworkedBehaviours, Does.Contain(ledgers[0]));
        Assert.That(participantObject.NetworkedBehaviours, Does.Contain(resolvers[0]));

        GameObject socialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
        Assert.That(socialPrefab.GetComponent<PlayerExpeditionExperienceLedger>(), Is.Null);

        AssertRaidAvatarComposition(BaseRaidAvatarPath);
        AssertRaidAvatarComposition(MeleeRaidAvatarPath);
        AssertRaidAvatarComposition(RangedRaidAvatarPath);
    }

    [Test]
    public void ExpeditionExperienceLedger_ExistsOnlyOnRaidParticipantPrefab()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
        int ledgerCount = 0;

        for (int index = 0; index < prefabGuids.Length; index++)
        {
            string prefabPath = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            PlayerExpeditionExperienceLedger[] ledgers =
                prefab.GetComponentsInChildren<PlayerExpeditionExperienceLedger>(true);

            if (ledgers.Length == 0)
            {
                continue;
            }

            Assert.That(prefabPath, Is.EqualTo(RaidParticipantPath));
            ledgerCount += ledgers.Length;
        }

        Assert.That(ledgerCount, Is.EqualTo(1));
    }

    [Test]
    public void RaidParticipantAttributeSnapshot_IsUnavailableUntilInitializationAndThenExact()
    {
        Assert.That(
            NetworkRaidParticipant.TryBuildCharacterAttributeState(
                false, 1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState unavailable),
            Is.False);
        Assert.That(unavailable, Is.EqualTo(default(CharacterAttributeState)));

        Assert.That(
            CharacterAttributeState.TryCreate(
                1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState expected),
            Is.True);
        Assert.That(
            NetworkRaidParticipant.TryBuildCharacterAttributeState(
                true, 1, 2, 3, 4, 5, 6, 7, out CharacterAttributeState actual),
            Is.True);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void RaidAvatarParticipantLink_DelegatesAttributeSnapshotWithoutOwningACopy()
    {
        System.Reflection.FieldInfo[] fields = typeof(RaidAvatarParticipantLink).GetFields(
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.DeclaredOnly);
        for (int index = 0; index < fields.Length; index++)
        {
            Assert.That(fields[index].FieldType, Is.Not.EqualTo(typeof(CharacterAttributeState)));
        }

        string linkSource = File.ReadAllText(
            "Assets/Scripts/Networking/RaidAvatarParticipantLink.cs");
        Assert.That(
            linkSource,
            Does.Contain("participant.TryGetCharacterAttributeState(out state)"));
    }

    [Test]
    public void RaidAvatar_HasSerializedDefeatAndSpectatorControls()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseRaidAvatarPath);
        RaidMenuView view = prefab.GetComponentInChildren<RaidMenuView>(true);

        Assert.That(view, Is.Not.Null);
        Assert.That(view.CancelRaidButton, Is.Not.Null);
        Assert.That(view.SpectatorBarRoot, Is.Not.Null);
        Assert.That(view.SpectatorTargetText, Is.Not.Null);
        Assert.That(view.PreviousTargetButton, Is.Not.Null);
        Assert.That(view.NextTargetButton, Is.Not.Null);
        Assert.That(view.SpectatorBarRoot.activeSelf, Is.False);

        string presenterSource = File.ReadAllText(
            "Assets/Scripts/Player/Presentation/RaidMenuPresenter.cs");
        Assert.That(presenterSource, Does.Not.Contain("Instantiate("));
        Assert.That(presenterSource, Does.Not.Contain("AddComponent<"));
    }

    [Test]
    public void SocialPlayer_ContainsSocialCapabilitiesAndNoRaidCapabilities()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<NetworkObject>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<NetworkTransform>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<SocialPlayerCharacter>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerMovementNetworkController>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerInteractionNetworkController>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<LocalInteractionCandidateSource>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<SocialPlayerIdentity>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<TownRaidPreparationPresenter>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<LocalPlayerCameraBinder>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerLootReceiver>(), Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<PlayerAnimatorView>(true), Is.Not.Null);

        var forbiddenTypes = new HashSet<System.Type>
        {
            typeof(PlayerCharacter),
            typeof(PlayerCombatNetworkController),
            typeof(DamageResolver),
            typeof(PlayerLootTransferNetworkController),
            typeof(PlayerLootDropNetworkController),
            typeof(PlayerCorpseGenerationController),
            typeof(PlayerExtractionController),
            typeof(PlayerExtractionProgressController),
            typeof(PlayerExtractionLootSaver),
            typeof(RaidHudPresenter),
            typeof(RaidHudView)
        };

        MonoBehaviour[] behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int index = 0; index < behaviours.Length; index++)
        {
            Assert.That(
                forbiddenTypes.Contains(behaviours[index].GetType()),
                Is.False,
                $"SocialPlayer contains raid-only component {behaviours[index].GetType().Name}.");
        }

        Assert.That(FindChild(prefab.transform, "LocalGameplayHud"), Is.Null);
        Assert.That(FindChild(prefab.transform, "CombatVisuals"), Is.Null);
        Assert.That(FindChild(prefab.transform, "DamageHitbox"), Is.Null);
        Assert.That(FindChild(prefab.transform, "CorpseLootInteraction"), Is.Null);
        Assert.That(FindChild(prefab.transform, "VisibilityMesh"), Is.Null);

        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(prefab.GetComponent<TownRaidPreparationPresenter>()));
    }

    [Test]
    public void SocialPlayer_ContainsModularLocomotionPresentation()
    {
        GameObject socialPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
        Assert.That(socialPrefab, Is.Not.Null);

        Transform visualRoot = FindChild(socialPrefab.transform, "VisualRoot");
        Assert.That(visualRoot, Is.Not.Null, "SocialPlayer missing VisualRoot");

        Animator animator = visualRoot.GetComponent<Animator>();
        Assert.That(animator, Is.Not.Null, "VisualRoot missing Animator");

        GameObject networkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        Animator networkAnimator = FindChild(networkPrefab.transform, "VisualRoot").GetComponent<Animator>();
        Assert.That(animator.runtimeAnimatorController, Is.EqualTo(networkAnimator.runtimeAnimatorController), "SocialPlayer must use the same AnimatorController as NetworkPlayer");

        PlayerAnimatorView animatorView = visualRoot.GetComponent<PlayerAnimatorView>();
        Assert.That(animatorView, Is.Not.Null, "VisualRoot missing PlayerAnimatorView");
        
        string[] requiredSlots = { "Legs", "Body", "Head", "LeftHand", "RightHand" };
        foreach (string slotName in requiredSlots)
        {
            Transform slot = FindChild(visualRoot, slotName);
            Assert.That(slot, Is.Not.Null, $"VisualRoot missing {slotName} slot");
            Assert.That(slot.parent, Is.EqualTo(visualRoot), $"{slotName} must be a direct child of VisualRoot");

            SpriteRenderer renderer = slot.GetComponent<SpriteRenderer>();
            Assert.That(renderer, Is.Not.Null, $"{slotName} missing SpriteRenderer");

            Transform networkSlot = FindChild(networkPrefab.transform.Find("VisualRoot"), slotName);
            SpriteRenderer networkRenderer = networkSlot.GetComponent<SpriteRenderer>();

            Assert.That(renderer.sharedMaterial, Is.EqualTo(networkRenderer.sharedMaterial), $"{slotName} material mismatch");
            Assert.That(renderer.sortingLayerID, Is.EqualTo(networkRenderer.sortingLayerID), $"{slotName} sortingLayerID mismatch");
            Assert.That(slot.gameObject.layer, Is.EqualTo(networkSlot.gameObject.layer), $"{slotName} layer mismatch");
        }

        Assert.That(FindChild(visualRoot, "HandOrbitAnchor"), Is.Null, "Obsolete HandOrbitAnchor found");
        Assert.That(visualRoot.GetComponent<SpriteRenderer>(), Is.Null, "Obsolete monolithic SpriteRenderer found on VisualRoot");
    }

    [Test]
    public void TownRaidNpc_HasAuthoritativePreparationDirectoryAndInteractionComposition()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownRaidNpcPath);

        Assert.That(prefab, Is.Not.Null);
        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        TownRaidPreparationDirectory directory = prefab.GetComponent<TownRaidPreparationDirectory>();
        TownRaidNpcInteractable interactable = prefab.GetComponent<TownRaidNpcInteractable>();
        Assert.That(networkObject, Is.Not.Null);
        Assert.That(directory, Is.Not.Null);
        Assert.That(interactable, Is.Not.Null);
        Assert.That(prefab.GetComponent<InteractionPromptMetadata>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Collider2D>(), Is.Not.Null);
        Assert.That(networkObject.Flags.HasFlag(NetworkObjectFlags.MasterClientObject), Is.True);
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(directory));
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(interactable));
        Assert.That(interactable.PreparationDirectory, Is.SameAs(directory));
    }

    [Test]
    public void TownRaidPreparation_IsAUiFreeRegisteredNetworkPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownRaidPreparationPath);

        Assert.That(prefab, Is.Not.Null);
        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        TownRaidPreparationNetworkController preparation =
            prefab.GetComponent<TownRaidPreparationNetworkController>();
        Assert.That(networkObject, Is.Not.Null);
        Assert.That(preparation, Is.Not.Null);
        Assert.That(networkObject.Flags.HasFlag(NetworkObjectFlags.MasterClientObject), Is.True);
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(preparation));
        Assert.That(prefab.GetComponentInChildren<Canvas>(true), Is.Null);
        NetworkPrefabId prefabId = NetworkProjectConfig.Global.PrefabTable.GetId(
            NetworkObjectGuid.Parse(TownRaidPreparationGuid));
        Assert.That(prefabId.IsValid, Is.True);
    }

    [Test]
    public void TownRaidPreparationView_HasSerializedLeaveAndCapacityPresentation()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownRaidPreparationViewPath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<TownRaidPreparationView>(), Is.Not.Null);
        Assert.That(prefab.transform.Find("RaidCodePanel/Abandonar preparacion"), Is.Not.Null);
        TMP_Text status = prefab.transform.Find("RaidCodePanel/Status")?.GetComponent<TMP_Text>();
        Assert.That(status, Is.Not.Null);
        Assert.That(status.GetComponent<LayoutElement>().preferredHeight, Is.GreaterThanOrEqualTo(300f));
    }

    [Test]
    public void MainMenu_NormalFlowShowsTownEntryAndHidesDirectJoin()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuCanvasPath);
        MainMenuController controller = prefab.GetComponentInChildren<MainMenuController>(true);
        SerializedObject serializedController = new SerializedObject(controller);
        Button createButton = serializedController.FindProperty("createRoomButton").objectReferenceValue as Button;
        Button joinButton = serializedController.FindProperty("joinRoomButton").objectReferenceValue as Button;
        TMP_InputField roomInput = serializedController.FindProperty("roomCodeInput").objectReferenceValue as TMP_InputField;

        Assert.That(createButton, Is.Not.Null);
        Assert.That(createButton.GetComponentInChildren<TMP_Text>(true).text, Is.EqualTo("Enter Town"));
        Assert.That(joinButton.gameObject.activeSelf, Is.False);
        Assert.That(roomInput.gameObject.activeSelf, Is.False);
    }

    [Test]
    public void TownScene_HasOneSpawnConfigurationCameraAndExplicitInput()
    {
        Scene scene = SceneManager.GetSceneByPath(TownScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
        {
            scene = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Additive);
        }

        try
        {
            List<HubSpawnSceneConfiguration> configurations = FindInScene<HubSpawnSceneConfiguration>(scene);
            Assert.That(configurations, Has.Count.EqualTo(1));
            Assert.That(configurations[0].SpawnPointCount, Is.EqualTo(RaidSessionRules.MaxParticipants));
            Assert.That(configurations[0].Validate(out _), Is.True);
            Assert.That(FindInScene<HubSessionLauncher>(scene), Is.Empty);
            Assert.That(FindInScene<LocalCameraController>(scene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<PlayerInputReader>(scene), Has.Count.EqualTo(1));
            Assert.That(FindInScene<FusionInputProvider>(scene), Has.Count.EqualTo(1));
        }
        finally
        {
            if (openedForTest)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [Test]
    public void TownScene_AllSocialSpawnPointsClearActualWorldCollisionAndEachOther()
    {
        Scene scene = SceneManager.GetSceneByPath(TownScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
        {
            scene = EditorSceneManager.OpenScene(TownScenePath, OpenSceneMode.Additive);
        }

        GameObject probe = null;
        try
        {
            HubSpawnSceneConfiguration configuration = FindInScene<HubSpawnSceneConfiguration>(scene)[0];
            CompositeCollider2D worldCollision = FindInScene<CompositeCollider2D>(scene)[0];
            GameObject socialPlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SocialPlayerPath);
            BoxCollider2D sourceCollider = socialPlayerPrefab.GetComponent<BoxCollider2D>();
            Kinematic2DMovementMotor movementMotor = socialPlayerPrefab.GetComponent<Kinematic2DMovementMotor>();
            var serializedMotor = new SerializedObject(movementMotor);
            int collisionMask = serializedMotor.FindProperty("_collisionMask").intValue;

            Assert.That(sourceCollider, Is.Not.Null);
            Assert.That(worldCollision, Is.Not.Null);
            Assert.That(collisionMask & (1 << worldCollision.gameObject.layer), Is.Not.Zero);

            var serializedConfiguration = new SerializedObject(configuration);
            SerializedProperty spawnPoints = serializedConfiguration.FindProperty("_spawnPoints");
            var spawnBounds = new List<Bounds>(spawnPoints.arraySize);

            probe = new GameObject("Town social spawn collision probe");
            BoxCollider2D probeCollider = probe.AddComponent<BoxCollider2D>();
            probeCollider.size = sourceCollider.size;
            probeCollider.offset = sourceCollider.offset;

            for (int index = 0; index < spawnPoints.arraySize; index++)
            {
                Transform spawnPoint = spawnPoints.GetArrayElementAtIndex(index).objectReferenceValue as Transform;
                Assert.That(spawnPoint, Is.Not.Null, $"Town social spawn point {index} is missing.");

                probe.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
                Physics2D.SyncTransforms();
                ColliderDistance2D distance = probeCollider.Distance(worldCollision);
                Assert.That(distance.isOverlapped, Is.False, $"Town social spawn point {index} overlaps WorldCollision.");
                Assert.That(distance.distance, Is.GreaterThanOrEqualTo(0.25f),
                    $"Town social spawn point {index} is too close to WorldCollision.");

                int clearDirections = 0;
                Vector2[] directions = { Vector2.left, Vector2.right, Vector2.up, Vector2.down };
                for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
                {
                    probe.transform.position = spawnPoint.position + (Vector3)(directions[directionIndex] * 0.25f);
                    Physics2D.SyncTransforms();
                    if (!probeCollider.Distance(worldCollision).isOverlapped)
                    {
                        clearDirections++;
                    }
                }

                Assert.That(clearDirections, Is.GreaterThanOrEqualTo(2),
                    $"Town social spawn point {index} cannot begin moving in at least two directions.");

                Vector3 center = spawnPoint.position + (Vector3)sourceCollider.offset;
                spawnBounds.Add(new Bounds(center, sourceCollider.size));
            }

            for (int index = 0; index < spawnBounds.Count; index++)
            {
                for (int other = index + 1; other < spawnBounds.Count; other++)
                {
                    Assert.That(spawnBounds[index].Intersects(spawnBounds[other]), Is.False,
                        $"Town social spawn points {index} and {other} overlap using the SocialPlayer collider.");
                }
            }
        }
        finally
        {
            if (probe != null)
            {
                Object.DestroyImmediate(probe);
            }

            if (openedForTest)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    [Test]
    public void GameplayScene_HasSixteenValidUniquePlayerSpawnPoints()
    {
        Scene scene = SceneManager.GetSceneByPath(GameplayScenePath);
        bool openedForTest = !scene.IsValid() || !scene.isLoaded;
        if (openedForTest)
        {
            scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
        }

        try
        {
            List<NetworkSpawnSceneConfiguration> configurations =
                FindInScene<NetworkSpawnSceneConfiguration>(scene);
            Assert.That(configurations, Has.Count.EqualTo(1));
            Assert.That(configurations[0].Validate(out string failure), Is.True, failure);
            SpawnGroupDefinition players = System.Array.Find(
                configurations[0].SpawnGroups,
                group => group.Group == SpawnGroupType.Players);
            Assert.That(players, Is.Not.Null);
            Assert.That(players.SpawnPoints, Has.Length.EqualTo(RaidSessionRules.MaxParticipants));

            var positions = new List<Vector3>(players.SpawnPoints.Length);
            for (int index = 0; index < players.SpawnPoints.Length; index++)
            {
                positions.Add(players.SpawnPoints[index].position);
            }

            Assert.That(
                RaidParticipantSpawnRules.ValidateSpawnPositions(
                    positions,
                    RaidSessionRules.MaxParticipants,
                    out failure),
                Is.True,
                failure);
        }
        finally
        {
            if (openedForTest)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }

    private static List<T> FindInScene<T>(Scene scene) where T : Component
    {
        var found = new List<T>();
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            found.AddRange(roots[index].GetComponentsInChildren<T>(true));
        }

        return found;
    }

    private static void AssertRaidAvatarComposition(string prefabPath)
    {
        GameObject avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        Assert.That(avatarPrefab, Is.Not.Null, prefabPath);
        NetworkObject networkObject = avatarPrefab.GetComponent<NetworkObject>();
        RaidAvatarParticipantLink participantLink = avatarPrefab.GetComponent<RaidAvatarParticipantLink>();
        LocalPlayerVisibilityBinder visibilityBinder = avatarPrefab.GetComponent<LocalPlayerVisibilityBinder>();
        Assert.That(networkObject, Is.Not.Null, prefabPath);
        Assert.That(avatarPrefab.GetComponent<PlayerCharacter>(), Is.Not.Null, prefabPath);
        Assert.That(participantLink, Is.Not.Null, prefabPath);
        Assert.That(visibilityBinder, Is.Not.Null, prefabPath);
        Assert.That(avatarPrefab.GetComponent<PlayerLoadoutInjector>(), Is.Null, prefabPath);
        Assert.That(avatarPrefab.GetComponent<PlayerExpeditionExperienceLedger>(), Is.Null, prefabPath);
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(participantLink), prefabPath);
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(visibilityBinder), prefabPath);
        Transform visibilityMesh = FindChild(avatarPrefab.transform, "VisibilityMesh");
        Assert.That(visibilityMesh, Is.Not.Null, prefabPath);
        Assert.That(visibilityMesh.gameObject.activeSelf, Is.False, prefabPath);
    }

    private static Transform FindChild(Transform root, string childName)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            if (transforms[index] != root && transforms[index].name == childName)
            {
                return transforms[index];
            }
        }

        return null;
    }
}
