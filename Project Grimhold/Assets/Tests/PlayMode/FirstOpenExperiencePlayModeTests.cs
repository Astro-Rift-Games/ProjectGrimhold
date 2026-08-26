#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;
using Object = UnityEngine.Object;

namespace Tests.PlayMode.Progression
{
    public sealed class FirstOpenExperiencePlayModeTests
    {
        private const string PlayerPrefabGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantPrefabGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string ContainerPrefabGuid = "2c19a78647c64b84a765ff0280706b7d";

        private NetworkRunner _runner;
        private ExtractionProgressSimulationDriver _driver;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null && _runner.IsRunning)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted)
                {
                    yield return null;
                }
            }

            if (_runner != null)
            {
                Object.DestroyImmediate(_runner.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator FirstOwnerReceivesConfiguredExplorationOnceIncludingEmptyChest()
        {
            yield return StartRunner();
            (NetworkRaidParticipant firstParticipant, NetworkObject firstAvatar) =
                SpawnParticipantAndAvatar("profile-a", Vector3.left);
            (NetworkRaidParticipant secondParticipant, NetworkObject secondAvatar) =
                SpawnParticipantAndAvatar("profile-b", Vector3.right);
            NetworkObject chest = SpawnContainer(
                new[] { new LootEntry(new LootId("bone"), 1) },
                Vector3.zero);
            NetworkLootContainerInteractable interactable =
                chest.GetComponent<NetworkLootContainerInteractable>();

            yield return Interact(interactable, firstAvatar);

            AssertBreakdown(firstParticipant, exploration: 5, total: 5);
            AssertBreakdown(secondParticipant, exploration: 0, total: 0);
            Assert.That((bool)interactable.FirstOpenExperienceResolved, Is.True);
            Assert.That(interactable.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));
            Assert.That((bool)interactable.FirstOpenResolved, Is.True);

            yield return Interact(interactable, firstAvatar);
            yield return Interact(interactable, secondAvatar);
            AssertBreakdown(firstParticipant, exploration: 5, total: 5);
            AssertBreakdown(secondParticipant, exploration: 0, total: 0);

            NetworkObject emptyChest = SpawnContainer(Array.Empty<LootEntry>(), Vector3.up * 2f);
            NetworkLootContainerInteractable emptyInteractable =
                emptyChest.GetComponent<NetworkLootContainerInteractable>();
            yield return Interact(emptyInteractable, secondAvatar);

            AssertBreakdown(secondParticipant, exploration: 5, total: 5);
            Assert.That((bool)emptyInteractable.FirstOpenExperienceResolved, Is.True);
            Assert.That((bool)emptyInteractable.FirstOpenResolved, Is.True);
        }

        [UnityTest]
        public IEnumerator InvalidAvatarDoesNotClaimAndValidInteractionCanResolveAfterProgress()
        {
            yield return StartRunner();
            (NetworkRaidParticipant participant, NetworkObject validAvatar) =
                SpawnParticipantAndAvatar("profile-a", Vector3.left);
            NetworkObject invalidAvatar = SpawnAvatarWithoutParticipant(Vector3.right);
            NetworkObject chest = SpawnContainer(
                new[] { new LootEntry(new LootId("bone"), 1) },
                Vector3.zero);
            NetworkLootContainerInteractable interactable =
                chest.GetComponent<NetworkLootContainerInteractable>();

            yield return Interact(interactable, invalidAvatar);

            Assert.That((bool)interactable.FirstOpenResolved, Is.True);
            Assert.That((bool)interactable.FirstOpenExperienceResolved, Is.False);
            Assert.That(interactable.FirstOpenExperienceOwnerProfileId.ToString(), Is.Empty);
            AssertBreakdown(participant, exploration: 0, total: 0);

            yield return Interact(interactable, validAvatar);

            Assert.That((bool)interactable.FirstOpenResolved, Is.True);
            Assert.That((bool)interactable.FirstOpenExperienceResolved, Is.True);
            Assert.That(interactable.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));
            AssertBreakdown(participant, exploration: 5, total: 5);
        }

        [UnityTest]
        public IEnumerator LedgerRejectionKeepsOwnerAndOnlyOwnerCanRetry()
        {
            yield return StartRunner();
            (NetworkRaidParticipant firstParticipant, NetworkObject firstAvatar) =
                SpawnParticipantAndAvatar("profile-a", Vector3.left);
            (NetworkRaidParticipant secondParticipant, NetworkObject secondAvatar) =
                SpawnParticipantAndAvatar("profile-b", Vector3.right);
            PlayerExpeditionExperienceLedger firstLedger = GetLedger(firstParticipant);
            PlayerExpeditionExperienceLedger secondLedger = GetLedger(secondParticipant);
            NetworkObject chest = SpawnContainer(
                new[] { new LootEntry(new LootId("bone"), 1) },
                Vector3.zero);
            NetworkLootContainerInteractable interactable =
                chest.GetComponent<NetworkLootContainerInteractable>();

            yield return Execute(_ => Assert.That(
                firstLedger.TryRegisterNormalReward(
                    ExpeditionExperienceCategory.Kill,
                    long.MaxValue,
                    out ExpeditionExperienceLedgerFailure failure),
                Is.True,
                failure.ToString()));
            yield return Interact(interactable, firstAvatar);

            Assert.That(interactable.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));
            Assert.That((bool)interactable.FirstOpenExperienceResolved, Is.False);
            Assert.That((bool)interactable.FirstOpenResolved, Is.True);

            yield return Interact(interactable, secondAvatar);
            AssertBreakdown(secondParticipant, exploration: 0, total: 0);
            Assert.That(interactable.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));

            yield return Execute(_ => firstLedger.CopyStateFrom(secondLedger));
            yield return Interact(interactable, firstAvatar);

            AssertBreakdown(firstParticipant, exploration: 5, total: 5);
            Assert.That((bool)interactable.FirstOpenExperienceResolved, Is.True);
            yield return Interact(interactable, firstAvatar);
            AssertBreakdown(firstParticipant, exploration: 5, total: 5);
        }

        [UnityTest]
        public IEnumerator MissingLedgerKeepsFirstEligibleOwnerAndBlocksOtherParticipants()
        {
            yield return StartRunner();
            (NetworkRaidParticipant firstParticipant, NetworkObject firstAvatar) =
                SpawnParticipantAndAvatar("profile-a", Vector3.left);
            (NetworkRaidParticipant secondParticipant, NetworkObject secondAvatar) =
                SpawnParticipantAndAvatar("profile-b", Vector3.right);
            NetworkLootContainerInteractable interactable = SpawnContainer(
                    new[] { new LootEntry(new LootId("bone"), 1) },
                    Vector3.zero)
                .GetComponent<NetworkLootContainerInteractable>();

            Object.DestroyImmediate(GetLedger(firstParticipant));
            yield return Interact(interactable, firstAvatar);

            Assert.That(interactable.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));
            Assert.That((bool)interactable.FirstOpenExperienceResolved, Is.False);
            Assert.That((bool)interactable.FirstOpenResolved, Is.True);

            yield return Interact(interactable, secondAvatar);

            Assert.That(interactable.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));
            Assert.That((bool)interactable.FirstOpenExperienceResolved, Is.False);
            AssertBreakdown(secondParticipant, exploration: 0, total: 0);
        }

        [UnityTest]
        public IEnumerator CopyStateFromPreservesOwnerAndIndependentOneShots()
        {
            yield return StartRunner();
            (NetworkRaidParticipant firstParticipant, NetworkObject firstAvatar) =
                SpawnParticipantAndAvatar("profile-a", Vector3.left);
            (NetworkRaidParticipant secondParticipant, NetworkObject secondAvatar) =
                SpawnParticipantAndAvatar("profile-b", Vector3.right);
            NetworkLootContainerInteractable resolved = SpawnContainer(
                    new[] { new LootEntry(new LootId("bone"), 1) },
                    Vector3.zero)
                .GetComponent<NetworkLootContainerInteractable>();
            NetworkLootContainerInteractable restored = SpawnContainer(
                    new[] { new LootEntry(new LootId("bone"), 1) },
                    Vector3.up * 2f)
                .GetComponent<NetworkLootContainerInteractable>();

            yield return Interact(resolved, firstAvatar);
            yield return Execute(_ => restored.CopyStateFrom(resolved));

            Assert.That((bool)restored.FirstOpenResolved, Is.True);
            Assert.That((bool)restored.FirstOpenExperienceResolved, Is.True);
            Assert.That(restored.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));

            yield return Interact(restored, secondAvatar);
            AssertBreakdown(firstParticipant, exploration: 5, total: 5);
            AssertBreakdown(secondParticipant, exploration: 0, total: 0);
        }

        [UnityTest]
        public IEnumerator CopyStateFromPreservesPendingOwnerAndBlocksReplacement()
        {
            yield return StartRunner();
            (NetworkRaidParticipant firstParticipant, NetworkObject firstAvatar) =
                SpawnParticipantAndAvatar("profile-a", Vector3.left);
            (NetworkRaidParticipant secondParticipant, NetworkObject secondAvatar) =
                SpawnParticipantAndAvatar("profile-b", Vector3.right);
            PlayerExpeditionExperienceLedger firstLedger = GetLedger(firstParticipant);
            NetworkLootContainerInteractable pending = SpawnContainer(
                    new[] { new LootEntry(new LootId("bone"), 1) },
                    Vector3.zero)
                .GetComponent<NetworkLootContainerInteractable>();
            NetworkLootContainerInteractable restored = SpawnContainer(
                    new[] { new LootEntry(new LootId("bone"), 1) },
                    Vector3.up * 2f)
                .GetComponent<NetworkLootContainerInteractable>();

            yield return Execute(_ => Assert.That(
                firstLedger.TryRegisterNormalReward(
                    ExpeditionExperienceCategory.Kill,
                    long.MaxValue,
                    out ExpeditionExperienceLedgerFailure failure),
                Is.True,
                failure.ToString()));
            yield return Interact(pending, firstAvatar);
            yield return Execute(_ => restored.CopyStateFrom(pending));

            Assert.That((bool)restored.FirstOpenResolved, Is.True);
            Assert.That((bool)restored.FirstOpenExperienceResolved, Is.False);
            Assert.That(restored.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));

            yield return Interact(restored, secondAvatar);

            Assert.That((bool)restored.FirstOpenExperienceResolved, Is.False);
            Assert.That(restored.FirstOpenExperienceOwnerProfileId.ToString(), Is.EqualTo("profile-a"));
            AssertBreakdown(secondParticipant, exploration: 0, total: 0);
        }

        private IEnumerator StartRunner()
        {
            var runnerObject = new GameObject("FirstOpenExperienceTestRunner");
            _runner = runnerObject.AddComponent<NetworkRunner>();
            runnerObject.AddComponent<EntityRegistry>();
            _driver = runnerObject.AddComponent<ExtractionProgressSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task-132-{Guid.NewGuid():N}",
                SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = runnerObject.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
            {
                yield return null;
            }

            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        }

        private (NetworkRaidParticipant participant, NetworkObject avatar) SpawnParticipantAndAvatar(
            string profileId,
            Vector3 position)
        {
            NetworkObject participantObject = _runner.Spawn(
                LoadPrefab(ParticipantPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<NetworkRaidParticipant>().Initialize(
                        profileId,
                        CreateParticipantId(position.x < 0f ? 1 : 2),
                        ExperienceCurve.InitialLevel,
                        0,
                        "task-132-generation"));
            NetworkRaidParticipant participant =
                participantObject.GetComponent<NetworkRaidParticipant>();

            ExpectBasePrefabExtractionProgressValidationError();
            NetworkObject avatar = _runner.Spawn(
                LoadPrefab(PlayerPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participantObject));
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            return (participant, avatar);
        }

        private static RaidParticipantId CreateParticipantId(int value)
        {
            RaidParticipantId.TryCreate(value, out RaidParticipantId participantId);
            return participantId;
        }

        private NetworkObject SpawnAvatarWithoutParticipant(Vector3 position)
        {
            ExpectBasePrefabExtractionProgressValidationError();
            return _runner.Spawn(
                LoadPrefab(PlayerPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null);
        }

        private NetworkObject SpawnContainer(IReadOnlyList<LootEntry> content, Vector3 position)
        {
            bool initialized = false;
            NetworkObject spawned = _runner.Spawn(
                LoadPrefab(ContainerPrefabGuid),
                position,
                Quaternion.identity,
                inputAuthority: null,
                onBeforeSpawned: (runner, instance) => initialized =
                    instance.GetComponent<NetworkLootContainer>()
                        .TrySetInitialContentOverride(runner, instance, content));
            Assert.That(initialized, Is.True);
            return spawned;
        }

        private IEnumerator Interact(
            NetworkLootContainerInteractable interactable,
            NetworkObject avatar)
        {
            yield return Execute(runner =>
            {
                InteractionResult result = interactable.Interact(new InteractionRequest(
                    avatar.GetComponent<PlayerCharacter>().Id,
                    interactable.Id,
                    runner.Tick));
                Assert.That(result.Success, Is.True);
            });
        }

        private IEnumerator Execute(Action<NetworkRunner> action)
        {
            _driver.ClearException();
            _driver.PendingAction = action;
            int frames = 120;
            while (_driver.PendingAction != null && frames-- > 0)
            {
                yield return null;
            }

            Assert.That(_driver.PendingAction, Is.Null);
            Assert.That(_driver.LastException, Is.Null, _driver.LastException?.ToString());
        }

        private NetworkObject LoadPrefab(string guid)
        {
            NetworkPrefabId prefabId =
                _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid));
            Assert.That(prefabId.IsValid, Is.True, guid);
            NetworkObject prefab = _runner.Config.PrefabTable.Load(prefabId, true);
            Assert.That(prefab, Is.Not.Null, guid);
            return prefab;
        }

        private static PlayerExpeditionExperienceLedger GetLedger(
            NetworkRaidParticipant participant) =>
            participant.GetComponent<PlayerExpeditionExperienceLedger>();

        private static void AssertBreakdown(
            NetworkRaidParticipant participant,
            long exploration,
            long total)
        {
            ExpeditionExperienceSnapshot snapshot = GetLedger(participant).Snapshot;
            Assert.That(snapshot.KillExperience, Is.Zero);
            Assert.That(snapshot.AssistExperience, Is.Zero);
            Assert.That(snapshot.ExplorationExperience, Is.EqualTo(exploration));
            Assert.That(snapshot.ExtractedLootExperience, Is.Zero);
            Assert.That(snapshot.TotalExperience, Is.EqualTo(total));
        }

        private static void ExpectBasePrefabExtractionProgressValidationError()
        {
            LogAssert.Expect(
                UnityEngine.LogType.Error,
                "PlayerExtractionProgressController requires character, extraction controller, registry, assignment service, and valid receiver/reader registrations.");
        }
    }
}
#endif
