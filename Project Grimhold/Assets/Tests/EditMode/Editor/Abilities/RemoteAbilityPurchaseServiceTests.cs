#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RemoteAbilityPurchaseServiceTests
{
    private static readonly ProfileId Profile = new("88888888888888888888888888888888");
    private static readonly AbilityId Charge = new("charge");
    private const long Price = 100L;

    private GameObject _owner;
    private RemoteAbilityPurchaseService _service;
    private LocalProfileStore _store;
    private MemoryFileStore _files;
    private LocalProfileRepository _repository;
    private AbilityDefinitionCatalog _abilityCatalog;
    private AbilityDefinition _chargeDef;

    private int _backendCalls;
    private int _reconciliations;
    private bool _backendSuccess;
    private string _backendErrorCode;
    private bool _throwOnBackend;
    private string _calledTransactionId;
    private string _calledAbilityId;
    private int _calledRevision;

    [SetUp]
    public void SetUp()
    {
        var lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

        _chargeDef = AbilityTestFactory.CreateDefinition("charge");
        _abilityCatalog = AbilityTestFactory.CreateCatalog(_chargeDef);

        _files = new MemoryFileStore();
        _repository = new LocalProfileRepository(_files, ".");
        Assert.That(_repository.Initialize(Profile, lootCatalog), Is.True);
        _store = new LocalProfileStore(_repository, Profile, abilityCatalog: _abilityCatalog);

        // Seed currency and revision
        Assert.That(_store.TryCreditCurrency(500L), Is.EqualTo(StashOperationResult.Success));
        _store.SetRemoteRevision(3);

        _owner = new GameObject("RemoteAbilityTestOwner");
        _service = _owner.AddComponent<RemoteAbilityPurchaseService>();

        _backendCalls = 0;
        _reconciliations = 0;
        _backendSuccess = true;
        _backendErrorCode = null;
        _throwOnBackend = false;

        _service.Initialize(_store, MockBackend, () =>
        {
            _reconciliations++;
            return Task.FromResult(true);
        });
    }

    [TearDown]
    public void TearDown()
    {
        if (_owner != null)
        {
            UnityEngine.Object.DestroyImmediate(_owner);
        }

        if (_abilityCatalog != null)
        {
            UnityEngine.Object.DestroyImmediate(_abilityCatalog);
        }

        if (_chargeDef != null)
        {
            UnityEngine.Object.DestroyImmediate(_chargeDef);
        }
    }

    private Task<(bool success, PurchaseAbilityResult data, BackendError error)> MockBackend(
        string transactionId, string abilityId, int expectedRevision)
    {
        _backendCalls++;
        _calledTransactionId = transactionId;
        _calledAbilityId = abilityId;
        _calledRevision = expectedRevision;

        if (_throwOnBackend)
        {
            throw new InvalidOperationException("Transport error");
        }

        if (_backendSuccess)
        {
            var result = new PurchaseAbilityResult
            {
                alreadySecured = false,
                transactionId = transactionId,
                abilityId = abilityId,
                price = Price,
                currency = 400L,
                unlockedAbilities = new[] { abilityId },
                revision = expectedRevision + 1
            };
            return Task.FromResult((true, result, default(BackendError)));
        }

        return Task.FromResult((false, default(PurchaseAbilityResult), new BackendError
        {
            error = _backendErrorCode ?? "INTERNAL_ERROR",
            message = "Backend error"
        }));
    }

    [Test]
    public async Task BackendSuccess_DebitsCurrencyUnlocksAbilityAndUpdatesRevision()
    {
        var result = await _service.PurchaseAbilityAsync(Profile, Charge, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.Success));
        Assert.That(_backendCalls, Is.EqualTo(1));
        Assert.That(_calledAbilityId, Is.EqualTo("charge"));
        Assert.That(_calledRevision, Is.EqualTo(3));
        Assert.That(_store.GetCurrency(), Is.EqualTo(400L));
        Assert.That(_store.RemoteRevision, Is.EqualTo(4));
        Assert.That(_store.IsAbilityUnlocked(Charge), Is.True);
    }

    [Test]
    public async Task LocalInsufficientCurrency_RejectsWithoutCallingBackend()
    {
        var result = await _service.PurchaseAbilityAsync(Profile, Charge, 1000L);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.InsufficientCurrency));
        Assert.That(_backendCalls, Is.Zero);
        Assert.That(_store.GetCurrency(), Is.EqualTo(500L));
    }

    [Test]
    public async Task LocalAlreadyUnlocked_RejectsWithoutCallingBackend()
    {
        Assert.That(_store.TryUnlockAbility(Charge), Is.EqualTo(AbilityUnlockResult.Success));

        var result = await _service.PurchaseAbilityAsync(Profile, Charge, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.AlreadyUnlocked));
        Assert.That(_backendCalls, Is.Zero);
    }

    [Test]
    public async Task BackendReservationPending_MapsToReservationPendingAndReconciles()
    {
        _backendSuccess = false;
        _backendErrorCode = "RESERVATION_PENDING";

        var result = await _service.PurchaseAbilityAsync(Profile, Charge, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.ReservationPending));
        Assert.That(_backendCalls, Is.EqualTo(1));
        Assert.That(_reconciliations, Is.EqualTo(1));
        Assert.That(_store.GetCurrency(), Is.EqualTo(500L));
        Assert.That(_store.IsAbilityUnlocked(Charge), Is.False);
    }

    [Test]
    public async Task BackendRevisionConflict_MapsToRevisionConflictAndReconciles()
    {
        _backendSuccess = false;
        _backendErrorCode = "REVISION_CONFLICT";

        var result = await _service.PurchaseAbilityAsync(Profile, Charge, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.RevisionConflict));
        Assert.That(_backendCalls, Is.EqualTo(1));
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [Test]
    public async Task BackendTransportFailure_ReconcilesSafelyAndReturnsPersistenceFailed()
    {
        _throwOnBackend = true;

        var result = await _service.PurchaseAbilityAsync(Profile, Charge, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.PersistenceFailed));
        Assert.That(_backendCalls, Is.EqualTo(1));
        Assert.That(_reconciliations, Is.EqualTo(1));
    }

    [Test]
    public async Task NullBackend_FallsBackToLocalStore()
    {
        _service.Initialize(_store, null, null);

        var result = await _service.PurchaseAbilityAsync(Profile, Charge, Price);

        Assert.That(result, Is.EqualTo(AbilityPurchaseResult.Success));
        Assert.That(_store.GetCurrency(), Is.EqualTo(400L));
        Assert.That(_store.IsAbilityUnlocked(Charge), Is.True);
    }

    [Test]
    public void HydrateSnapshot_PopulatesUnlockedAbilitiesFromBackend_AndDropsOrphanedLocalUnlocks()
    {
        var snapshot = new LocalProfileSnapshot { ProfileId = Profile };
        snapshot.UnlockedAbilities.Add(new AbilityId("orphaned_local_ability"));

        var progressionData = new Grimhold.Backend.ProgressionData
        {
            level = 1,
            characterAttributes = new Grimhold.Backend.CharacterAttributesData
            {
                vitality = 10, resistance = 10, strength = 10, dexterity = 10, intelligence = 10, luck = 10, availablePoints = 0
            },
            unlockedAbilities = new[] { "charge", "unknown_spell_xyz" },
            revision = 9
        };

        var lootCatalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
            "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");

        bool hydrated = ApplicationStashServiceBootstrapper.HydrateSnapshot(
            Profile, snapshot, null, progressionData, lootCatalog, _abilityCatalog);

        Assert.That(hydrated, Is.True);
        Assert.That(snapshot.UnlockedAbilities, Has.Count.EqualTo(1));
        Assert.That(snapshot.UnlockedAbilities[0], Is.EqualTo(Charge));
        Assert.That(snapshot.RemoteRevision, Is.EqualTo(9));
    }

    private sealed class MemoryFileStore : ILocalProfileFileStore
    {
        private readonly Dictionary<string, string> _files = new Dictionary<string, string>();
        public bool FailWrites;
        public bool Exists(string path) => _files.ContainsKey(path);
        public bool TryRead(string path, out string contents, out string error)
        {
            error = null;
            return _files.TryGetValue(path, out contents);
        }
        public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath, string contents, out string error)
        {
            if (FailWrites) { error = "Simulated disk failure"; return false; }
            if (_files.TryGetValue(mainPath, out string previous)) _files[backupPath] = previous;
            _files[mainPath] = contents;
            error = null;
            return true;
        }
        public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
        {
            error = null;
            if (!_files.TryGetValue(backupPath, out string contents)) return false;
            _files[mainPath] = contents;
            return true;
        }
    }
}
#endif
