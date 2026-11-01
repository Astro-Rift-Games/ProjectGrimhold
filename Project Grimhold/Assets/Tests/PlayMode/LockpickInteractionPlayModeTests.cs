#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Tests.PlayMode;
using Object = UnityEngine.Object;
using Assert = NUnit.Framework.Assert;

public sealed class LockpickInteractionPlayModeTests
{
    private const string PlayerGuid = "fea3a7b256f965a4eb9b965832939741";
    private const string ParticipantGuid = "c39d451563bae6e43934008a0dadc6d6";
    private const string ContainerGuid = "d3073f98b1bfb53448989241f9087814";
    private static readonly LootId Lockpick = new("CON_LOCKPICK");
    private NetworkRunner _runner;
    private ExtractionProgressSimulationDriver _driver;
    private PlayerCharacter _player;
    private PlayerLootReceiver _inventory;
    private PlayerInteractionNetworkController _interaction;
    private NetworkLootContainer _chest;
    private NetworkLootContainerInteractable _interactable;

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_runner == null) yield break;
        if (_runner.IsRunning)
        {
            var shutdown = _runner.Shutdown();
            while (!shutdown.IsCompleted) yield return null;
        }
        if (_runner != null) Object.DestroyImmediate(_runner.gameObject);
    }

    [UnityTest]
    public IEnumerator MissingPick_RejectsWithoutOpeningOrRewards()
    {
        yield return Begin();
        yield return Execute(() =>
        {
            var result = Interact();
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InteractionFailureReason.LockpickRequired));
            Assert.That(_interaction.IsLockpicking, Is.False);
            Assert.That((bool)_chest.IsLocked, Is.True);
            Assert.That((bool)_interactable.FirstOpenResolved, Is.False);
        });
    }

    [UnityTest]
    public IEnumerator Completion_WaitsThenSpendsExactlyOneAndOpensSharedChest()
    {
        yield return Begin();
        yield return StartChannel();
        Assert.That(_inventory.GetLootAmount(Lockpick), Is.EqualTo(2));
        Assert.That((bool)_chest.IsLocked, Is.True);
        Assert.That((bool)_interactable.FirstOpenResolved, Is.False);
        yield return WaitTicks(5);
        Assert.That(_interaction.IsLockpicking, Is.True);
        yield return WaitFor(() => !_interaction.IsLockpicking);
        Assert.That((bool)_chest.IsLocked, Is.False);
        Assert.That((bool)_interactable.FirstOpenResolved, Is.True);
        Assert.That(_inventory.GetLootAmount(Lockpick), Is.EqualTo(1));
        yield return Execute(() => Assert.That(Interact().Success, Is.True));
        yield return WaitTicks(5);
        Assert.That(_inventory.GetLootAmount(Lockpick), Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator Movement_CancelsWithoutSpendingOrOpening()
    {
        yield return Begin();
        yield return StartChannel();
        yield return Execute(() => _player.transform.position += Vector3.right * 0.05f);
        yield return WaitFor(() => !_interaction.IsLockpicking);
        AssertInterrupted();
    }

    [UnityTest]
    public IEnumerator PositiveCommittedDamage_CancelsButZeroDamageDoesNot()
    {
        yield return Begin();
        yield return StartChannel();
        yield return Execute(() => Damage(0));
        Assert.That(_interaction.IsLockpicking, Is.True);
        yield return Execute(() => Damage(1));
        Assert.That(_interaction.IsLockpicking, Is.False);
        AssertInterrupted();
    }

    [UnityTest]
    public IEnumerator DamageOnDeadlineTick_InterruptsBeforeCompletion()
    {
        yield return Begin();
        yield return StartChannel();
        Action<NetworkRunner> damageOnDeadline = null;
        damageOnDeadline = runner =>
        {
            if (_interaction.LockpickRemainingSeconds > runner.DeltaTime)
            {
                _driver.PendingAction = damageOnDeadline;
                return;
            }
            Damage(1);
        };
        _driver.PendingAction = damageOnDeadline;
        yield return WaitFor(() => !_interaction.IsLockpicking);
        Assert.That(_driver.LastException, Is.Null, _driver.LastException?.ToString());
        AssertInterrupted();
    }

    [UnityTest]
    public IEnumerator RemovedPick_CancelsWithoutUnlocking()
    {
        yield return Begin();
        yield return StartChannel();
        yield return Execute(() =>
        {
            var request = new LootTransferRequest(_player.Id, _player.Id, Lockpick, 2, _runner.Tick);
            Assert.That(_inventory.TryResolveRaidLootOriginTransfer(request,
                RaidLootOriginSelectionOrder.PlayerFirst, out var origins), Is.True);
            _inventory.CommitRaidLootExtraction(request, origins);
        });
        yield return WaitFor(() => !_interaction.IsLockpicking);
        Assert.That((bool)_chest.IsLocked, Is.True);
        Assert.That((bool)_interactable.FirstOpenResolved, Is.False);
        Assert.That(_inventory.GetLootAmount(Lockpick), Is.Zero);
    }

    [UnityTest]
    public IEnumerator AnotherUnlock_CancelsWithoutSecondSpend()
    {
        yield return Begin();
        // Compete within the same tick, before frame/JIT stalls can expire the test deadline.
        yield return StartChannel(() => Assert.That(_chest.TryUnlock(), Is.True));
        yield return WaitFor(() => !_interaction.IsLockpicking);
        Assert.That(_inventory.GetLootAmount(Lockpick), Is.EqualTo(2));
        Assert.That((bool)_interactable.FirstOpenResolved, Is.False);
    }

    [UnityTest]
    public IEnumerator UnavailableTarget_CancelsWithoutSpending()
    {
        yield return Begin();
        yield return StartChannel();
        yield return Execute(() => _chest.SetAvailability(false));
        yield return WaitFor(() => !_interaction.IsLockpicking);
        AssertInterrupted();
    }

    [UnityTest]
    public IEnumerator DownedPlayer_CancelsWithoutSpending()
    {
        yield return Begin();
        yield return StartChannel();
        yield return Execute(() => Assert.That(
            _player.GetComponent<PlayerDownedStateNetworkController>().TryEnterDowned(), Is.True));
        yield return WaitFor(() => !_interaction.IsLockpicking);
        AssertInterrupted();
    }

    private void AssertInterrupted()
    {
        Assert.That((bool)_chest.IsLocked, Is.True);
        Assert.That((bool)_interactable.FirstOpenResolved, Is.False);
        Assert.That(_inventory.GetLootAmount(Lockpick), Is.EqualTo(2));
    }

    private IEnumerator StartChannel(Action afterStart = null)
    {
        yield return Execute(() =>
        {
            var request = new LootTransferRequest(new EntityId(123), _player.Id, Lockpick, 2, _runner.Tick);
            var origins = RaidLootOriginTransfer.Dungeon(2);
            Assert.That(_inventory.ValidateRaidLootOriginReceive(request, origins),
                Is.EqualTo(LootTransferFailureReason.None));
            _inventory.CommitRaidLootReceive(request, origins);
            var result = Interact();
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(InteractionFailureReason.LockpickingStarted));
            Assert.That(_interaction.IsLockpicking, Is.True);
            afterStart?.Invoke();
        });
    }

    private InteractionResult Interact() => _interactable.Interact(
        new InteractionRequest(_player.Id, _interactable.Id, _runner.Tick));

    private void Damage(float amount) => _player.ApplyDamage(new DamageRequest(
        new EntityId(123), _player.Id, amount, DamageType.TrueDamage,
        Vector2.right, Vector2.zero, _runner.Tick));

    private IEnumerator Begin()
    {
        var owner = new GameObject("LockpickInteractionTestRunner");
        _runner = owner.AddComponent<NetworkRunner>();
        PlayModeRunnerComposition.AddLocalInputContext(_runner);
        owner.AddComponent<EntityRegistry>();
        PlayModeRunnerComposition.AddTeamExtractionProgress(_runner);
        _driver = owner.AddComponent<ExtractionProgressSimulationDriver>();
        var start = _runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Single,
            SessionName = $"task-235-{Guid.NewGuid():N}",
            SceneManager = owner.AddComponent<NetworkSceneManagerDefault>(),
            ObjectProvider = owner.AddComponent<NetworkObjectProviderDefault>()
        });
        while (!start.IsCompleted) yield return null;
        Assert.That(start.Result.Ok, Is.True, start.Result.ShutdownReason.ToString());
        Assert.That(RaidParticipantId.TryCreate(1, out var id), Is.True);
        var participant = _runner.Spawn(Load(ParticipantGuid), onBeforeSpawned: (_, instance) =>
            instance.GetComponent<NetworkRaidParticipant>().Initialize("lockpick-player", id,
                ProgressionBalanceDefaults.InitialCharacterAttributeState,
                ExperienceCurve.InitialLevel, 0, "lockpick-test"))
            .GetComponent<NetworkRaidParticipant>();
        var avatar = _runner.Spawn(Load(PlayerGuid), Vector3.zero, Quaternion.identity,
            inputAuthority: _runner.LocalPlayer, onBeforeSpawned: (_, instance) =>
            {
                instance.GetComponent<RaidAvatarParticipantLink>().Initialize(participant.Object);
                Configure(instance.GetComponent<PlayerInteractionNetworkController>(), "_lockpickDurationSeconds", 0.4f);
            });
        Assert.That(participant.TrySetCurrentAvatar(avatar), Is.True);
        _player = avatar.GetComponent<PlayerCharacter>();
        _inventory = avatar.GetComponent<PlayerLootReceiver>();
        _interaction = avatar.GetComponent<PlayerInteractionNetworkController>();
        var chestObject = _runner.Spawn(Load(ContainerGuid), Vector3.right, Quaternion.identity,
            inputAuthority: null, onBeforeSpawned: (runner, instance) =>
            {
                var container = instance.GetComponent<NetworkLootContainer>();
                Assert.That(container.StartsLocked, Is.True, "The production LockedChest must start locked.");
                Assert.That(container.TrySetInitialContentOverride(runner, instance, Array.Empty<LootEntry>()), Is.True);
            });
        _chest = chestObject.GetComponent<NetworkLootContainer>();
        _interactable = chestObject.GetComponent<NetworkLootContainerInteractable>();
        Physics2D.SyncTransforms();
    }

    private NetworkObject Load(string guid) =>
        _runner.Config.PrefabTable.Load(_runner.Config.PrefabTable.GetId(NetworkObjectGuid.Parse(guid)), true);

    // Configure serialized authoring data only; gameplay goes through public simulation interfaces.
    private static void Configure(object target, string fieldName, object value) =>
        target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    private IEnumerator Execute(Action action)
    {
        _driver.ClearException();
        _driver.PendingAction = _ => action();
        yield return WaitFor(() => _driver.PendingAction == null);
        Assert.That(_driver.LastException, Is.Null, _driver.LastException?.ToString());
    }

    private IEnumerator WaitTicks(int count)
    {
        int until = _runner.Tick.Raw + count;
        yield return WaitFor(() => _runner.Tick.Raw >= until);
    }

    private static IEnumerator WaitFor(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 8f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Authoritative lockpick state was not reached before timeout.");
    }
}
#endif
