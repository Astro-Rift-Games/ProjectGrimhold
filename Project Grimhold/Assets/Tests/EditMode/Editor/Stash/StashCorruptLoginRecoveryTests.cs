#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Stash
{
    public sealed class StashCorruptLoginRecoveryTests
    {
        private readonly ProfileId _profile = new ProfileId("49494949494949494949494949494949");
        private LootDefinitionCatalog _catalog;
        private MemoryFiles _files;
        private LocalProfileRepository _repository;
        private InventoryData _inventory;
        private ProgressionData _progression;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(_catalog, Is.Not.Null);
            _files = new MemoryFiles();
            _repository = new LocalProfileRepository(_files, ".");
            _inventory = new InventoryData { currency = 41, revision = 7 };
            _progression = new ProgressionData { level = 1, revision = 7 };
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConfirmedMalformedCache_WithExplicitLoginPermission_RecoversAtomically(bool malformedBackup)
        {
            _files.Main = "not-json";
            _files.Backup = malformedBackup ? "also-not-json" : null;
            _files.BeforeWrite = () => Assert.That(_repository.Snapshot, Is.Null,
                "A recovery candidate must not be published before the atomic write succeeds.");
            Assert.That(Prepare(true), Is.True);
            Assert.That(_files.Writes, Is.EqualTo(1));
            Assert.That(_repository.Snapshot.ProfileId, Is.EqualTo(_profile));
            Assert.That(_repository.Snapshot.Currency, Is.EqualTo(41));
            Assert.That(_repository.Snapshot.RemoteRevision, Is.EqualTo(7));
            var reload = new LocalProfileRepository(_files, ".");
            Assert.That(reload.Initialize(_profile, _catalog), Is.True);
            Assert.That(reload.Snapshot.Currency, Is.EqualTo(41));
        }

        [TestCase("future-main")]
        [TestCase("future-backup")]
        [TestCase("io-main")]
        [TestCase("io-backup")]
        public void UnsupportedOrUnreadableCache_FailsClosed(string fault)
        {
            string future = WithSchemaVersion(HealthyJson(), LocalProfileSnapshot.CurrentSchemaVersion + 1);
            _files.Main = fault == "future-main" ? future : "not-json";
            _files.Backup = fault == "future-backup" ? future : "not-json";
            _files.ReadError = fault == "io-main" ? "main" : fault == "io-backup" ? "backup" : null;
            AssertRejectedWithoutWrites(true, _inventory, _progression);
        }

        [TestCase("invalid-inventory")]
        [TestCase("invalid-progression")]
        [TestCase("revision-mismatch")]
        [TestCase("missing-inventory")]
        [TestCase("missing-progression")]
        [TestCase("offline")]
        [TestCase("no-permission")]
        public void CorruptCache_WithoutCompleteValidatedLoginInputs_RemainsUntouched(string fault)
        {
            _files.Main = "not-json";
            _files.Backup = "also-not-json";
            if (fault == "invalid-inventory") _inventory.currency = -1;
            if (fault == "invalid-progression")
                _progression.characterAttributes = new CharacterAttributesData { vitality = -1 };
            if (fault == "revision-mismatch") _progression.revision++;
            AssertRejectedWithoutWrites(fault != "no-permission" && fault != "offline",
                fault == "missing-inventory" || fault == "offline" ? (InventoryData?)null : _inventory,
                fault == "missing-progression" || fault == "offline" ? (ProgressionData?)null : _progression);
        }

        [Test]
        public void CorruptRecovery_AtomicWriteFailure_DoesNotPublishCandidateOrAlterFiles()
        {
            _files.Main = "not-json";
            _files.FailWrite = true;
            Assert.That(Prepare(true), Is.False);
            Assert.That(_files.Writes, Is.EqualTo(1), "Must attempt the validated recovery write.");
            Assert.That(_repository.Snapshot, Is.Null);
            Assert.That(_files.Main, Is.EqualTo("not-json"));
            Assert.That(_files.Backup, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HealthyCacheOrBackup_PreservesPendingExtractionCommit(bool useBackup)
        {
            Assert.That(_catalog.TryGetByIndex(0, out LootDefinition definition), Is.True);
            var item = new StashItem(definition.LootId, 1);
            var snapshot = new LocalProfileSnapshot { ProfileId = _profile };
            snapshot.Loadout.Add(item);
            snapshot.PendingExtractionCommit = new PendingExtractionCommit(
                new ExtractionReceipt("recovery-raid", _profile, 1), new[] { item }, default, 0, 1);
            string json = LocalProfileSaveCodec.Encode(snapshot);
            Assert.That(LocalProfileSaveCodec.TryDecode(json, _profile, _catalog, null,
                out _, out _, out string error), Is.True, error);
            _files.Main = useBackup ? "not-json" : json;
            _files.Backup = useBackup ? json : null;
            Assert.That(Prepare(true), Is.True);
            Assert.That(_repository.Snapshot.PendingExtractionCommit.Receipt.RaidId, Is.EqualTo("recovery-raid"));
            CollectionAssert.AreEqual(new[] { item }, _repository.Snapshot.PendingExtractionCommit.Items);
            CollectionAssert.AreEqual(new[] { item }, _repository.Snapshot.Loadout);
            var reload = new LocalProfileRepository(_files, ".");
            Assert.That(reload.Initialize(_profile, _catalog), Is.True);
            Assert.That(reload.Snapshot.PendingExtractionCommit, Is.Not.Null);
        }

        [Test]
        public void HealthyCache_InvalidHydration_DoesNotWriteOrPublishClone()
        {
            _files.Main = HealthyJson();
            _inventory.currency = -1;
            LogAssert.Expect(LogType.Error, new Regex("Hydration failed: negative currency"));
            Assert.That(Invoke(true, _inventory, _progression), Is.False);
            Assert.That(_files.Writes, Is.Zero);
            Assert.That(_repository.Snapshot.Currency, Is.EqualTo(3));
        }

        [Test]
        public void HealthyCache_SaveFailure_KeepsPreviouslyPublishedSnapshot()
        {
            _files.Main = HealthyJson();
            string before = _files.Main;
            _files.FailWrite = true;
            _files.BeforeWrite = () => Assert.That(_repository.Snapshot.Currency, Is.EqualTo(3));
            LogAssert.Expect(LogType.Error, new Regex("Hydrated profile could not be saved"));
            Assert.That(Invoke(true, _inventory, _progression), Is.False);
            Assert.That(_files.Writes, Is.EqualTo(1));
            Assert.That(_repository.Snapshot.Currency, Is.EqualTo(3));
            Assert.That(_files.Main, Is.EqualTo(before));
        }

        [TestCase("null")]
        [TestCase("identity")]
        [TestCase("codec")]
        [TestCase("caller-identity")]
        public void RepositoryRecovery_RejectsInvalidCandidateWithoutWriting(string fault)
        {
            _files.Main = "not-json";
            ExpectInitializeFailure(_profile, _catalog);
            Assert.That(_repository.CanRecoverCorruptCache, Is.True);
            var candidate = new LocalProfileSnapshot { ProfileId = _profile };
            if (fault == "identity") candidate.ProfileId = default;
            if (fault == "codec") candidate.Currency = -1;
            Assert.That(_repository.TryRecoverCorruptCache(fault == "caller-identity" ? default : _profile,
                fault == "null" ? null : candidate, out _), Is.False);
            Assert.That(_files.Writes, Is.Zero);
            Assert.That(_files.Main, Is.EqualTo("not-json"));
            Assert.That(_repository.Snapshot, Is.Null);
            Assert.That(_repository.Status, Is.EqualTo(LocalProfilePersistenceStatus.Unavailable));
            Assert.That(_repository.TrySave(candidate, out _), Is.False);
        }

        [TestCase("io")]
        [TestCase("configuration")]
        [TestCase("identity")]
        [TestCase("missing")]
        [TestCase("future")]
        public void RepositoryRecovery_ReinitializeClearsPriorEligibility(string fault)
        {
            _files.Main = "not-json";
            ExpectInitializeFailure(_profile, _catalog);
            Assert.That(_repository.CanRecoverCorruptCache, Is.True);
            if (fault == "io") _files.ReadError = "main";
            if (fault == "future") _files.Main = WithSchemaVersion(HealthyJson(), LocalProfileSnapshot.CurrentSchemaVersion + 1);
            if (fault == "missing")
            {
                _files.Main = null;
                Assert.That(_repository.Initialize(_profile, _catalog), Is.True);
            }
            else ExpectInitializeFailure(fault == "identity" ? default : _profile, fault == "configuration" ? null : _catalog);
            Assert.That(_repository.CanRecoverCorruptCache, Is.False);
            Assert.That(_repository.TryRecoverCorruptCache(_profile,
                new LocalProfileSnapshot { ProfileId = _profile }, out _), Is.False);
            Assert.That(_files.Writes, Is.Zero);
        }

        [Test]
        public void RepositoryRecovery_SuccessAllowsOrdinarySaveAndCannotRecoverAgain()
        {
            _files.Main = "not-json";
            Assert.That(Prepare(true), Is.True);
            Assert.That(_repository.CanRecoverCorruptCache, Is.False);
            var candidate = _repository.Snapshot.Clone();
            candidate.Currency++;
            Assert.That(_repository.TryRecoverCorruptCache(_profile, candidate, out _), Is.False);
            Assert.That(_repository.TrySave(candidate, out string error), Is.True, error);
            Assert.That(_repository.Snapshot.Currency, Is.EqualTo(42));
        }

        [Test]
        public void SupportedOlderSchema_LoadsNormallyWithoutRecoveryEligibility()
        {
            _files.Main = WithSchemaVersion(HealthyJson(), LocalProfileSnapshot.CurrentSchemaVersion - 1);
            Assert.That(_repository.Initialize(_profile, _catalog), Is.True);
            Assert.That(_repository.CanRecoverCorruptCache, Is.False);
            Assert.That(_repository.Snapshot.Currency, Is.EqualTo(3));
            Assert.That(_files.Writes, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepositoryRecovery_UninitializedOrIOOnly_IsNeverEligible(bool initializeWithIO)
        {
            if (initializeWithIO)
            {
                _files.Main = HealthyJson();
                _files.ReadError = "main";
                ExpectInitializeFailure(_profile, _catalog);
            }
            Assert.That(_repository.CanRecoverCorruptCache, Is.False);
            Assert.That(_repository.TryRecoverCorruptCache(_profile,
                new LocalProfileSnapshot { ProfileId = _profile }, out _), Is.False);
            Assert.That(_repository.TrySave(new LocalProfileSnapshot { ProfileId = _profile }, out _), Is.False);
            Assert.That(_files.Writes, Is.Zero);
        }

        [Test]
        public void CorruptRecovery_EqualZeroRevisions_AreAccepted()
        {
            _files.Main = "not-json";
            _inventory.revision = _progression.revision = 0;
            Assert.That(Prepare(true), Is.True);
            Assert.That(_repository.Snapshot.RemoteRevision, Is.Zero);
        }

        private void ExpectInitializeFailure(ProfileId profile, LootDefinitionCatalog catalog)
        {
            LogAssert.Expect(LogType.Error, new Regex("\\[LocalProfileRepository\\]"));
            Assert.That(_repository.Initialize(profile, catalog), Is.False);
        }

        private string HealthyJson()
        {
            return LocalProfileSaveCodec.Encode(new LocalProfileSnapshot { ProfileId = _profile, Currency = 3 });
        }

        private static string WithSchemaVersion(string json, int version)
        {
            string replaced = Regex.Replace(json, "\"schemaVersion\": \\d+", $"\"schemaVersion\": {version}");
            Assert.That(replaced, Is.Not.EqualTo(json));
            return replaced;
        }

        private void AssertRejectedWithoutWrites(bool permission, InventoryData? inventory, ProgressionData? progression)
        {
            string main = _files.Main, backup = _files.Backup;
            Assert.That(Prepare(permission, inventory, progression), Is.False);
            Assert.That(_files.Writes, Is.Zero);
            Assert.That(_files.Restores, Is.Zero);
            Assert.That(_files.Main, Is.EqualTo(main));
            Assert.That(_files.Backup, Is.EqualTo(backup));
            Assert.That(_repository.Snapshot, Is.Null);
        }

        private bool Prepare(bool permission) => Prepare(permission, _inventory, _progression);
        private bool Prepare(bool permission, InventoryData? inventory, ProgressionData? progression)
        {
            // Corruption diagnostics may disappear after successful recovery. Assertions above
            // check outcomes; do not make the RED depend on today's failure-only log messages.
            bool previous = LogAssert.ignoreFailingMessages;
            try { LogAssert.ignoreFailingMessages = true; return Invoke(permission, inventory, progression); }
            finally { LogAssert.ignoreFailingMessages = previous; }
        }

        private bool Invoke(bool permission, InventoryData? inventory, ProgressionData? progression)
        {
            MethodInfo method = typeof(ApplicationStashServiceBootstrapper).GetMethod(
                "PrepareRepositoryForLogin", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] {
                _repository, _profile, _catalog, null, inventory, progression, permission });
        }

        private sealed class MemoryFiles : ILocalProfileFileStore
        {
            public string Main, Backup, ReadError;
            public bool FailWrite;
            public int Writes, Restores;
            public Action BeforeWrite;
            private bool IsBackup(string path) => path.EndsWith(".bak", StringComparison.Ordinal);
            public bool Exists(string path) => (IsBackup(path) ? Backup : Main) != null;
            public bool TryRead(string path, out string contents, out string error)
            {
                bool backup = IsBackup(path);
                contents = backup ? Backup : Main;
                error = ReadError == (backup ? "backup" : "main") ? "simulated read IO failure" : null;
                return error == null && contents != null;
            }
            public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath,
                string contents, out string error)
            {
                Writes++;
                BeforeWrite?.Invoke();
                error = FailWrite ? "simulated atomic write failure" : null;
                if (FailWrite) return false;
                Backup = Main;
                Main = contents;
                return true;
            }
            public bool TryRestoreMainFromBackup(string mainPath, string backupPath, out string error)
            {
                Restores++;
                error = Backup == null ? "missing backup" : null;
                if (error != null) return false;
                Main = Backup;
                return true;
            }
        }
    }
}
#endif
