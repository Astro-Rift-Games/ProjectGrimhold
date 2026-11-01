#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using LogType = UnityEngine.LogType;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Loot
{
    public sealed class LockedLootContainerPlayModeTests
    {
        private const string PrefabGuid = "2c19a78647c64b84a765ff0280706b7d";
        private NetworkRunner _runner;
        private NetworkObject _prefab;
        private ExtractionProgressSimulationDriver _driver;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var holder = new GameObject(nameof(LockedLootContainerPlayModeTests));
            _runner = holder.AddComponent<NetworkRunner>();
            holder.AddComponent<EntityRegistry>();
            PlayModeRunnerComposition.AddLocalInputContext(_runner);
            _driver = holder.AddComponent<ExtractionProgressSimulationDriver>();
            var start = _runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Single,
                SessionName = $"task235-container-{Guid.NewGuid():N}",
                SceneManager = holder.AddComponent<NetworkSceneManagerDefault>(),
                ObjectProvider = holder.AddComponent<NetworkObjectProviderDefault>()
            });
            while (!start.IsCompleted)
                yield return null;
            Assert.That(start.IsFaulted, Is.False, start.Exception?.ToString());
            Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
            NetworkPrefabId id = _runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(PrefabGuid));
            Assert.That(id.IsValid, Is.True);
            _prefab = _runner.Config.PrefabTable.Load(id, true);
            Assert.That(_prefab, Is.Not.Null);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null && _runner.IsRunning)
            {
                _runner.Shutdown();
                while (_runner != null && _runner.IsRunning)
                    yield return null;
            }
            if (_runner != null)
                UnityEngine.Object.DestroyImmediate(_runner.gameObject);
        }

        [UnityTest]
        public IEnumerator LockedContainer_RejectsBothTransfersWithoutChangingContentOrProvenance()
        {
            NetworkLootContainer container = Spawn(locked: true);
            Assert.That(IsLocked(container), Is.True);
            Assert.That(container.IsInitialized && container.IsAvailable, Is.True);
            Assert.That(container.TryGetLootContent(out IReadOnlyList<LootEntry> before), Is.True);
            Assert.That(before.Count, Is.GreaterThan(0));
            var origins = container.GetComponent<ContainerRaidLootOriginState>();
            Assert.That(origins.TryGetEntries(container.LootCatalog, out IReadOnlyList<RaidLootOriginEntry> beforeOrigins), Is.True);
            int sequence = container.LootChangeSequence;
            var take = new LootTransferRequest(container.Id, new EntityId(int.MaxValue), before[0].LootId, 1, _runner.Tick);
            var deposit = new LootTransferRequest(new EntityId(int.MaxValue), container.Id, before[0].LootId, 1, _runner.Tick);
            Assert.That(container.ValidateExtraction(take), Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
            Assert.That(container.ValidateReceive(deposit), Is.EqualTo(LootTransferFailureReason.ContainerUnavailable));
            Assert.That(container.TryGetLootContent(out IReadOnlyList<LootEntry> after), Is.True);
            Assert.That(after, Is.EqualTo(before));
            Assert.That(origins.TryGetEntries(container.LootCatalog, out IReadOnlyList<RaidLootOriginEntry> afterOrigins), Is.True);
            Assert.That(afterOrigins, Is.EqualTo(beforeOrigins));
            Assert.That(container.LootChangeSequence, Is.EqualTo(sequence));

            Assert.That(TryUnlock(container), Is.False, "Frame code cannot unlock a container.");
            yield return Execute(() =>
            {
                Assert.That(container.TryResolveRaidLootOriginTransfer(take, out RaidLootOriginTransfer transfer), Is.True);
                LogAssert.Expect(LogType.Error,
                    "NetworkLootContainer: CommitExtraction contract was violated for 'LootContainer(Clone)'.");
                Assert.Throws<InvalidOperationException>(() => container.CommitRaidLootExtraction(take, transfer));
                LogAssert.Expect(LogType.Error,
                    "NetworkLootContainer: CommitReceive contract was violated for 'LootContainer(Clone)'.");
                Assert.Throws<InvalidOperationException>(() => container.CommitRaidLootReceive(deposit, transfer));
                Assert.That(TryUnlock(container), Is.True);
                Assert.That(TryUnlock(container), Is.False, "Unlock is a one-time transition.");
                Assert.That(container.ValidateExtraction(take), Is.EqualTo(LootTransferFailureReason.None));
                Assert.That(container.ValidateReceive(deposit), Is.EqualTo(LootTransferFailureReason.None));
            });
            Assert.That(IsLocked(container), Is.False);
            Assert.That(container.LootChangeSequence, Is.EqualTo(sequence));
            Assert.That(container.TryGetLootContent(out after), Is.True);
            Assert.That(after, Is.EqualTo(before));
            Assert.That(origins.TryGetEntries(container.LootCatalog, out afterOrigins), Is.True);
            Assert.That(afterOrigins, Is.EqualTo(beforeOrigins));
        }

        [UnityTest]
        public IEnumerator LockedPendingGeneration_RemainsPendingUntilUnlockThenResolvesOnce()
        {
            NetworkLootContainer container = Spawn(locked: true, random: true);
            Assert.That(container.GenerationState, Is.EqualTo(LootSourceGenerationState.Pending));
            yield return Execute(() =>
            {
                Assert.That(container.TryResolveRandomContent(out _), Is.False);
                Assert.That(container.GenerationState, Is.EqualTo(LootSourceGenerationState.Pending));
                Assert.That(container.IsEmpty, Is.True);
                Assert.That(container.LootChangeSequence, Is.Zero);
                Assert.That(TryUnlock(container), Is.True);
                Assert.That(container.TryResolveRandomContent(out string error), Is.True, error);
                Assert.That(container.GenerationState, Is.EqualTo(LootSourceGenerationState.Resolved));
                Assert.That(container.TryResolveRandomContent(out _), Is.False);
            });
        }

        [UnityTest]
        public IEnumerator Unlock_RejectsUnavailableUninitializedAndUnregisteredState()
        {
            NetworkLootContainer container = Spawn(locked: true);
            yield return Execute(() =>
            {
                container.SetAvailability(false);
                Assert.That(TryUnlock(container), Is.False);
                container.SetAvailability(true);
                SetState(container, "IsInitialized", false);
                Assert.That(TryUnlock(container), Is.False);
                SetState(container, "IsInitialized", true);
                SetField(container, "_isRegistered", false);
                Assert.That(TryUnlock(container), Is.False);
                SetField(container, "_isRegistered", true);
                Assert.That(IsLocked(container), Is.True);
                Assert.That(TryUnlock(container), Is.True);
            });
        }

        [UnityTest]
        public IEnumerator DefaultUnlockedContainer_PreservesExistingTransferBehaviourAndCopiedLockState()
        {
            NetworkLootContainer unlocked = Spawn(locked: false);
            NetworkLootContainer locked = Spawn(locked: true);
            Assert.That(IsLocked(unlocked), Is.False);
            Assert.That(unlocked.TryGetLootContent(out IReadOnlyList<LootEntry> content), Is.True);
            var take = new LootTransferRequest(unlocked.Id, new EntityId(int.MaxValue), content[0].LootId, 1, _runner.Tick);
            var deposit = new LootTransferRequest(new EntityId(int.MaxValue), unlocked.Id, content[0].LootId, 1, _runner.Tick);
            Assert.That(unlocked.ValidateExtraction(take), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(unlocked.ValidateReceive(deposit), Is.EqualTo(LootTransferFailureReason.None));
            yield return Execute(() =>
            {
                Assert.That(TryUnlock(unlocked), Is.False);
                locked.CopyStateFrom(unlocked);
                Assert.That(IsLocked(locked), Is.False, "Snapshot copy must preserve an unlocked lock.");
                SetState(unlocked, "IsLocked", true);
                locked.CopyStateFrom(unlocked);
                Assert.That(IsLocked(locked), Is.True, "Snapshot copy must preserve a locked lock.");
            });
        }

        private NetworkLootContainer Spawn(bool locked, bool random = false)
        {
            NetworkObject spawned = _runner.Spawn(_prefab, Vector3.zero, Quaternion.identity,
                onBeforeSpawned: (runner, instance) =>
                {
                    NetworkLootContainer container = instance.GetComponent<NetworkLootContainer>();
                    SetField(container, "_startsLocked", locked);
                    if (random)
                        Assert.That(container.TrySetRandomGenerationOverride(runner, instance, 144, 0), Is.True);
                    else
                        Assert.That(container.TrySetInitialContentOverride(runner, instance,
                            new[] { new LootEntry(new LootId("bone"), 2) }), Is.True);
                });
            Assert.That(spawned, Is.Not.Null);
            return spawned.GetComponent<NetworkLootContainer>();
        }

        private IEnumerator Execute(Action action)
        {
            _driver.ClearException();
            _driver.PendingAction = _ => action();
            int frames = 120;
            while (_driver.PendingAction != null && frames-- > 0)
                yield return null;
            Assert.That(_driver.PendingAction, Is.Null, "No simulation tick executed the action.");
            Assert.That(_driver.LastException, Is.Null, _driver.LastException?.ToString());
        }

        private static bool IsLocked(NetworkLootContainer container)
        {
            PropertyInfo property = typeof(NetworkLootContainer).GetProperty("IsLocked");
            Assert.That(property, Is.Not.Null);
            return (NetworkBool)property.GetValue(container);
        }

        private static bool TryUnlock(NetworkLootContainer container)
        {
            MethodInfo method = typeof(NetworkLootContainer).GetMethod("TryUnlock");
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(container, null);
        }

        private static void SetState(NetworkLootContainer container, string name, bool value)
        {
            typeof(NetworkLootContainer).GetProperty(name).SetValue(container, (NetworkBool)value);
        }

        private static void SetField(NetworkLootContainer container, string name, object value)
        {
            FieldInfo field = typeof(NetworkLootContainer).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(container, value);
        }
    }
}
#endif
