using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Local presentation state for the extraction-to-Loadout transaction.
/// This is not replicated gameplay state; State Authority remains the source
/// of truth for the participant result and inventory contents.
/// </summary>
public enum ExtractionLootSaveStatus
{
    None,
    Pending,
    PersistenceFailed,
    Committed
}

/// <summary>
/// Coordinates the authoritative extraction result with the local player's
/// process-local Loadout commit.
///
/// State Authority retains the raid snapshot until Input Authority acknowledges
/// an idempotent local commit. The raid inventory is therefore never cleared
/// before the local commit succeeds, and a lost RPC can be retried safely.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerExtractionController))]
[RequireComponent(typeof(PlayerLootReceiver))]
[RequireComponent(typeof(PlayerWeaponEquipmentNetworkController))]
[RequireComponent(typeof(ExtractedLootExperienceProducer))]
public sealed class PlayerExtractionLootSaver : NetworkBehaviour
{
    internal const int MaximumSnapshotEntries =
        PlayerLootReceiver.MaxDistinctLootTypes + EquipmentSlotRules.SlotCount;
    private const float RetryIntervalSeconds = 1f;

    private PlayerExtractionController _extractionController;
    private PlayerLootReceiver _lootReceiver;
    private PlayerWeaponEquipmentNetworkController _weaponEquipment;
    private ExtractedLootExperienceProducer _experienceProducer;
    private RaidAvatarParticipantLink _participantLink;
    private NetworkRaidParticipant _participant;
    private IReadOnlyList<LootEntry> _pendingSnapshot;
    private PlayerExpeditionLootSnapshot _pendingOwnershipSnapshot;
    private int[] _pendingCatalogIndices;
    private int[] _pendingAmounts;
    private int _pendingResultSequence;
    private ExtractedLootExperienceCandidate? _pendingExperienceCandidate;
    private bool _localCommitAttempted;

    [Networked]
    private TickTimer RetryTimer { get; set; }

    /// <summary>
    /// Gets the local commit state observed by the result presenter.
    /// </summary>
    public ExtractionLootSaveStatus LocalSaveStatus { get; private set; }

    private void Awake()
    {
        _extractionController = GetComponent<PlayerExtractionController>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
        _weaponEquipment = GetComponent<PlayerWeaponEquipmentNetworkController>();
        _experienceProducer = GetComponent<ExtractedLootExperienceProducer>();
        _participantLink = GetComponent<RaidAvatarParticipantLink>();
        LocalSaveStatus = ExtractionLootSaveStatus.None;
    }

    public override void Spawned()
    {
        _extractionController.ExtractionCompleted += HandleExtractionCompleted;
        _participantLink?.TryResolveParticipant(out _participant);

        if (HasStateAuthority && TryResolvePendingParticipant() &&
            _participant.State == RaidParticipantState.Extracted &&
            !_participant.IsExtractionCommitConfirmed)
        {
            PreparePendingTransaction();
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !TryResolvePendingParticipant() ||
            _participant.State != RaidParticipantState.Extracted ||
            _participant.IsExtractionCommitConfirmed)
        {
            return;
        }

        if (!HasPendingTransaction() && !PreparePendingTransaction())
        {
            return;
        }

        if (!RetryTimer.ExpiredOrNotRunning(Runner))
        {
            return;
        }

        SendPendingTransaction();
        RetryTimer = TickTimer.CreateFromSeconds(Runner, RetryIntervalSeconds);
    }

    public override void Render()
    {
        if (HasStateAuthority || LocalSaveStatus != ExtractionLootSaveStatus.Pending ||
            _localCommitAttempted || !HasValidPendingPayload())
        {
            return;
        }

        // Fusion may deliver the RPC before the participant's Extracted state
        // is visible on this peer. Keep the validated payload and retry once
        // the replicated participant state catches up.
        TryCommitPendingLocally();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_extractionController != null)
        {
            _extractionController.ExtractionCompleted -= HandleExtractionCompleted;
        }

        _participant = null;
        ClearPendingTransaction();
    }

    /// <summary>
    /// Requests one retry after a local aggregate commit failure. Network
    /// retransmission remains automatic, but it never reopens a failed local
    /// operation without an explicit retry.
    /// </summary>
    public void RetryLocalCommit()
    {
        if (LocalSaveStatus != ExtractionLootSaveStatus.PersistenceFailed ||
            !HasValidPendingPayload())
        {
            return;
        }

        _localCommitAttempted = false;
        LocalSaveStatus = ExtractionLootSaveStatus.Pending;
        TryCommitPendingLocally();
    }

    private void HandleExtractionCompleted(PlayerExtractionController controller)
    {
        if (!HasStateAuthority || controller != _extractionController ||
            !TryResolvePendingParticipant() ||
            _participant.State != RaidParticipantState.Extracted ||
            _participant.IsExtractionCommitConfirmed)
        {
            return;
        }

        PreparePendingTransaction();
        RetryTimer = TickTimer.None;
    }

    private bool PreparePendingTransaction()
    {
        _pendingExperienceCandidate = null;
        string snapshotError = null;
        if (!TryResolvePendingParticipant() ||
            _participant.State != RaidParticipantState.Extracted ||
            _participant.IsExtractionCommitConfirmed ||
            _lootReceiver == null || _weaponEquipment == null ||
            !PlayerExpeditionLootSnapshot.TryCapture(
                _lootReceiver,
                _weaponEquipment,
                out PlayerExpeditionLootSnapshot ownershipSnapshot,
                out snapshotError))
        {
            if (!string.IsNullOrEmpty(snapshotError))
            {
                Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: {snapshotError}", this);
            }
            return false;
        }

        IReadOnlyList<LootEntry> snapshot = ownershipSnapshot.Combined;

        LootDefinitionCatalog catalog = _lootReceiver.LootCatalog;
        if (catalog == null || snapshot.Count > MaximumSnapshotEntries)
        {
            Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Extraction snapshot cannot be resolved.", this);
            return false;
        }

        var indices = new int[snapshot.Count];
        var amounts = new int[snapshot.Count];
        for (int i = 0; i < snapshot.Count; i++)
        {
            LootEntry entry = snapshot[i];
            if (!entry.IsValid || !catalog.TryGetIndex(entry.LootId, out int index) ||
                index < 0 || index >= PlayerLootReceiver.MaxCatalogEntries)
            {
                Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Invalid extraction snapshot entry.", this);
                return false;
            }

            for (int previous = 0; previous < i; previous++)
            {
                if (indices[previous] == index)
                {
                    Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Duplicate extraction snapshot entry.", this);
                    return false;
                }
            }

            indices[i] = index;
            amounts[i] = entry.Amount;
        }

        int resultSequence = _participant.ResultSequence;
        ExtractedLootExperienceCandidate? experienceCandidate = null;
        if (_experienceProducer == null)
        {
            Debug.LogError(
                $"{nameof(PlayerExtractionLootSaver)}: Missing {nameof(ExtractedLootExperienceProducer)}; " +
                "the Loot extraction will continue without an Experience reward.",
                this);
        }
        else
        {
            NetworkSpawnManager spawnManager = Runner?.GetComponent<NetworkSpawnManager>();
            if (spawnManager == null ||
                !spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations))
            {
                Debug.LogError(
                    $"{nameof(PlayerExtractionLootSaver)}: Initial Raid affiliations are unavailable; " +
                    "the Loot extraction will continue without an Experience reward.",
                    this);
            }
            else if (_experienceProducer.TryPrepare(
                         resultSequence,
                         _participant.RaidParticipantId,
                         ownershipSnapshot,
                         affiliations,
                         out ExtractedLootExperienceCandidate preparedCandidate,
                         out string experienceError))
            {
                experienceCandidate = preparedCandidate;
            }
            else
            {
                Debug.LogError(
                    $"{nameof(PlayerExtractionLootSaver)}: {experienceError ?? "Extracted Loot Experience could not be calculated."} " +
                    "The Loot extraction will continue.",
                    this);
            }
        }

        _pendingSnapshot = snapshot;
        _pendingOwnershipSnapshot = ownershipSnapshot;
        _pendingCatalogIndices = indices;
        _pendingAmounts = amounts;
        _pendingResultSequence = resultSequence;
        _pendingExperienceCandidate = experienceCandidate;
        LocalSaveStatus = ExtractionLootSaveStatus.Pending;
        _localCommitAttempted = false;
        return true;
    }

    private void SendPendingTransaction()
    {
        if (!HasValidPendingPayload() || !TryResolvePendingParticipant())
        {
            return;
        }

        RPC_CommitExtractionOnInputAuthority(
            _pendingResultSequence,
            _pendingCatalogIndices,
            _pendingAmounts);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_CommitExtractionOnInputAuthority(
        int resultSequence,
        int[] catalogIndices,
        int[] amounts)
    {
        if (!ValidateIncomingPayload(catalogIndices, amounts))
        {
            LocalSaveStatus = ExtractionLootSaveStatus.PersistenceFailed;
            return;
        }

        if (LocalSaveStatus == ExtractionLootSaveStatus.PersistenceFailed &&
            _localCommitAttempted && _pendingResultSequence == resultSequence)
        {
            return;
        }

        _pendingResultSequence = resultSequence;
        if (!_pendingExperienceCandidate.HasValue ||
            !_pendingExperienceCandidate.Value.Matches(resultSequence))
        {
            _pendingExperienceCandidate = null;
        }
        _pendingCatalogIndices = (int[])catalogIndices.Clone();
        _pendingAmounts = (int[])amounts.Clone();
        _pendingSnapshot = BuildSnapshot(catalogIndices, amounts);
        if (_pendingSnapshot == null)
        {
            LocalSaveStatus = ExtractionLootSaveStatus.PersistenceFailed;
            return;
        }
        _localCommitAttempted = false;
        LocalSaveStatus = ExtractionLootSaveStatus.Pending;
        TryCommitPendingLocally();
    }

    private void TryCommitPendingLocally()
    {
        if (!TryResolvePendingParticipant() || !HasValidPendingPayload() ||
            _participant.State != RaidParticipantState.Extracted ||
            _pendingResultSequence != _participant.ResultSequence)
        {
            // The payload is valid, but the participant snapshot has not caught
            // up on this peer yet. Render() will retry without committing locally.
            return;
        }

        _localCommitAttempted = true;

        ApplicationStashContext context = FindAnyObjectByType<ApplicationStashContext>();
        ProfileId expectedProfileId = new ProfileId(_participant.ProfileId.Value);
        if (context?.Store == null || context.Store.ProfileId != expectedProfileId)
        {
            Debug.LogError(
                $"{nameof(PlayerExtractionLootSaver)}: Local stash context is unavailable or belongs to another profile.",
                this);
            LocalSaveStatus = ExtractionLootSaveStatus.PersistenceFailed;
            return;
        }

        ProfileId localProfileId = context.Store.ProfileId;

        LootDefinitionCatalog catalog = _lootReceiver.LootCatalog;
        var items = new List<StashItem>(_pendingCatalogIndices.Length);
        for (int i = 0; i < _pendingCatalogIndices.Length; i++)
        {
            if (!catalog.TryGetByIndex(_pendingCatalogIndices[i], out LootDefinition definition))
            {
                LocalSaveStatus = ExtractionLootSaveStatus.PersistenceFailed;
                return;
            }

            items.Add(new StashItem(definition.LootId, _pendingAmounts[i]));
        }

        ExtractionReceipt receipt = new ExtractionReceipt(
            _participant.RaidGenerationId.ToString(),
            localProfileId,
            _pendingResultSequence);
        StashOperationResult result = context.Store.TryCommitExtraction(receipt, items);
        if (result != StashOperationResult.Success && result != StashOperationResult.AlreadySecured)
        {
            Debug.LogError(
                $"{nameof(PlayerExtractionLootSaver)}: Local extraction commit failed with {result}. " +
                $"Persistence={context.PersistenceStatus}; Error={context.PersistenceError ?? "none"}.",
                this);
            LocalSaveStatus = ExtractionLootSaveStatus.PersistenceFailed;
            return;
        }

        LocalSaveStatus = ExtractionLootSaveStatus.Committed;
        RPC_AcknowledgeExtractionCommit(_pendingResultSequence);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_AcknowledgeExtractionCommit(int resultSequence)
    {
        if (!HasStateAuthority || !TryResolvePendingParticipant() ||
            _participant.State != RaidParticipantState.Extracted ||
            _participant.IsExtractionCommitConfirmed ||
            resultSequence != _participant.ResultSequence ||
            resultSequence != _pendingResultSequence ||
            !HasValidPendingPayload())
        {
            return;
        }

        string clearError = null;
        if (_pendingOwnershipSnapshot == null ||
            !_pendingOwnershipSnapshot.TryClearExact(_lootReceiver, _weaponEquipment, out clearError))
        {
            Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Extraction inventory changed before ACK: {clearError}.", this);
            return;
        }

        if (!_participant.TryConfirmExtractionCommit(resultSequence))
        {
            Debug.LogError($"{nameof(PlayerExtractionLootSaver)}: Could not confirm extraction result.", this);
            return;
        }

        TryApplyPendingExtractedLootExperience(resultSequence);
        ClearPendingTransaction();
    }

    private bool ValidateIncomingPayload(int[] catalogIndices, int[] amounts)
    {
        if (catalogIndices == null || amounts == null ||
            catalogIndices.Length != amounts.Length ||
            catalogIndices.Length > MaximumSnapshotEntries ||
            _lootReceiver.LootCatalog == null)
        {
            return false;
        }

        if (TryResolvePendingParticipant() && _participant.IsExtractionCommitConfirmed)
        {
            return false;
        }

        return ValidatePayloadShape(
            catalogIndices,
            amounts,
            MaximumSnapshotEntries,
            PlayerLootReceiver.MaxCatalogEntries,
            index => _lootReceiver.LootCatalog.TryGetByIndex(index, out _));
    }

    /// <summary>
    /// Validates the complete wire payload before any local commit begins.
    /// </summary>
    internal static bool ValidatePayloadShape(
        int[] catalogIndices,
        int[] amounts,
        int maximumEntries,
        int maximumCatalogEntries,
        Func<int, bool> isKnownIndex)
    {
        if (catalogIndices == null || amounts == null ||
            catalogIndices.Length != amounts.Length ||
            catalogIndices.Length > maximumEntries || maximumCatalogEntries <= 0 || isKnownIndex == null)
        {
            return false;
        }

        for (int i = 0; i < catalogIndices.Length; i++)
        {
            if (catalogIndices[i] < 0 || catalogIndices[i] >= maximumCatalogEntries ||
                amounts[i] <= 0 || !isKnownIndex(catalogIndices[i]))
            {
                return false;
            }

            for (int previous = 0; previous < i; previous++)
            {
                if (catalogIndices[previous] == catalogIndices[i])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool HasValidPendingPayload()
    {
        return _pendingSnapshot != null && _pendingCatalogIndices != null &&
            _pendingAmounts != null && _pendingCatalogIndices.Length == _pendingAmounts.Length &&
            _pendingCatalogIndices.Length == _pendingSnapshot.Count &&
            _pendingResultSequence > 0;
    }

    private IReadOnlyList<LootEntry> BuildSnapshot(int[] catalogIndices, int[] amounts)
    {
        LootDefinitionCatalog catalog = _lootReceiver.LootCatalog;
        var snapshot = new List<LootEntry>(catalogIndices.Length);
        for (int i = 0; i < catalogIndices.Length; i++)
        {
            if (!catalog.TryGetByIndex(catalogIndices[i], out LootDefinition definition))
            {
                return null;
            }

            snapshot.Add(new LootEntry(definition.LootId, amounts[i]));
        }

        return snapshot.AsReadOnly();
    }

    private bool HasPendingTransaction()
    {
        return HasValidPendingPayload();
    }

    private void TryApplyPendingExtractedLootExperience(int resultSequence)
    {
        if (!_pendingExperienceCandidate.HasValue)
        {
            return;
        }

        ExtractedLootExperienceCandidate candidate = _pendingExperienceCandidate.Value;
        if (!CandidateMatchesPendingTransaction(
                candidate,
                resultSequence,
                _pendingResultSequence,
                _participant.ResultSequence))
        {
            Debug.LogError(
                $"{nameof(PlayerExtractionLootSaver)}: Extracted Loot Experience candidate does not " +
                "belong to the confirmed pending transaction. The reward will be discarded.",
                this);
            return;
        }

        if (_experienceProducer == null)
        {
            Debug.LogError(
                $"{nameof(PlayerExtractionLootSaver)}: Confirmed extraction has no " +
                $"{nameof(ExtractedLootExperienceProducer)}. The extraction remains confirmed.",
                this);
            return;
        }

        if (!_experienceProducer.TryApplyConfirmed(
                _participant,
                candidate,
                out ExpeditionExperienceLedgerFailure failure))
        {
            Debug.LogError(
                $"{nameof(PlayerExtractionLootSaver)}: Confirmed extraction could not credit " +
                $"Extracted Loot Experience ({failure}). The extraction remains confirmed.",
                this);
        }
    }

    internal static bool CandidateMatchesPendingTransaction(
        in ExtractedLootExperienceCandidate candidate,
        int acknowledgedResultSequence,
        int pendingResultSequence,
        int participantResultSequence) =>
        candidate.Matches(acknowledgedResultSequence) &&
        acknowledgedResultSequence == pendingResultSequence &&
        acknowledgedResultSequence == participantResultSequence;

    private void ClearPendingTransaction()
    {
        _pendingSnapshot = null;
        _pendingOwnershipSnapshot = null;
        _pendingCatalogIndices = null;
        _pendingAmounts = null;
        _pendingResultSequence = 0;
        _pendingExperienceCandidate = null;
        RetryTimer = TickTimer.None;
    }

    private bool TryResolvePendingParticipant()
    {
        if (_participant != null)
        {
            return true;
        }

        return _participantLink != null && _participantLink.TryResolveParticipant(out _participant);
    }
}
