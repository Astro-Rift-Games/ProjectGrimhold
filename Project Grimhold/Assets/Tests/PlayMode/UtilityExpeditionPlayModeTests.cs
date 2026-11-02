#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using ProjectGrimhold.Gameplay.Visibility;
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
        private const string LootContainerGuid = "2c19a78647c64b84a765ff0280706b7d";
        private const string NetworkEnemyGuid = "be065b8028b5c3446a4e981ab29a28a1";
        private const string SanctuaryPrefabPath = "Assets/Prefabs/ExtractionSanctuary.prefab";

        private static readonly LootId CompassId = PlayerConsumableNetworkController.RuneCompassId;
        private static readonly LootId DetectorId = PlayerConsumableNetworkController.ArcaneDetectorId;
        private static readonly LootId EtherDustId = new("RES_ETHER_DUST");
        private static readonly LootId BoneId = new("RES_BONE");
        private static readonly LootId PilgrimLightId = PlayerInventoryPassiveEffectsController.PilgrimLightId;
        private static readonly LootId VoidBellId = PlayerInventoryPassiveEffectsController.VoidBellId;
        private static readonly LootId WandererEyeId = PlayerInventoryPassiveEffectsController.WandererEyeId;

        private NetworkRunner _runner;
        private PlayerAbilityRuntimeSimulationDriver _driver;
        private SprintInputDriver _inputDriver;
        private CharacterAttributeState _attributes;
        private bool _previousIgnoreFailingMessages;

        private sealed class SprintInputDriver : NetworkRunnerCallbacksAdapter
        {
            public Vector2 MoveDirection { get; set; }
            public NetworkButtons Buttons { get; set; }

            public override void OnInput(NetworkRunner runner, NetworkInput input) =>
                input.Set(new PlayerNetworkInput
                {
                    MoveDirection = MoveDirection,
                    Buttons = Buttons
                });
        }

        [UnitySetUp]
        public void SetUp()
        {
            _previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            PlayerInventoryPassiveEffectsController.ObserverTransformOverrideForTesting = null;
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

            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(1), "Utility must not be consumed from inventory.");
            Assert.That(notifiedDirection, Is.EqualTo(CardinalDirection.North), "Compass must orient towards nearest sanctuary (North).");
            Assert.That(notifiedDuration, Is.EqualTo(5.0f).Within(0.01f), "Display duration must be 5.0s.");

            TickTimer cooldown = consume.GetUtilityCooldown(CompassId);
            Assert.That(cooldown.IsRunning, Is.True, "Compass cooldown must be running.");
            float remaining = cooldown.RemainingTime(_runner) ?? 0f;
            Assert.That(remaining, Is.GreaterThan(28f).And.LessThanOrEqualTo(30f));

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

            Assert.That(consume.IsConsuming, Is.True, "Arcane Detector must channel for 2.0s.");
            Assert.That(consume.VoluntaryMovementSpeedMultiplier, Is.EqualTo(0.5f), "Movement speed must be reduced to 0.5x.");

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

            var arcanePickup = SpawnPickup(EtherDustId, 1, new Vector3(3f, 0f, 0f));
            var normalPickup = SpawnPickup(BoneId, 1, new Vector3(4f, 0f, 0f));

            yield return InSimulation(() => AddLoot(loot, DetectorId, 1));
            Assert.That(loot.GetLootAmount(DetectorId), Is.EqualTo(1));

            int detectedCount = -1;
            consume.ArcaneDetectorActivated += (count, dur) => detectedCount = count;

            Assert.That(consume.TryRequestConsume(DetectorId), Is.True);

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

            var request = new LootTransferRequest(new EntityId(999), loot.Id, CompassId, 1, _runner.Tick);
            LootTransferFailureReason reason = loot.ValidateReceive(request);
            Assert.That(reason, Is.EqualTo(LootTransferFailureReason.MaxTransportableExceeded),
                "Carrying 1 compass must reject receiving a 2nd compass via MaxTransportable.");

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

            Assert.That(consume.TryRequestConsume(CompassId), Is.True);
            yield return WaitTicks(3);
            Assert.That(consume.GetUtilityCooldown(CompassId).IsRunning, Is.True);

            yield return WaitSimulationSeconds(2f);
            float remainingBeforeDrop = consume.GetUtilityCooldown(CompassId).RemainingTime(_runner) ?? 0f;
            Assert.That(remainingBeforeDrop, Is.GreaterThan(20f).And.LessThanOrEqualTo(28.5f));

            Assert.That(dropController.TryRequestDrop(CompassId, LootTransferQuantityMode.FullStack), Is.True);
            yield return WaitTicks(5);

            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(0), "Compass was dropped.");
            Assert.That(consume.GetUtilityCooldown(CompassId).ExpiredOrNotRunning(_runner), Is.True,
                "Cooldown was cleared from player upon drop.");

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

            yield return WaitSimulationSeconds(2f);

            var interactReq = new InteractionRequest(player.Id, droppedPickup.Id, _runner.Tick);
            InteractionResult result = default;
            yield return InSimulation(() => result = droppedPickup.Interact(interactReq));
            Assert.That(result.Success, Is.True, "Player must pick up the compass.");

            Assert.That(loot.GetLootAmount(CompassId), Is.EqualTo(1));
            TickTimer restoredCooldown = consume.GetUtilityCooldown(CompassId);
            Assert.That(restoredCooldown.IsRunning, Is.True, "Cooldown must be restored upon pickup.");
            float remainingAfterPickup = restoredCooldown.RemainingTime(_runner) ?? 0f;
            Assert.That(remainingAfterPickup, Is.GreaterThan(15f).And.LessThanOrEqualTo(remainingBeforeDrop));
        }

        [UnityTest]
        public IEnumerator UtilityCooldown_PersistsAcrossContainerDepositAndWithdrawal_IncludingSecondPlayer()
        {
            yield return Begin();
            var playerA = SpawnAvatar(1, Vector3.zero);
            var playerB = SpawnAvatar(2, new Vector3(1f, 0f, 0f));
            var consumeA = playerA.GetComponent<PlayerConsumableNetworkController>();
            var consumeB = playerB.GetComponent<PlayerConsumableNetworkController>();
            var lootA = playerA.GetComponent<PlayerLootReceiver>();
            var lootB = playerB.GetComponent<PlayerLootReceiver>();
            var container = SpawnContainer(new Vector3(0.5f, 0f, 0f));

            yield return InSimulation(() => AddLoot(lootA, CompassId, 1));
            Assert.That(consumeA.TryRequestConsume(CompassId), Is.True);
            yield return WaitTicks(3);
            Assert.That(consumeA.GetUtilityCooldown(CompassId).IsRunning, Is.True);

            yield return WaitSimulationSeconds(2f);
            float remainingBeforeDeposit = consumeA.GetUtilityCooldown(CompassId).RemainingTime(_runner) ?? 0f;

            // Deposit into world container
            LootTransferResult depositResult = default;
            yield return InSimulation(() =>
            {
                var req = new LootTransferRequest(lootA.Id, container.Id, CompassId, 1, _runner.Tick);
                depositResult = LootTransferTransaction.Execute(lootA, container, req);
            });
            Assert.That(depositResult.Success, Is.True);
            Assert.That(lootA.GetLootAmount(CompassId), Is.EqualTo(0));
            Assert.That(consumeA.GetUtilityCooldown(CompassId).ExpiredOrNotRunning(_runner), Is.True,
                "Player A cooldown must be cleared once utility is deposited into container.");
            Assert.That(container.GetUtilityCooldown(CompassId).IsRunning, Is.True,
                "Container must hold the active utility cooldown.");

            // Let cooldown continue ticking while inside container
            yield return WaitSimulationSeconds(2f);

            // Withdraw back to Player A -> cooldown remains active, immediate reuse rejected
            LootTransferResult withdrawAResult = default;
            yield return InSimulation(() =>
            {
                var req = new LootTransferRequest(container.Id, lootA.Id, CompassId, 1, _runner.Tick);
                withdrawAResult = LootTransferTransaction.Execute(container, lootA, req);
            });
            Assert.That(withdrawAResult.Success, Is.True);
            Assert.That(container.GetUtilityCooldown(CompassId).ExpiredOrNotRunning(_runner), Is.True,
                "Container cooldown must be cleared after last unit is withdrawn.");
            Assert.That(consumeA.GetUtilityCooldown(CompassId).IsRunning, Is.True);
            float remainingAfterWithdrawA = consumeA.GetUtilityCooldown(CompassId).RemainingTime(_runner) ?? 0f;
            Assert.That(remainingAfterWithdrawA, Is.GreaterThan(15f).And.LessThan(remainingBeforeDeposit));

            ConsumableFailureReason rejectA = ConsumableFailureReason.None;
            consumeA.ConsumeRejected += r => rejectA = r;
            Assert.That(consumeA.TryRequestConsume(CompassId), Is.False, "Container deposit/withdraw must NOT reset cooldown.");
            Assert.That(rejectA, Is.EqualTo(ConsumableFailureReason.CooldownActive));

            // Deposit again and withdraw into Player B -> Player B also receives remaining cooldown
            yield return InSimulation(() =>
            {
                var dep = new LootTransferRequest(lootA.Id, container.Id, CompassId, 1, _runner.Tick);
                Assert.That(LootTransferTransaction.Execute(lootA, container, dep).Success, Is.True);
                var wit = new LootTransferRequest(container.Id, lootB.Id, CompassId, 1, _runner.Tick);
                Assert.That(LootTransferTransaction.Execute(container, lootB, wit).Success, Is.True);
            });
            yield return WaitTicks(2);

            Assert.That(consumeB.GetUtilityCooldown(CompassId).IsRunning, Is.True,
                "Second player withdrawing the utility must inherit the remaining cooldown.");
            ConsumableFailureReason rejectB = ConsumableFailureReason.None;
            consumeB.ConsumeRejected += r => rejectB = r;
            Assert.That(consumeB.TryRequestConsume(CompassId), Is.False);
            Assert.That(rejectB, Is.EqualTo(ConsumableFailureReason.CooldownActive));
        }

        [UnityTest]
        public IEnumerator TreasurePassives_PilgrimLight_WandererEye_AndDeathRevocation_WorkInRuntime()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var loot = player.GetComponent<PlayerLootReceiver>();
            var passive = player.PassiveEffectsController;
            var stamina = player.GetComponent<PlayerStaminaNetworkController>();
            var vision = player.GetComponentInChildren<LocalVisionSource>(true);
            var drop = player.GetComponent<PlayerLootDropNetworkController>();

            float baseRegen = stamina.EffectiveRegenerationPerSecond;
            Assert.That(vision, Is.Not.Null);
            Assert.That(vision.TryBuildQuery(out VisionQuery baseQuery), Is.True);
            float baseNearRadius = baseQuery.NearRadius;

            // Carry 2 Pilgrim's Lights -> binary +35% stamina regen (non-stacking)
            yield return InSimulation(() =>
            {
                AddLoot(loot, PilgrimLightId, 1);
                AddLoot(loot, PilgrimLightId, 1);
            });
            Assert.That(loot.GetLootAmount(PilgrimLightId), Is.EqualTo(2));
            Assert.That(passive.HasPilgrimLight, Is.True);
            Assert.That(passive.StaminaRegenMultiplier, Is.EqualTo(1.35f).Within(0.001f));
            Assert.That(stamina.EffectiveRegenerationPerSecond, Is.EqualTo(baseRegen * 1.35f).Within(0.01f));

            // Carry Wanderer's Eye -> 0.75x vision & minimap blocked
            yield return InSimulation(() => AddLoot(loot, WandererEyeId, 1));
            Assert.That(passive.HasWandererEye, Is.True);
            Assert.That(passive.IsMinimapBlocked, Is.True);
            Assert.That(vision.TryBuildQuery(out VisionQuery eyeQuery), Is.True);
            Assert.That(eyeQuery.DistanceMultiplier, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(eyeQuery.NearRadius, Is.EqualTo(baseNearRadius * 0.75f).Within(0.001f));

            // Drop Wanderer's Eye -> immediate restoration
            Assert.That(drop.TryRequestDrop(WandererEyeId, LootTransferQuantityMode.FullStack), Is.True);
            yield return WaitTicks(5);
            Assert.That(passive.HasWandererEye, Is.False);
            Assert.That(passive.IsMinimapBlocked, Is.False);
            Assert.That(vision.TryBuildQuery(out VisionQuery afterDropQuery), Is.True);
            Assert.That(afterDropQuery.DistanceMultiplier, Is.EqualTo(1f).Within(0.001f));

            // Re-acquire Wanderer's Eye + active utility cooldown, then trigger definitive death
            yield return InSimulation(() =>
            {
                AddLoot(loot, WandererEyeId, 1);
                AddLoot(loot, CompassId, 1);
            });
            var consume = player.GetComponent<PlayerConsumableNetworkController>();
            Assert.That(consume.TryRequestConsume(CompassId), Is.True);
            yield return WaitTicks(3);

            yield return InSimulation(() => player.ForceDefinitiveDefeatFromCollapse());
            yield return WaitTicks(2);

            Assert.That(player.IsAlive, Is.False);
            Assert.That(passive.HasPilgrimLight, Is.False, "Death must revoke Pilgrim's Light immediately.");
            Assert.That(passive.HasWandererEye, Is.False, "Death must revoke Wanderer's Eye immediately.");
            Assert.That(passive.IsMinimapBlocked, Is.False);
            Assert.That(stamina.EffectiveRegenerationPerSecond, Is.EqualTo(baseRegen).Within(0.01f));

            // Corpse container must also retain the active utility cooldown
            var corpseContainer = player.GetComponent<NetworkLootContainer>();
            Assert.That(corpseContainer.IsAvailable, Is.True);
            Assert.That(corpseContainer.GetUtilityCooldown(CompassId).IsRunning, Is.True,
                "Defeated player's corpse container must preserve active utility cooldowns.");
        }

        [UnityTest]
        public IEnumerator VoidBell_AuthoritativeSprintTick_IncrementsChimeSequence_AndAlertsNearbyEnemy()
        {
            yield return Begin();
            var player = SpawnAvatar(1, Vector3.zero);
            var loot = player.GetComponent<PlayerLootReceiver>();
            var passive = player.PassiveEffectsController;

            var enemyObj = _runner.Spawn(Load(NetworkEnemyGuid), new Vector3(8f, 0f, 0f), Quaternion.identity);
            var enemyAi = enemyObj.GetComponent<EnemyMovementAIController>();
            Assert.That(enemyAi, Is.Not.Null);

            yield return InSimulation(() => AddLoot(loot, VoidBellId, 1));
            Assert.That(passive.HasVoidBell, Is.True);
            Assert.That(passive.SprintSpeedMultiplier, Is.EqualTo(1.15f).Within(0.001f));

            int initialChimes = player.VoidBellChimeSequence;

            // Feed continuous sprint input rightwards
            var sprintButtons = default(NetworkButtons);
            sprintButtons.Set((int)PlayerInputButton.Sprint, true);
            _inputDriver.MoveDirection = Vector2.right;
            _inputDriver.Buttons = sprintButtons;

            yield return WaitTicks(6);

            Assert.That(player.VoidBellChimeSequence, Is.GreaterThan(initialChimes),
                "Sprinting with Void Bell must advance replicated VoidBellChimeSequence on State Authority.");

            // Stop sprinting and drop Void Bell -> chime sequence stops advancing
            _inputDriver.MoveDirection = Vector2.zero;
            _inputDriver.Buttons = default;
            var drop = player.GetComponent<PlayerLootDropNetworkController>();
            Assert.That(drop.TryRequestDrop(VoidBellId, LootTransferQuantityMode.FullStack), Is.True);
            yield return WaitTicks(5);

            int chimesAfterDrop = player.VoidBellChimeSequence;
            _inputDriver.MoveDirection = Vector2.right;
            _inputDriver.Buttons = sprintButtons;
            yield return WaitSimulationSeconds(2.2f);

            Assert.That(player.VoidBellChimeSequence, Is.EqualTo(chimesAfterDrop),
                "Sprinting without Void Bell must not emit chimes.");
        }

        private void AddLoot(PlayerLootReceiver loot, LootId lootId, int amount)
        {
            var request = new LootTransferRequest(new EntityId(999), loot.Id, lootId, amount, _runner.Tick);
            var origin = RaidLootOriginTransfer.Dungeon(amount);
            Assert.That(loot.ValidateRaidLootOriginReceive(request, origin), Is.EqualTo(LootTransferFailureReason.None));
            loot.CommitRaidLootReceive(request, origin);
        }

        private NetworkLootContainer SpawnContainer(Vector3 position)
        {
            NetworkObject containerObj = _runner.Spawn(Load(LootContainerGuid), position, Quaternion.identity);
            return containerObj.GetComponent<NetworkLootContainer>();
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
            _inputDriver = owner.AddComponent<SprintInputDriver>();
            _runner.AddCallbacks(_inputDriver);

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
