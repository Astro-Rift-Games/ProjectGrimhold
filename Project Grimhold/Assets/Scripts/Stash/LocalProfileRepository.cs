using System;
using UnityEngine;

/// <summary>
/// Loads and durably saves the complete local profile aggregate.
/// </summary>
public sealed class LocalProfileRepository : ILocalProfileRepository
{
    private readonly object _sync = new();
    private readonly ILocalProfileFileStore _fileStore;
    private string _mainPath;
    private string _temporaryPath;
    private string _backupPath;
    private readonly string _directory;
    private LootDefinitionCatalog _catalog;
    private readonly AbilityDefinitionCatalog _abilityCatalog;
    private ProfileId _profileId;

    public LocalProfilePersistenceStatus Status { get; private set; } = LocalProfilePersistenceStatus.Unavailable;
    public string LastError { get; private set; }
    public LocalProfileSnapshot Snapshot { get; private set; }

    public LocalProfileRepository(
        ILocalProfileFileStore fileStore,
        string directory,
        AbilityDefinitionCatalog abilityCatalog = null)
    {
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _directory = directory;
        _abilityCatalog = abilityCatalog;
    }

    public bool Initialize(ProfileId profileId, LootDefinitionCatalog catalog)
    {
        _profileId = profileId;
        _catalog = catalog;
        if (!profileId.IsValid || catalog == null)
        {
            return Fail(LocalProfilePersistenceStatus.Unavailable, "Local profile identity or loot catalog is missing.");
        }

        string filename = $"grimhold-profile-{profileId.Value}.json";
        _mainPath = System.IO.Path.Combine(_directory, filename);
        _temporaryPath = _mainPath + ".tmp";
        _backupPath = _mainPath + ".bak";

        if (!_fileStore.Exists(_mainPath) && !_fileStore.Exists(_backupPath))
        {
            Snapshot = new LocalProfileSnapshot { ProfileId = profileId };
            Status = LocalProfilePersistenceStatus.Ready;
            LastError = null;
            return true;
        }

        LocalProfilePersistenceStatus mainStatus = LocalProfilePersistenceStatus.Unavailable;
        string mainError = null;
        string mainReadError = null;
        LocalProfileSnapshot mainSnapshot = null;
        if (_fileStore.Exists(_mainPath) && _fileStore.TryRead(_mainPath, out string mainJson, out mainReadError) &&
            LocalProfileSaveCodec.TryDecode(mainJson, profileId, catalog, _abilityCatalog, out mainSnapshot, out mainStatus, out mainError))
        {
            Snapshot = mainSnapshot;
            Status = mainStatus;
            LastError = null;
            return true;
        }

        if (mainStatus == LocalProfilePersistenceStatus.UnsupportedVersion)
        {
            return Fail(mainStatus, mainError);
        }

        string backupReadError = null;
        string backupError = null;
        LocalProfilePersistenceStatus backupStatus = LocalProfilePersistenceStatus.Unavailable;
        LocalProfileSnapshot backupSnapshot = null;
        if (_fileStore.Exists(_backupPath) && _fileStore.TryRead(_backupPath, out string backupJson, out backupReadError) &&
            LocalProfileSaveCodec.TryDecode(backupJson, profileId, catalog, _abilityCatalog, out backupSnapshot, out backupStatus, out backupError))
        {
            Snapshot = backupSnapshot;
            Status = LocalProfilePersistenceStatus.RecoveredFromBackup;
            LastError = mainError ?? mainReadError;
            if (!_fileStore.TryRestoreMainFromBackup(_mainPath, _backupPath, out string repairError))
            {
                LastError = $"Recovered backup but could not repair main file: {repairError}";
            }
            return true;
        }

        if (backupStatus == LocalProfilePersistenceStatus.UnsupportedVersion)
        {
            return Fail(backupStatus, backupError);
        }

        return Fail(LocalProfilePersistenceStatus.Unavailable,
            $"No valid profile state could be loaded. Main: {mainError ?? mainReadError}; Backup: {backupError ?? backupReadError}.");
    }

    public bool TrySave(LocalProfileSnapshot snapshot, out string error)
    {
        lock (_sync)
        {
            return TrySaveCore(snapshot, out error);
        }
    }

    private bool TrySaveCore(LocalProfileSnapshot snapshot, out string error)
    {
        error = null;
        if (Status == LocalProfilePersistenceStatus.Unavailable || Status == LocalProfilePersistenceStatus.UnsupportedVersion)
        {
            error = LastError ?? "Profile persistence is unavailable.";
            return false;
        }

        if (snapshot == null || snapshot.ProfileId != _profileId)
        {
            error = "Snapshot profile ID does not match the initialized local profile.";
            return false;
        }

        if (!LocalProfileSaveCodec.TryDecode(LocalProfileSaveCodec.Encode(snapshot), snapshot.ProfileId, _catalog, _abilityCatalog, out _, out _, out error))
        {
            LastError = error;
            return false;
        }

        string json = LocalProfileSaveCodec.Encode(snapshot);
        if (!_fileStore.TryWriteAtomically(_mainPath, _temporaryPath, _backupPath, json, out error))
        {
            LastError = error;
            return false;
        }

        Snapshot = snapshot.Clone();
        Status = LocalProfilePersistenceStatus.Ready;
        LastError = null;
        return true;
    }

    private bool Fail(LocalProfilePersistenceStatus status, string error)
    {
        Status = status;
        LastError = error;
        Snapshot = null;
        Debug.LogError($"[LocalProfileRepository] {error}");
        return false;
    }
}
