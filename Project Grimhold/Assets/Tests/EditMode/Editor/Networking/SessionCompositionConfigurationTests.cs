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
using Assert = NUnit.Framework.Assert;

public sealed class SessionCompositionConfigurationTests
{
    private const string SocialPlayerPath = "Assets/Prefabs/SocialPlayer.prefab";
    private const string SystemsPath = "Assets/Prefabs/Systems.prefab";
    private const string TownRaidNpcPath = "Assets/Prefabs/TownRaidNpc.prefab";
    private const string RaidParticipantPath = "Assets/Prefabs/NetworkRaidParticipant.prefab";
    private const string BaseRaidAvatarPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string MeleeRaidAvatarPath = "Assets/Prefabs/NetworkPlayerMelee.prefab";
    private const string RangedRaidAvatarPath = "Assets/Prefabs/NetworkPlayerRanged.prefab";
    private const string MainMenuCanvasPath = "Assets/Prefabs/MainMenu Canvas.prefab";
    private const string TownScenePath = "Assets/Scenes/Lobby-Town.unity";

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
    }

    [Test]
    public void RaidParticipantAndAvatars_HaveSeparatedNetworkComposition()
    {
        GameObject participantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RaidParticipantPath);

        Assert.That(participantPrefab, Is.Not.Null);
        NetworkObject participantObject = participantPrefab.GetComponent<NetworkObject>();
        NetworkRaidParticipant participant = participantPrefab.GetComponent<NetworkRaidParticipant>();
        Assert.That(participantObject, Is.Not.Null);
        Assert.That(participant, Is.Not.Null);
        Assert.That(participantPrefab.GetComponent<PlayerCharacter>(), Is.Null);
        Assert.That(participantObject.NetworkedBehaviours, Does.Contain(participant));

        AssertRaidAvatarComposition(BaseRaidAvatarPath);
        AssertRaidAvatarComposition(MeleeRaidAvatarPath);
        AssertRaidAvatarComposition(RangedRaidAvatarPath);
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
        Assert.That(prefab.GetComponent<TownRaidQueuePresenter>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<LocalPlayerCameraBinder>(), Is.Not.Null);
        Assert.That(prefab.GetComponentInChildren<PlayerAnimatorView>(true), Is.Not.Null);

        var forbiddenTypes = new HashSet<System.Type>
        {
            typeof(PlayerCharacter),
            typeof(PlayerCombatNetworkController),
            typeof(DamageResolver),
            typeof(PlayerLootReceiver),
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
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(prefab.GetComponent<TownRaidQueuePresenter>()));
    }

    [Test]
    public void TownRaidNpc_HasAuthoritativeQueueAndInteractionComposition()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TownRaidNpcPath);

        Assert.That(prefab, Is.Not.Null);
        NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
        TownRaidQueueNetworkController queue = prefab.GetComponent<TownRaidQueueNetworkController>();
        TownRaidNpcInteractable interactable = prefab.GetComponent<TownRaidNpcInteractable>();
        Assert.That(networkObject, Is.Not.Null);
        Assert.That(queue, Is.Not.Null);
        Assert.That(interactable, Is.Not.Null);
        Assert.That(prefab.GetComponent<InteractionPromptMetadata>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Collider2D>(), Is.Not.Null);
        Assert.That(networkObject.Flags.HasFlag(NetworkObjectFlags.MasterClientObject), Is.True);
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(queue));
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(interactable));
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
            Assert.That(FindInScene<HubSpawnSceneConfiguration>(scene), Has.Count.EqualTo(1));
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
        Assert.That(networkObject, Is.Not.Null, prefabPath);
        Assert.That(avatarPrefab.GetComponent<PlayerCharacter>(), Is.Not.Null, prefabPath);
        Assert.That(participantLink, Is.Not.Null, prefabPath);
        Assert.That(networkObject.NetworkedBehaviours, Does.Contain(participantLink), prefabPath);
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
