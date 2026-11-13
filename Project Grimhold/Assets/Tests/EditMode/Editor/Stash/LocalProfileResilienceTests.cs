#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;
using System.Reflection;
using Grimhold.Backend;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Stash
{
    public sealed class LocalProfileResilienceTests
    {
        private readonly ProfileId _profile = new ProfileId("50505050505050505050505050505050");
        private LootDefinitionCatalog _catalog;
        private TestMemoryFiles _files;
        private LocalProfileRepository _repository;
        private InventoryData _inventory;
        private ProgressionData _progression;

        [SetUp]
        public void SetUp()
        {
            _catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(_catalog, Is.Not.Null);
            _files = new TestMemoryFiles();
            _repository = new LocalProfileRepository(_files, ".");
            _inventory = new InventoryData { currency = 100, revision = 5 };
            _progression = new ProgressionData { level = 2, revision = 5 };
        }

        [Test]
        public void ProfileWithUnknownLootId_AndPendingExtractionCommit_PreservesCommitAfterLogin()
        {
            // Create a valid snapshot containing an unknown loot item (content drift)
            // along with a pending extraction commit.
            Assert.That(_catalog.TryGetByIndex(0, out LootDefinition validDef), Is.True);
            var validItem = new StashItem(validDef.LootId, 2).AtSlot(0);
            var unknownItem = new StashItem(new LootId("unknown_version_drift_item_999"), 1).AtSlot(1);

            var snapshot = new LocalProfileSnapshot
            {
                ProfileId = _profile,
                Currency = 50,
                RemoteRevision = 5,
                InventorySchemaVersion = 3
            };
            snapshot.Loadout.Add(validItem);
            snapshot.Loadout.Add(unknownItem);

            var pendingExtractionItem = new StashItem(new LootId("unknown_extracted_drift_item_888"), 3);
            snapshot.PendingExtractionCommit = new PendingExtractionCommit(
                new ExtractionReceipt("raid-resilience-41", _profile, 1),
                new[] { pendingExtractionItem },
                default,
                150,
                2);

            string encodedJson = LocalProfileSaveCodec.Encode(snapshot);
            _files.Main = encodedJson;
            _files.Backup = null;

            // PrepareRepositoryForLogin must succeed without discarding the PendingExtractionCommit
            bool loginResult = InvokePrepareRepositoryForLogin(permission: true, _inventory, _progression);
            Assert.That(loginResult, Is.True, "Login preparation must succeed for a profile with unknown loot items.");

            Assert.That(_repository.Snapshot, Is.Not.Null);
            Assert.That(_repository.Snapshot.PendingExtractionCommit, Is.Not.Null,
                "PendingExtractionCommit must be preserved after login even if profile contains unknown loot IDs.");
            Assert.That(_repository.Snapshot.PendingExtractionCommit.Receipt.RaidId, Is.EqualTo("raid-resilience-41"));
            Assert.That(_repository.Snapshot.PendingExtractionCommit.Items.Count, Is.EqualTo(1));
            Assert.That(_repository.Snapshot.PendingExtractionCommit.Items[0].LootId.Value,
                Is.EqualTo("unknown_extracted_drift_item_888"));

            // Reload and verify persisted durability
            var reloadedRepo = new LocalProfileRepository(_files, ".");
            Assert.That(reloadedRepo.Initialize(_profile, _catalog), Is.True);
            Assert.That(reloadedRepo.Snapshot.PendingExtractionCommit, Is.Not.Null,
                "Persisted file must retain PendingExtractionCommit upon reload.");
        }

        [Test]
        public void TransientReadFailureOnMainFile_WithValidBackup_DoesNotRestoreBackupOverMain()
        {
            // Main contains the latest version with currency 999
            var mainSnapshot = new LocalProfileSnapshot { ProfileId = _profile, Currency = 999 };
            string mainJson = LocalProfileSaveCodec.Encode(mainSnapshot);

            // Backup contains an older version with currency 111
            var backupSnapshot = new LocalProfileSnapshot { ProfileId = _profile, Currency = 111 };
            string backupJson = LocalProfileSaveCodec.Encode(backupSnapshot);

            _files.Main = mainJson;
            _files.Backup = backupJson;
            _files.ReadError = "main"; // Simulate transient IO failure reading main

            // Initialize repository
            bool initialized = _repository.Initialize(_profile, _catalog);

            // Must NOT overwrite the main file with the backup!
            Assert.That(_files.Restores, Is.Zero,
                "A transient read failure on main file must NOT trigger TryRestoreMainFromBackup.");
            Assert.That(_files.Main, Is.EqualTo(mainJson),
                "Main file contents must remain unchanged after a transient read failure.");
        }

        [Test]
        public void CorruptMainFile_ConfirmedDecodeFailure_RestoresBackupOverMain()
        {
            // Main contains invalid malformed data
            _files.Main = "{ corrupt json ";

            // Backup contains valid data
            var backupSnapshot = new LocalProfileSnapshot { ProfileId = _profile, Currency = 111 };
            _files.Backup = LocalProfileSaveCodec.Encode(backupSnapshot);
            _files.ReadError = null;

            // When main was actually read and confirmed corrupt, restoring backup is expected
            bool initialized = _repository.Initialize(_profile, _catalog);
            Assert.That(initialized, Is.True);
            Assert.That(_files.Restores, Is.EqualTo(1),
                "When main file is genuinely corrupted, restoring backup is expected.");
        }

        private bool InvokePrepareRepositoryForLogin(bool permission, InventoryData? inventory, ProgressionData? progression)
        {
            MethodInfo method = typeof(ApplicationStashServiceBootstrapper).GetMethod(
                "PrepareRepositoryForLogin", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] {
                _repository, _profile, _catalog, null, inventory, progression, permission });
        }

        private sealed class TestMemoryFiles : ILocalProfileFileStore
        {
            public string Main;
            public string Backup;
            public string ReadError;
            public int Restores;
            public int Writes;

            private bool IsBackup(string path) => path != null && path.EndsWith(".bak", StringComparison.Ordinal);

            public bool Exists(string path) => (IsBackup(path) ? Backup : Main) != null;

            public bool TryRead(string path, out string contents, out string error)
            {
                bool backup = IsBackup(path);
                contents = backup ? Backup : Main;
                error = ReadError == (backup ? "backup" : "main") ? "simulated transient read IO failure" : null;
                return error == null && contents != null;
            }

            public bool TryWriteAtomically(string mainPath, string temporaryPath, string backupPath,
                string contents, out string error)
            {
                Writes++;
                error = null;
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
