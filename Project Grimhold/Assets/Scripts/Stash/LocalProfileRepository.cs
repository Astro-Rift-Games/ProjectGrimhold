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
    private enum CacheRecoveryEligibility { None, ConfirmedDecodeFailure }
    private CacheRecoveryEligibility _recoveryEligibility;

    public bool CanRecoverCorruptCache =>
        Status == LocalProfilePersistenceStatus.Unavailable &&
        _recoveryEligibility == CacheRecoveryEligibility.ConfirmedDecodeFailure;

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
        _recoveryEligibility = CacheRecoveryEligibility.None;
        Snapshot = null;
        Status = LocalProfilePersistenceStatus.Unavailable;
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

        bool mainExists = _fileStore.Exists(_mainPath);
        bool backupExists = _fileStore.Exists(_backupPath);
        if (!mainExists && !backupExists)
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
        bool mainRead = false;
        if (mainExists && (mainRead = _fileStore.TryRead(_mainPath, out string mainJson, out mainReadError)) &&
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
        bool backupRead = false;
        if (backupExists && (backupRead = _fileStore.TryRead(_backupPath, out string backupJson, out backupReadError)) &&
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

        // At least one readable file failed decoding; every other existing file must
        // also have been readable. Missing files alone and IO failures never authorize reset.
        if ((mainRead || backupRead) && (!mainExists || mainRead) && (!backupExists || backupRead))
            _recoveryEligibility = CacheRecoveryEligibility.ConfirmedDecodeFailure;

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

        return TryWriteValidatedSnapshot(snapshot, out error);
    }

    public bool TryRecoverCorruptCache(ProfileId profileId, LocalProfileSnapshot candidate, out string error)
    {
        lock (_sync)
        {
            if (!CanRecoverCorruptCache || !profileId.IsValid || profileId != _profileId || _catalog == null)
            {
                error = "Profile cache is not eligible for authenticated corruption recovery.";
                return false;
            }

            return TryWriteValidatedSnapshot(candidate, out error);
        }
    }

    private bool TryWriteValidatedSnapshot(LocalProfileSnapshot snapshot, out string error)
    {
        error = null;
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
        _recoveryEligibility = CacheRecoveryEligibility.None;
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
