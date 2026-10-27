#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode.Utilities
{
    public sealed class UtilityExpeditionPlayModeTests
    {
        private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
        private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
        private const string LootPickupGuid = "5d26f13a358d7894b9419465e4ba1869";
        private const string SanctuaryPrefabPath = "Assets/Prefabs/ExtractionSanctuary.prefab";

        private static readonly LootId CompassId = PlayerConsumableNetworkController.RuneCompassId;
        private static readonly LootId DetectorId = PlayerConsumableNetworkController.ArcaneDetectorId;
        private static readonly LootId EtherDustId = new("RES_ETHER_DUST");
        private static readonly LootId BoneId = new("RES_BONE");

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private CharacterAttributeState _attributes;
        private bool _previousIgnoreFailingMessages;

        [UnitySetUp]
        public void SetUp()
        {
            _previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            LogAssert.ignoreFailingMessages = _previousIgnoreFailingMessages;
            if (_runner != null && _runner.IsRunning)
            {
                var shutdown = _runner.Shutdown();
                while (!shutdown.IsCompleted) yield return null;
            }

            if (_runner != null)
            {
                Object.DestroyImmediate(_runner.gameObject);
                _runner = null;
            }
        }

        [UnityTest]
        public IEnumerator RuneCompass_InstantActivation_OrientatesTowardsSanctuary_AndSetsCooldown()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();

            // Spawn Sanctuary at (0, 10) (North of player)
            SpawnSanctuary(new Vector3(0f, 10f, 0f));

            yield return InSimulation(() => AddLoot(loot, CompassId, 1));
            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(1));

            CardinalDirection notifiedDirection = CardinalDirection.None;
            float notifiedDuration = 0f;
            consume.RuneCompassActivated += (dir, dur) =>
            {
                notifiedDirection = dir;
                notifiedDuration = dur;
            };

            Assert.That(consume.TryRequestConsume(CompassId), Is.True);
            yield return WaitTicks(3);

            // Item must NOT be consumed (reusable utility)
            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(1), "Utility must not be consumed from inventory.");
            Assert.That(notifiedDirection, Is.EqualTo(CardinalDirection.North), "Compass must orient towards nearest sanctuary (North).");
            Assert.That(notifiedDuration, Is.EqualTo(5.0f).Within(0.01f), "Display duration must be 5.0s.");

            // Cooldown must be active (30s)
            TickTimer cooldown = consume.GetUtilityCooldown(CompassId);
            Assert.That(cooldown.IsRunning, Is.True, "Compass cooldown must be running.");
            float remaining = cooldown.RemainingTime(_runner) ?? 0f;
            Assert.That(remaining, Is.GreaterThan(28f).And.LessThanOrEqualTo(30f));

            // Immediate second use must fail with CooldownActive
            ConsumableFailureReason rejectionReason = ConsumableFailureReason.None;
            consume.ConsumeRejected += r => rejectionReason = r;
            Assert.That(consume.TryRequestConsume(CompassId), Is.False);
            Assert.That(rejectionReason, Is.EqualTo(ConsumableFailureReason.CooldownActive));
        }

        [UnityTest]
        public IEnumerator ArcaneDetector_ChannelsTwoSeconds_AndInterruptionByDamage_CancelsWithoutCooldown()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();

            yield return InSimulation(() => AddLoot(loot, DetectorId, 1));
            Assert.That(loot.GetLootAmount(DetectorId), Is.EqualTo(1));

            Assert.That(consume.TryRequestConsume(DetectorId), Is.True);
            yield return WaitTicks(3);

            // Channeling must be active
            Assert.That(consume.IsConsuming, Is.True, "Arcane Detector must channel for 2.0s.");
            Assert.That(consume.VoluntaryMovementSpeedMultiplier, Is.EqualTo(0.5f), "Movement speed must be reduced to 0.5x.");

            // Taking damage cancels consumption without activating cooldown
            yield return InSimulation(() => consume.CancelConsumption());
            yield return WaitTicks(2);

            Assert.That(consume.IsConsuming, Is.False, "Channel must be cancelled.");
            TickTimer cooldown = consume.GetUtilityCooldown(DetectorId);
            Assert.That(cooldown.ExpiredOrNotRunning(_runner), Is.True, "Interruption must NOT activate cooldown.");
            Assert.That(loot.GetLootAmount(DetectorId), Is.EqualTo(1), "Detector must remain in inventory.");
        }

        [UnityTest]
        public IEnumerator ArcaneDetector_WhenChannelCompletes_ActivatesCooldown_AndHighlightsArcanePickups()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();

            // Spawn an arcane pickup at (3, 0) (within 10m)
            var arcanePickup = SpawnPickup(EtherDustId, 1, new Vector3(3f, 0f, 0f));
            // Spawn a non-arcane pickup at (4, 0)
            var normalPickup = SpawnPickup(BoneId, 1, new Vector3(4f, 0f, 0f));

            yield return InSimulation(() => AddLoot(loot, DetectorId, 1));
            Assert.That(loot.GetLootAmount(DetectorId), Is.EqualTo(1));

            int detectedCount = -1;
            consume.ArcaneDetectorActivated += (count, dur) => detectedCount = count;

            Assert.That(consume.TryRequestConsume(DetectorId), Is.True);

            // Wait for 2.0s channel to complete
            yield return WaitSimulationSeconds(2.2f);
            yield return WaitTicks(3);

            Assert.That(loot.GetLootAmount(DetectorId), Is.EqualTo(1), "Detector must remain in inventory.");
            Assert.That(consume.GetUtilityCooldown(DetectorId).IsRunning, Is.True, "Detector 20s cooldown must be active.");
            Assert.That(detectedCount, Is.GreaterThanOrEqualTo(1), "Arcane pickup within 10m must be detected.");
            Assert.That(arcanePickup.GetComponent<ArcaneHighlightEffect>(), Is.Not.Null, "Arcane pickup must receive ArcaneHighlightEffect.");
            Assert.That(normalPickup.GetComponent<ArcaneHighlightEffect>(), Is.Null, "Normal pickup must NOT receive ArcaneHighlightEffect.");
        }

        [UnityTest]
        public IEnumerator MaxTransportable_RejectsReceivingSecondUtility()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var loot = player.GetComponent<PlayerLootReceiver>();

            yield return InSimulation(() => AddLoot(loot, CompassId, 1));
            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(1));

            // Attempt to receive a 2nd compass: must be rejected with MaxTransportableExceeded
            var request = new LootTransferRequest(new EntityId(999), loot.Id, CompassId, 1, _runner.Tick);
            LootTransferFailureReason reason = loot.ValidateReceive(request);
            Assert.That(reason, Is.EqualTo(LootTransferFailureReason.MaxTransportableExceeded),
                "Carrying 1 compass must reject receiving a 2nd compass via MaxTransportable.");

            // Detector also has MaxTransportable = 1
            yield return InSimulation(() => AddLoot(loot, DetectorId, 1));
            Assert.That(loot.GetLootAmount(DetectorId), Is.EqualTo(1));

            var requestDetector = new LootTransferRequest(new EntityId(999), loot.Id, DetectorId, 1, _runner.Tick);
            Assert.That(loot.ValidateReceive(requestDetector), Is.EqualTo(LootTransferFailureReason.MaxTransportableExceeded));
        }

        [UnityTest]
        public IEnumerator UtilityCooldown_PersistsAcrossDropAndPickup()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            var loot = player.GetComponent<PlayerLootReceiver>();
            var dropController = player.GetComponent<PlayerLootDropNetworkController>();

            yield return InSimulation(() => AddLoot(loot, CompassId, 1));
            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(1));

            // Use compass -> starts 30s cooldown
            Assert.That(consume.TryRequestConsume(CompassId), Is.True);
            yield return WaitTicks(3);
            Assert.That(consume.GetUtilityCooldown(CompassId).IsRunning, Is.True);

            // Wait 2 seconds in simulation (~28s remaining)
            yield return WaitSimulationSeconds(2f);
            float remainingBeforeDrop = consume.GetUtilityCooldown(CompassId).RemainingTime(_runner) ?? 0f;
            Assert.That(remainingBeforeDrop, Is.GreaterThan(20f).And.LessThanOrEqualTo(28.5f));

            // Drop compass
            Assert.That(dropController.TryRequestDrop(CompassId, LootTransferQuantityMode.FullStack), Is.True);
            yield return WaitTicks(5);

            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(0), "Compass was dropped.");
            Assert.That(consume.GetUtilityCooldown(CompassId).ExpiredOrNotRunning(_runner), Is.True,
                "Cooldown was cleared from player upon drop.");

            // Find the dropped pickup
            NetworkLootPickup droppedPickup = null;
            foreach (var no in _runner.GetAllNetworkObjects())
            {
                if (no.TryGetComponent(out NetworkLootPickup p) && p.LootDefinition != null && p.LootDefinition.LootId == CompassId)
                {
                    droppedPickup = p;
                    break;
                }
            }
            Assert.That(droppedPickup, Is.Not.Null, "Dropped pickup must exist in world.");
            Assert.That(droppedPickup.CooldownExpiration.IsRunning, Is.True, "Pickup must store active CooldownExpiration.");

            // Wait another 2 seconds
            yield return WaitSimulationSeconds(2f);

            // Pick it back up
            var interactReq = new InteractionRequest(player.Id, droppedPickup.Id, _runner.Tick);
            InteractionResult result = default;
            yield return InSimulation(() => result = droppedPickup.Interact(interactReq));
            Assert.That(result.Success, Is.True, "Player must pick up the compass.");

            // Player must now have compass back WITH remaining cooldown active
            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(1));
            TickTimer restoredCooldown = consume.GetUtilityCooldown(CompassId);
            Assert.That(restoredCooldown.IsRunning, Is.True, "Cooldown must be restored upon pickup.");
            float remainingAfterPickup = restoredCooldown.RemainingTime(_runner) ?? 0f;
            Assert.That(remainingAfterPickup, Is.GreaterThan(15f).And.LessThanOrEqualTo(remainingBeforeDrop));
        }

        private void AddLoot(PlayerLootReceiver loot, LootId lootId, int amount)
        {
            var request = new LootTransferRequest(new EntityId(999), loot.Id, lootId, amount, _runner.Tick);
            var origin = RaidLootOriginTransfer.Dungeon(amount);
            Assert.That(loot.ValidateRaidLootOriginReceive(request, origin), Is.EqualTo(LootTransferFailureReason.None));
            loot.CommitRaidLootReceive(request, origin);
        }

        private NetworkLootPickup SpawnPickup(LootId lootId, int amount, Vector3 position)
        {
            NetworkObject pickupObj = _runner.Spawn(
                Load(LootPickupGuid),
                position,
                Quaternion.identity,
                onBeforeSpawned: (runner, instance) =>
                {
                    var pickup = instance.GetComponent<NetworkLootPickup>();
                    var entry = new LootEntry(lootId, amount);
                    pickup.TrySetSpawnContentOverride(runner, instance, entry, true, amount, RaidLootOriginTransfer.Dungeon(amount));
                });
            return pickupObj.GetComponent<NetworkLootPickup>();
        }

        private ExtractionSanctuary SpawnSanctuary(Vector3 position)
        {
            GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(SanctuaryPrefabPath);
            Assert.That(prefab, Is.Not.Null, "Sanctuary prefab must exist.");
            NetworkObject obj = _runner.Spawn(prefab, position, Quaternion.identity);
            return obj.GetComponent<ExtractionSanctuary>();
        }

        private IEnumerator Begin()
        {
            var owner = new GameObject("UtilityExpeditionTestRunner");
            _runner = owner.AddComponent<NetworkRunner>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            owner.AddComponent<EntityRegistry>();
            _driver = owner.AddComponent<PlayerAbilityRuntimeSimulationDriver>();

            Assert.That(RaidCode.TryParse("123456", out var code), Is.True);
            var host = new ProfileId("utility-player-1");
            Assert.That(RaidTeamId.TryCreate(1, out var firstTeam), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out var secondTeam), Is.True);
            var roster = new[]
            {
                new RaidLaunchParticipant(host, firstTeam),
                new RaidLaunchParticipant(new ProfileId("utility-player-2"), secondTeam)
            };
            Assert.That(RaidLaunchContext.TryCreate(code, host, roster, host, 1, out var launch), Is.True);
            Assert.That(owner.AddComponent<NetworkSpawnManager>().InitializeForRunner(_runner, default, default,
                Array.Empty<NetworkPrefabRef>(), SessionStartupContext.FreshSession, launch), Is.True);

            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"utility-test-{Guid.NewGuid():N}",
                SceneManager = owner.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = owner.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted) yield return null;
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            Assert.That(CharacterAttributeState.TryCreate(20, 20, 20, 20, 20, 20, 0, out _attributes), Is.True);
        }

        private PlayerCharacter SpawnAvatar(int sequence, Vector3 position)
        {
            Assert.That(RaidParticipantId.TryCreate(sequence, out var id), Is.True);
            var participant = _runner.Spawn(Load(ParticipantGuid), onBeforeSpawned: (_, instance) =>
                instance.GetComponent<NetworkRaidParticipant>().Initialize($"utility-player-{sequence}", id,
                    _attributes, ExperienceCurve.InitialLevel, 0, "utility-test"))
                .GetComponent<NetworkRaidParticipant>();
            var avatar = _runner.Spawn(Load(PlayerGuid), position, Quaternion.identity, _runner.LocalPlayer,
                onBeforeSpawned: (_, instance) =>
                    instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object));
            Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
            ExtractionProgressTestAssertions.AssertRegistered(_runner, avatar);
            return avatar.GetComponent<PlayerCharacter>();
        }

        private NetworkObject Load(string guid) =>
            _runner.Config.PrefabTable.Load(_runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid)), true);

        private IEnumerator InSimulation(Action action)
        {
            int before = _driver.CompletionSequence;
            _driver.RequestOperation(action);
            yield return WaitFor(() => _driver.CompletionSequence != before);
        }

        private IEnumerator WaitTicks(int count)
        {
            int until = _runner.Tick.Raw + count;
            yield return WaitFor(() => _runner.Tick.Raw >= until);
        }

        private IEnumerator WaitSimulationSeconds(float seconds)
        {
            int until = _runner.Tick.Raw + Mathf.CeilToInt(seconds / _runner.DeltaTime);
            yield return WaitFor(() => _runner.Tick.Raw >= until);
        }

        private static IEnumerator WaitFor(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(predicate(), Is.True, "Condition not satisfied within deadline.");
        }
    }
}
#endif
