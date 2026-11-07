using System;
using System.Collections.Generic;
using UnityEngine;
using static ProfileInventoryRules;

/// <summary>
/// Atomic application-level operations over the local profile aggregate.
/// </summary>
public sealed class LocalProfileStore : IMissionClaimService
{
    private readonly object _sync = new();
    private readonly ILocalProfileRepository _repository;
    private readonly ProfileId _profileId;
    private readonly LootDefinitionCatalog _lootCatalog;
    private readonly LootId _recoveryWeaponLootId;
    private readonly MissionDefinitionCatalog _missionCatalog;
    private readonly AbilityDefinitionCatalog _abilityCatalog;

    public event Action<ProfileId> ProfileCommitted;
    public event Action<ProfileId, ExperienceApplicationResult> ProgressionCommitted;
    public event Action<MissionId, MissionState> MissionProgressChanged;

    public ProfileId ProfileId => _profileId;
    public LocalProfilePersistenceStatus Status => _repository.Status;
    public string LastError => _repository.LastError;
    public bool IsAvailable => Status == LocalProfilePersistenceStatus.Ready || Status == LocalProfilePersistenceStatus.RecoveredFromBackup;
    public MissionDefinitionCatalog MissionCatalog => _missionCatalog;
    public LootDefinitionCatalog LootCatalog => _lootCatalog;
    public const int DefaultStashCapacity = 60;
    public int StashCapacity { get; }

    public LocalProfileStore(
        ILocalProfileRepository repository,
        ProfileId profileId,
        LootDefinitionCatalog lootCatalog = null,
        LootId recoveryWeaponLootId = default,
        MissionDefinitionCatalog missionCatalog = null,
        AbilityDefinitionCatalog abilityCatalog = null,
        int stashCapacity = DefaultStashCapacity)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        if (stashCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(stashCapacity));
        StashCapacity = stashCapacity;
        _profileId = profileId;
        _lootCatalog = lootCatalog;
        _recoveryWeaponLootId = recoveryWeaponLootId;
        _missionCatalog = missionCatalog;
        _abilityCatalog = abilityCatalog;
    }

    public IReadOnlyList<StashItem> GetStash() =>
        _repository.Snapshot != null ? _repository.Snapshot.Stash : Array.Empty<StashItem>();

    public IReadOnlyList<StashItem> GetLoadout() =>
        _repository.Snapshot != null ? _repository.Snapshot.Loadout : Array.Empty<StashItem>();
    public PreparedEquipmentLoadout GetPreparedEquipment() =>
        _repository.Snapshot != null ? _repository.Snapshot.PreparedEquipment : default;
    public PendingLoadoutReservation PendingReservation => 
        _repository.Snapshot?.PendingReservation;
    public PendingExtractionCommit PendingExtractionCommit => 
        _repository.Snapshot?.PendingExtractionCommit;
    /// <summary>
    /// Remains true after the pending backend commit is acknowledged and cleared.
    /// Departure cleanup must match this participation, not a previous Raid's receipt.
    /// </summary>
    public bool HasCommittedExtraction(string raidId)
    {
        if (string.IsNullOrEmpty(raidId) || _repository.Snapshot == null) return false;
        foreach (ExtractionReceipt receipt in _repository.Snapshot.AppliedExtractionReceipts)
        {
            if (receipt.ProfileId == _profileId && string.Equals(receipt.RaidId, raidId, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    public long GetCurrency() =>
        _repository.Snapshot != null ? _repository.Snapshot.Currency : LocalProfileSnapshot.InitialCurrency;
    public int GetLevel() =>
        _repository.Snapshot != null ? _repository.Snapshot.Level : ExperienceCurve.InitialLevel;
    public long GetCurrentExperience() =>
        _repository.Snapshot != null ? _repository.Snapshot.CurrentExperience : 0L;
    public int GetLastAppliedProgressionResultSequence() =>
        _repository.Snapshot != null ? _repository.Snapshot.LastAppliedProgressionResultSequence : 0;
    public int RemoteRevision =>
        _repository.Snapshot != null ? _repository.Snapshot.RemoteRevision : 0;
    public int InventorySchemaVersion => _repository.Snapshot?.InventorySchemaVersion ?? 1;
    public bool RequiresEquipmentHydration => _repository.Snapshot?.RequiresEquipmentHydration ?? true;

    public IReadOnlyList<MissionInstanceState> GetActiveMissions() =>
        _repository.Snapshot != null ? _repository.Snapshot.ActiveMissions : Array.Empty<MissionInstanceState>();

    public PreparedAbilityLoadout GetPreparedAbilities()
    {
        lock (_sync)
        {
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            return IsAvailable && snapshot != null && snapshot.ProfileId == _profileId
                ? snapshot.PreparedAbilities
                : default;
        }
    }

    public QuickSlotBindings GetQuickSlots()
    {
        lock (_sync)
        {
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            return IsAvailable && snapshot != null && snapshot.ProfileId == _profileId
                ? snapshot.QuickSlots
                : default;
        }
    }

    /// <summary>
    /// Commits Quick Slot bindings. Bindings only reference loot definitions, so this never
    /// touches Stash, Loadout, Origin or amounts and, unlike equipment, is not blocked by
    /// Ready or a pending reservation. Whether the definition is present in the Inventory the
    /// player is looking at is checked by the caller, because the Loadout is reserved or
    /// empty while in Raid.
    /// </summary>
    public StashOperationResult TrySetQuickSlots(QuickSlotBindings bindings)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return StashOperationResult.PersistenceFailed;
            }

            for (int i = 0; i < QuickSlotBindings.SlotCount; i++)
            {
                if (bindings.IsAssigned(i) &&
                    (_lootCatalog == null ||
                     !_lootCatalog.TryGet(bindings.Get(i).Value, out LootDefinition definition) ||
                     !definition.QuickSlotCompatible))
                {
                    return StashOperationResult.InvalidInventory;
                }
            }

            if (current.QuickSlots == bindings)
            {
                return StashOperationResult.Success;
            }

            LocalProfileSnapshot next = current.Clone();
            next.QuickSlots = bindings;
            return Commit(next);
        }
    }

    /// <summary>
    /// Captures the confirmed attributes and prepared abilities for Raid admission in one
    /// aggregate read. The unlocked repertoire remains local and is used only to revalidate the
    /// two transported identities before they cross the Town-to-Raid boundary.
    /// </summary>
    public bool TryGetRaidAdmissionAbilitySnapshot(
        out CharacterAttributeState characterAttributes,
        out PreparedAbilityLoadout preparedAbilities,
        out string error)
    {
        lock (_sync)
        {
            characterAttributes = default;
            preparedAbilities = default;
            error = null;

            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                error = "The local profile is unavailable for Raid admission.";
                return false;
            }

            if (!PreparedAbilityLoadout.TryValidate(
                    snapshot.PreparedAbilities,
                    snapshot.UnlockedAbilities,
                    snapshot.CharacterAttributes,
                    _abilityCatalog,
                    out error))
            {
                return false;
            }

            characterAttributes = snapshot.CharacterAttributes;
            preparedAbilities = snapshot.PreparedAbilities;
            return true;
        }
    }

    public IReadOnlyList<AbilityId> GetUnlockedAbilities()
    {
        lock (_sync)
        {
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                return Array.Empty<AbilityId>();
            }

            var result = new AbilityId[snapshot.UnlockedAbilities.Count];
            snapshot.UnlockedAbilities.CopyTo(result);
            return result;
        }
    }

    public bool IsAbilityUnlocked(AbilityId abilityId)
    {
        if (!abilityId.IsValid)
        {
            return false;
        }

        lock (_sync)
        {
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                return false;
            }

            return snapshot.UnlockedAbilities.Contains(abilityId);
        }
    }

    public AbilityUnlockResult TryUnlockAbility(AbilityId abilityId)
    {
        if (!abilityId.IsValid)
        {
            return AbilityUnlockResult.InvalidAbility;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityUnlockResult.ProfileUnavailable;
            }

            AbilityUnlockResult validation = ValidateAbilityUnlock(current, abilityId);
            if (validation != AbilityUnlockResult.Success)
            {
                return validation;
            }

            LocalProfileSnapshot next = current.Clone();
            next.UnlockedAbilities.Add(abilityId);
            return Commit(next) == StashOperationResult.Success
                ? AbilityUnlockResult.Success
                : AbilityUnlockResult.PersistenceFailed;
        }
    }

    /// <summary>
    /// Evaluates whether an ability purchase is valid against current local profile state.
    /// Does not mutate state or commit.
    /// </summary>
    public AbilityPurchaseResult EvaluateAbilityPurchase(AbilityId abilityId, long price)
    {
        if (!abilityId.IsValid)
        {
            return AbilityPurchaseResult.InvalidAbility;
        }

        if (price <= 0)
        {
            return AbilityPurchaseResult.InvalidPrice;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityPurchaseResult.ProfileUnavailable;
            }

            if (current.PendingReservation != null)
            {
                return AbilityPurchaseResult.ReservationPending;
            }

            switch (ValidateAbilityUnlock(current, abilityId))
            {
                case AbilityUnlockResult.UnknownAbility:
                    return AbilityPurchaseResult.UnknownAbility;
                case AbilityUnlockResult.AlreadyUnlocked:
                    return AbilityPurchaseResult.AlreadyUnlocked;
            }

            if (current.Currency < price)
            {
                return AbilityPurchaseResult.InsufficientCurrency;
            }

            return AbilityPurchaseResult.Success;
        }
    }

    /// <summary>
    /// Applies a confirmed authoritative ability purchase from the backend.
    /// Sets new balance (or debits price), adds ability to UnlockedAbilities, and updates remote revision.
    /// </summary>
    public AbilityPurchaseResult ApplyConfirmedAbilityPurchase(AbilityId abilityId, long price, int remoteRevision, long? newBalance = null)
    {
        if (!abilityId.IsValid)
        {
            return AbilityPurchaseResult.InvalidAbility;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityPurchaseResult.ProfileUnavailable;
            }

            LocalProfileSnapshot next = current.Clone();
            next.Currency = newBalance ?? (next.Currency - price);
            if (!next.UnlockedAbilities.Contains(abilityId))
            {
                next.UnlockedAbilities.Add(abilityId);
            }
            next.RemoteRevision = remoteRevision;
            return Commit(next) == StashOperationResult.Success
                ? AbilityPurchaseResult.Success
                : AbilityPurchaseResult.PersistenceFailed;
        }
    }

    /// <summary>
    /// Debits <paramref name="price"/> Currency and unlocks the ability in one transaction.
    /// Attribute requirements do not condition acquisition; an owned ability cannot be bought again.
    /// </summary>
    public AbilityPurchaseResult TryPurchaseAbility(AbilityId abilityId, long price)
    {
        var eval = EvaluateAbilityPurchase(abilityId, price);
        if (eval != AbilityPurchaseResult.Success)
        {
            return eval;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            LocalProfileSnapshot next = current.Clone();
            next.Currency -= price;
            next.UnlockedAbilities.Add(abilityId);
            return Commit(next) == StashOperationResult.Success
                ? AbilityPurchaseResult.Success
                : AbilityPurchaseResult.PersistenceFailed;
        }
    }

    private AbilityUnlockResult ValidateAbilityUnlock(LocalProfileSnapshot candidate, AbilityId abilityId)
    {
        if (!abilityId.IsValid)
        {
            return AbilityUnlockResult.InvalidAbility;
        }

        if (_abilityCatalog == null || !_abilityCatalog.TryGet(abilityId, out _))
        {
            return AbilityUnlockResult.UnknownAbility;
        }

        return candidate.UnlockedAbilities.Contains(abilityId)
            ? AbilityUnlockResult.AlreadyUnlocked
            : AbilityUnlockResult.Success;
    }

    public AbilityPreparationResult TrySetPreparedAbility(
        UniversalAbilitySlot slot,
        AbilityId abilityId)
    {
        if (!PreparedAbilityLoadout.IsKnownSlot(slot))
        {
            return AbilityPreparationResult.InvalidSlot;
        }

        if (!abilityId.IsValid)
        {
            return AbilityPreparationResult.InvalidAbility;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityPreparationResult.ProfileUnavailable;
            }

            if (_abilityCatalog == null || !_abilityCatalog.TryGet(abilityId, out AbilityDefinition definition))
            {
                return AbilityPreparationResult.UnknownAbility;
            }

            if (!current.UnlockedAbilities.Contains(abilityId))
            {
                return AbilityPreparationResult.AbilityNotUnlocked;
            }

            UniversalAbilitySlot otherSlot = slot == UniversalAbilitySlot.Slot1
                ? UniversalAbilitySlot.Slot2
                : UniversalAbilitySlot.Slot1;
            if (current.PreparedAbilities.Get(otherSlot) == abilityId)
            {
                return AbilityPreparationResult.DuplicateAbility;
            }

            if (!definition.AreAttributeRequirementsSatisfiedBy(current.CharacterAttributes))
            {
                return AbilityPreparationResult.AttributeRequirementsNotMet;
            }

            PreparedAbilityLoadout candidate = current.PreparedAbilities.With(slot, abilityId);
            if (!PreparedAbilityLoadout.TryValidate(
                    candidate,
                    current.UnlockedAbilities,
                    current.CharacterAttributes,
                    _abilityCatalog,
                    out _))
            {
                return AbilityPreparationResult.InvalidPreparedState;
            }

            LocalProfileSnapshot next = current.Clone();
            next.PreparedAbilities = candidate;
            return Commit(next) == StashOperationResult.Success
                ? AbilityPreparationResult.Success
                : AbilityPreparationResult.PersistenceFailed;
        }
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Developer/test tooling only: clears the whole unlocked repertoire and both prepared slots in one
    /// transaction. Gameplay acquisition stays permanent; no production flow calls this.
    /// </summary>
    public bool TryResetAbilityRepertoire()
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return false;
            }

            if (current.UnlockedAbilities.Count == 0 && !current.PreparedAbilities.Slot1.IsValid
                && !current.PreparedAbilities.Slot2.IsValid)
            {
                return false;
            }

            LocalProfileSnapshot next = current.Clone();
            next.UnlockedAbilities.Clear();
            next.PreparedAbilities = default;
            return Commit(next) == StashOperationResult.Success;
        }
    }
#endif

    public AbilityPreparationResult TryClearPreparedAbility(UniversalAbilitySlot slot)
    {
        if (!PreparedAbilityLoadout.IsKnownSlot(slot))
        {
            return AbilityPreparationResult.InvalidSlot;
        }

        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return AbilityPreparationResult.ProfileUnavailable;
            }

            if (!current.PreparedAbilities.Get(slot).IsValid)
            {
                return AbilityPreparationResult.Success;
            }

            PreparedAbilityLoadout candidate = current.PreparedAbilities.Without(slot);
            if (!PreparedAbilityLoadout.TryValidate(
                    candidate,
                    current.UnlockedAbilities,
                    current.CharacterAttributes,
                    _abilityCatalog,
                    out _))
            {
                return AbilityPreparationResult.InvalidPreparedState;
            }

            LocalProfileSnapshot next = current.Clone();
            next.PreparedAbilities = candidate;
            return Commit(next) == StashOperationResult.Success
                ? AbilityPreparationResult.Success
                : AbilityPreparationResult.PersistenceFailed;
        }
    }
    public int GuildReputation => _repository.Snapshot?.GuildReputation ?? 0;
    public MissionRank GuildRank => _repository.Snapshot?.GuildRank ?? MissionRank.E;

    public StashOperationResult TryAcceptMission(MissionDefinition mission)
    {
        if (mission == null || !mission.MissionId.IsValid) return StashOperationResult.InvalidInventory;
        var current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;

        int abandonedIndex = -1;
        for (int i = 0; i < current.ActiveMissions.Count; i++)
        {
            var active = current.ActiveMissions[i];
            if (active.MissionId == mission.MissionId)
            {
                if (active.State == MissionState.Activa || active.State == MissionState.PendienteDeReclamar)
                    return StashOperationResult.AlreadyApplied;

                if (active.State == MissionState.Reclamada)
                    return StashOperationResult.AlreadyApplied;

                if (active.State == MissionState.Abandonada)
                {
                    abandonedIndex = i;
                }
            }
        }

        if (mission.RankRequired > current.GuildRank)
        {
            Debug.LogWarning($"[MissionBoard] Rango insuficiente para aceptar '{mission.Title}'. Requerido: {mission.RankRequired}, Actual: {current.GuildRank}.");
            return StashOperationResult.InvalidInventory;
        }

        if (_missionCatalog != null)
        {
            var activeTypes = new List<MissionType>();
            foreach (var active in current.ActiveMissions)
            {
                if (active.State == MissionState.Reclamada || active.State == MissionState.Abandonada)
                    continue;

                if (_missionCatalog.TryGet(active.MissionId.Value, out var activeDef))
                    activeTypes.Add(activeDef.Type);
            }

            if (!MissionLifecycleRules.CanAcceptMission(mission.Type, activeTypes))
                return StashOperationResult.PersistenceFailed;
        }

        var next = current.Clone();
        if (abandonedIndex >= 0)
        {
            next.ActiveMissions[abandonedIndex] = new MissionInstanceState(mission.MissionId, MissionState.Activa);
        }
        else
        {
            next.ActiveMissions.Add(new MissionInstanceState(mission.MissionId, MissionState.Activa));
        }
        return Commit(next);
    }

    public StashOperationResult TryAbandonMission(MissionId missionId)
    {
        if (!missionId.IsValid) return StashOperationResult.InvalidInventory;
        var current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;

        int index = current.ActiveMissions.FindIndex(m => m.MissionId == missionId);
        if (index < 0) return StashOperationResult.InvalidInventory;

        var instance = current.ActiveMissions[index];
        if (!MissionLifecycleRules.CanTransitionTo(instance.State, MissionState.Abandonada))
            return StashOperationResult.PersistenceFailed;

        var next = current.Clone();
        next.ActiveMissions[index].State = MissionState.Abandonada;
        return Commit(next);
    }

    public bool CanClaimMission(MissionDefinition mission, out string failureReason)
    {
        lock (_sync)
        {
            failureReason = null;
            if (mission == null || !mission.MissionId.IsValid)
            {
                failureReason = "Misión inválida.";
                return false;
            }

            var snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null)
            {
                failureReason = "Perfil no disponible.";
                return false;
            }

            int index = snapshot.ActiveMissions.FindIndex(m => m.MissionId == mission.MissionId);
            if (index < 0)
            {
                failureReason = "La misión no está activa en el perfil.";
                return false;
            }

            var instance = snapshot.ActiveMissions[index];
            if (instance.State != MissionState.PendienteDeReclamar)
            {
                failureReason = $"La misión no está en estado pendiente de reclamación (Estado actual: {instance.State}).";
                return false;
            }

            // Check required delivery items from personal inventory (Loadout)
            var requiredItems = GetRequiredDeliveryItems(mission);
            foreach (var kvp in requiredItems)
            {
                int inLoadout = FindAmount(snapshot.Loadout, kvp.Key);
                if (inLoadout < kvp.Value)
                {
                    int inStash = FindAmount(snapshot.Stash, kvp.Key);
                    if (inStash >= kvp.Value)
                    {
                        failureReason = $"Los materiales requeridos ({kvp.Key.Value}) están en el Alijo. Debes transferirlos a tu inventario personal para entregarlos al NPC.";
                    }
                    else
                    {
                        failureReason = $"Materiales insuficientes en inventario personal para {kvp.Key.Value} (Requerido: {kvp.Value}, En inventario: {inLoadout}).";
                    }
                    return false;
                }
            }

            // Check stash capacity for item/equipment rewards
            if (mission.Rewards != null)
            {
                var itemsToAdd = new List<StashItem>();
                foreach (var reward in mission.Rewards)
                {
                    if (reward.Type == RewardDefinition.RewardType.Item || reward.Type == RewardDefinition.RewardType.Equipment)
                    {
                        var lootId = new LootId(reward.ReferenceId);
                        if (lootId.IsValid && reward.Amount > 0)
                        {
                            itemsToAdd.Add(new StashItem(lootId, reward.Amount));
                        }
                    }
                }

                if (itemsToAdd.Count > 0 && !CanAdmitStashItems(snapshot.Stash, itemsToAdd))
                {
                    failureReason = "Espacio insuficiente en el Alijo para recibir las recompensas de items.";
                    return false;
                }
            }

            return true;
        }
    }

    private static Dictionary<LootId, int> GetRequiredDeliveryItems(MissionDefinition mission)
    {
        var required = new Dictionary<LootId, int>();
        if (mission == null || mission.Phases == null) return required;

        foreach (var phase in mission.Phases)
        {
            if (phase.Objectives == null) continue;
            foreach (var obj in phase.Objectives)
            {
                if ((obj.Family == ObjectiveFamily.Obtencion || obj.Family == ObjectiveFamily.ExtraccionDeObjetos) &&
                    !string.IsNullOrEmpty(obj.Condition.TargetId) &&
                    obj.Condition.RequiredAmount > 0)
                {
                    var lootId = new LootId(obj.Condition.TargetId);
                    if (lootId.IsValid)
                    {
                        required.TryGetValue(lootId, out int existing);
                        required[lootId] = checked(existing + obj.Condition.RequiredAmount);
                    }
                }

            }
        }
        return required;
    }

    private void SynchronizeActiveResourceMissions(LocalProfileSnapshot snapshot)
    {
        if (snapshot == null || _missionCatalog == null) return;

        for (int i = 0; i < snapshot.ActiveMissions.Count; i++)
        {
            var instance = snapshot.ActiveMissions[i];
            if (instance.State != MissionState.Activa &&
                instance.State != MissionState.PendienteDeReclamar &&
                instance.State != MissionState.Completada)
            {
                continue;
            }

            if (!_missionCatalog.TryGet(instance.MissionId.Value, out var definition))
            {
                continue;
            }

            MissionProgressEngine.EvaluateDynamicPossession(instance, definition, snapshot.Loadout);
        }
    }

    public void SynchronizeResourceMissions()
    {
        lock (_sync)
        {
            var current = _repository.Snapshot;
            if (!IsAvailable || current == null) return;
            var next = current.Clone();
            SynchronizeActiveResourceMissions(next);
            Commit(next);
        }
    }

    public StashOperationResult TryClaimMission(MissionDefinition mission)
    {
        lock (_sync)
        {
            if (!CanClaimMission(mission, out string failureReason))
            {
                Debug.LogWarning($"[MissionBoard] No se puede reclamar '{mission?.Title}': {failureReason}");
                if (failureReason != null && failureReason.Contains("Espacio insuficiente en el Alijo"))
                {
                    return StashOperationResult.InventoryFull;
                }
                return StashOperationResult.InvalidInventory;
            }

            var current = _repository.Snapshot;
            var next = current.Clone();

            int index = next.ActiveMissions.FindIndex(m => m.MissionId == mission.MissionId);
            var instance = next.ActiveMissions[index];

            // Atomic deduction of delivery items from personal inventory (next.Loadout)
            var requiredItems = GetRequiredDeliveryItems(mission);
            foreach (var kvp in requiredItems)
            {
                if (!TryRemove(next.Loadout, kvp.Key, kvp.Value))
                {
                    Debug.LogError($"[MissionBoard] Fallo inesperado al deducir {kvp.Value}x {kvp.Key.Value} del inventario personal.");
                    return StashOperationResult.InvalidInventory;
                }
            }

            if (mission.Rewards != null)
            {
                var itemsToAdd = new List<StashItem>();
                foreach (var reward in mission.Rewards)
                {
                    if (reward.Type == RewardDefinition.RewardType.Gold)
                    {
                        if (next.Currency > long.MaxValue - reward.Amount) return StashOperationResult.InvalidInventory;
                        next.Currency += reward.Amount;
                        Debug.Log($"[MissionBoard] Recompensa reclamada: {reward.Amount} Oro. (Total: {next.Currency})");
                    }
                    else if (reward.Type == RewardDefinition.RewardType.GuildReputation)
                    {
                        if (reward.Amount > 0)
                        {
                            next.GuildReputation += reward.Amount;
                            Debug.Log($"[MissionBoard] Recompensa reclamada: {reward.Amount} Reputación. (Total: {next.GuildReputation}, Rango: {next.GuildRank})");
                        }
                    }
                    else if (reward.Type == RewardDefinition.RewardType.Experience)
                    {
                        if (CharacterProgressionRules.TryApplyExperience(
                                ProgressionBalanceDefaults.InitialExperienceCurve,
                                next.Level,
                                next.CurrentExperience,
                                reward.Amount,
                                out ExperienceApplicationResult expResult))
                        {
                            next.Level = expResult.ResultingLevel;
                            next.CurrentExperience = expResult.ResultingExperience;
                            
                            next.LastAppliedProgressionResultSequence++;
                            var receipt = new ProgressionReceipt(
                                "mission-claim",
                                _profileId,
                                next.LastAppliedProgressionResultSequence,
                                next.CurrentExperience,
                                next.Level);
                                
                            next.LastProgressionReceipt = receipt;
                            next.AppliedProgressionReceipts.Add(receipt);
                            while (next.AppliedProgressionReceipts.Count > LocalProfileSnapshot.MaxAppliedProgressionReceipts)
                            {
                                next.AppliedProgressionReceipts.RemoveAt(0);
                            }

                            Debug.Log($"[MissionBoard] Recompensa reclamada: {reward.Amount} XP. (Nivel: {next.Level}, XP: {next.CurrentExperience})");
                        }
                        else
                        {
                            Debug.LogWarning($"[MissionBoard] No se pudo aplicar la experiencia de recompensa ({reward.Amount} XP).");
                        }
                    }
                    else if (reward.Type == RewardDefinition.RewardType.Item || reward.Type == RewardDefinition.RewardType.Equipment)
                    {
                        var lootId = new LootId(reward.ReferenceId);
                        if (lootId.IsValid && reward.Amount > 0)
                        {
                            itemsToAdd.Add(new StashItem(lootId, reward.Amount));
                            Debug.Log($"[MissionBoard] Recompensa reclamada: {reward.Amount}x {reward.ReferenceId} para el Stash.");
                        }
                    }
                    else if (reward.Type == RewardDefinition.RewardType.Ability)
                    {
                        AbilityId.TryCreate(reward.ReferenceId, out AbilityId abilityId);
                        AbilityUnlockResult unlock = ValidateAbilityUnlock(next, abilityId);
                        if (unlock == AbilityUnlockResult.Success)
                        {
                            next.UnlockedAbilities.Add(abilityId);
                        }
                        else if (unlock != AbilityUnlockResult.AlreadyUnlocked)
                        {
                            Debug.LogWarning($"[MissionBoard] Recompensa de habilidad inválida '{reward.ReferenceId}' ({unlock}); la reclamación se rechaza.");
                            return StashOperationResult.InvalidInventory;
                        }
                    }
                }

                if (itemsToAdd.Count > 0)
                {
                    if (!CanAdmitStashItems(current.Stash, itemsToAdd)) return StashOperationResult.InventoryFull;
                    if (!TryMerge(next.Stash, itemsToAdd, StashCapacity))
                        return StashOperationResult.PersistenceFailed;
                    Debug.Log($"[MissionBoard] {itemsToAdd.Count} items de recompensa añadidos al Stash local exitosamente.");
                }
            }

            next.ActiveMissions[index].State = MissionState.Reclamada;

            // Recalculate other active missions against the new Loadout state
            SynchronizeActiveResourceMissions(next);

            var result = Commit(next);
            if (result == StashOperationResult.Success)
            {
                Debug.Log($"[MissionBoard] Misión '{mission.Title}' reclamada y persistida correctamente en disco.");
            }
            return result;
        }
    }

    public StashOperationResult TryApplyMissionProgress(MissionContributionEvent contribution)
    {
        var current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;
        if (_missionCatalog == null) return StashOperationResult.PersistenceFailed;

        var next = current.Clone();
        bool changed = false;
        List<(MissionId id, MissionState state)> changedMissions = null;

        for (int i = 0; i < next.ActiveMissions.Count; i++)
        {
            var instance = next.ActiveMissions[i];
            if (instance.State != MissionState.Activa) continue;

            if (_missionCatalog.TryGet(instance.MissionId.Value, out var definition))
            {
                if (MissionProgressEngine.TryApplyProgress(instance, definition, contribution))
                {
                    changed = true;
                    if (changedMissions == null) changedMissions = new List<(MissionId, MissionState)>();
                    changedMissions.Add((instance.MissionId, instance.State));
                }
            }
        }

        if (changed)
        {
            var commitResult = Commit(next);
            if (commitResult == StashOperationResult.Success && changedMissions != null)
            {
                for (int m = 0; m < changedMissions.Count; m++)
                {
                    MissionProgressChanged?.Invoke(changedMissions[m].id, changedMissions[m].state);
                }
            }
            return commitResult;
        }

        return StashOperationResult.Success;
    }

    public bool TryGetCharacterAttributeState(out CharacterAttributeState state)
    {
        lock (_sync)
        {
            state = default;
            LocalProfileSnapshot snapshot = _repository.Snapshot;
            if (!IsAvailable || snapshot == null || snapshot.ProfileId != _profileId)
            {
                return false;
            }

            state = snapshot.CharacterAttributes;
            return true;
        }
    }

    public CharacterAttributeAssignmentCommitResult TryAssignCharacterAttribute(
        CharacterAttribute attribute,
        out CharacterAttributeAssignmentFailure failure)
    {
        lock (_sync)
        {
            failure = CharacterAttributeAssignmentFailure.None;
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
            {
                return CharacterAttributeAssignmentCommitResult.Unavailable;
            }

            if (!CharacterAttributeAssignmentRules.TryAssign(
                    ProgressionBalanceDefaults.InitialMaximumAttributeValue,
                    current.CharacterAttributes,
                    attribute,
                    out CharacterAttributeState candidate,
                    out failure))
            {
                return CharacterAttributeAssignmentCommitResult.Rejected;
            }

            if (!TryRevalidatePreparedAbilities(
                    current,
                    candidate,
                    out PreparedAbilityLoadout revalidatedAbilities,
                    out string abilityError))
            {
                Debug.LogError($"[LocalProfileStore] Attribute assignment found invalid prepared abilities: {abilityError}");
                return CharacterAttributeAssignmentCommitResult.PersistenceFailed;
            }

            LocalProfileSnapshot next = current.Clone();
            next.CharacterAttributes = candidate;
            next.PreparedAbilities = revalidatedAbilities;
            return Commit(next) == StashOperationResult.Success
                ? CharacterAttributeAssignmentCommitResult.Success
                : CharacterAttributeAssignmentCommitResult.PersistenceFailed;
        }
    }

    /// <summary>Applies the gem endpoint's authoritative lists through the existing hydration transaction.</summary>
    public StashOperationResult ApplyConfirmedGemInstall(Grimhold.Backend.MoveItemResult result, int expectedRevision)
    {
        lock (_sync)
        {
            var current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.RemoteRevision != expectedRevision ||
                current.InventorySchemaVersion != 3 || current.RequiresEquipmentHydration ||
                current.PendingReservation != null || current.PendingExtractionCommit != null ||
                result.stash == null || result.loadout == null || result.revision <= expectedRevision)
                return StashOperationResult.InvalidInventory;
            // The endpoint changes only these lists and revision. Retain the aggregate's other fields.
            return ReconcileRemoteState(new Grimhold.Backend.InventoryData
            {
                inventorySchemaVersion = 3, stash = result.stash, loadout = result.loadout,
                revision = result.revision, currency = current.Currency,
                preparedEquipment = Grimhold.Backend.PreparedEquipmentData.FromLoadout(current.PreparedEquipment)
            }, null, _lootCatalog, _abilityCatalog);
        }
    }

    public void SetRemoteRevision(int revision)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (IsAvailable && current != null && current.ProfileId == _profileId)
            {
                LocalProfileSnapshot next = current.Clone();
                next.RemoteRevision = revision;
                Commit(next);
            }
        }
    }

    public StashOperationResult ReconcileRemoteState(
        Grimhold.Backend.InventoryData? inventoryData,
        Grimhold.Backend.ProgressionData? progressionData,
        LootDefinitionCatalog catalog,
        AbilityDefinitionCatalog abilityCatalog)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (IsAvailable && current != null && current.ProfileId == _profileId)
            {
                LocalProfileSnapshot next = current.Clone();
                if (!ApplicationStashServiceBootstrapper.HydrateSnapshot(
                    _profileId, next, inventoryData, progressionData, catalog, abilityCatalog))
                {
                    return StashOperationResult.HydrationFailed;
                }

                // The revision is directly assigned from the backend data, bypassing
                // Math.Max to ensure it matches the server exactly even if it was rolled back or reset.
                if (inventoryData.HasValue)   next.RemoteRevision = inventoryData.Value.revision;
                if (progressionData.HasValue) next.RemoteRevision = progressionData.Value.revision;

                return Commit(next);
            }
            return StashOperationResult.PersistenceFailed;
        }
    }

    public void ForceCharacterAttributeState(CharacterAttributeState state)
    {
        lock (_sync)
        {
            LocalProfileSnapshot current = _repository.Snapshot;
            if (!IsAvailable || current == null || current.ProfileId != _profileId)
                return;

            if (!TryRevalidatePreparedAbilities(
                    current,
                    state,
                    out PreparedAbilityLoadout revalidatedAbilities,
                    out string abilityError))
            {
                Debug.LogError($"[LocalProfileStore] Forced attributes found invalid prepared abilities: {abilityError}");
                return;
            }

            LocalProfileSnapshot next = current.Clone();
            next.CharacterAttributes = state;
            next.PreparedAbilities = revalidatedAbilities;
            Commit(next);
        }
    }

    public StashOperationResult TryCreditCurrency(long amount)
    {
        if (amount <= 0) return StashOperationResult.InvalidInventory;
        if (_repository.Snapshot.Currency > long.MaxValue - amount) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        next.Currency += amount;
        return Commit(next);
    }

    public StashOperationResult TryDebitCurrency(long amount)
    {
        if (amount <= 0) return StashOperationResult.InvalidInventory;
        if (_repository.Snapshot.Currency < amount) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        next.Currency -= amount;
        return Commit(next);
    }

    public StashOperationResult TrySecureLoot(IReadOnlyList<StashItem> items)
    {
        if (!HasValidItems(items)) return StashOperationResult.InvalidInventory;
        if (!IsAvailable || _repository.Snapshot == null) return StashOperationResult.InvalidInventory;
        if (!CanAdmitStashItems(_repository.Snapshot.Stash, items)) return StashOperationResult.InventoryFull;
        var next = _repository.Snapshot.Clone();
        if (!TryMerge(next.Stash, items, StashCapacity)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    /// <summary>
    /// Applies one authority-issued trade to the Inventory (Loadout) and Currency as a single
    /// commit. Sales are checked against the confirmed Inventory, sales may fund purchases, and
    /// Currency and slot capacity are validated on the final state of every line together, the
    /// same semantics as <see cref="MerchantTradePreview"/>. Buy and sell lines of the same loot
    /// stay independent. Any failure leaves the profile unchanged and publishes nothing;
    /// a ticket whose transaction id was already persisted returns
    /// <see cref="StashOperationResult.AlreadyApplied"/> without mutating or publishing.
    /// </summary>
    /// <summary>
    /// Whether the trade with this transaction id is already persisted in the confirmed profile.
    /// A transaction at or below the idempotency watermark counts as applied.
    /// </summary>
    public bool IsTradeApplied(ProfileId profileId, ShopTransactionId transactionId)
    {
        if (!profileId.IsValid || profileId != _profileId || !transactionId.IsValid)
            return false;

        lock (_sync)
        {
            return IsShopReceiptApplied(_repository.Snapshot, new ShopTransactionReceipt(transactionId, _profileId));
        }
    }

    public StashOperationResult TryCommitTrade(ProfileId profileId, MerchantTradeTicket ticket)
    {
        lock (_sync)
        {
            StashOperationResult result = PlanTrade(profileId, ticket, out LocalProfileSnapshot next);
            return result == StashOperationResult.Success ? Commit(next) : result;
        }
    }

    /// <summary>Plans the exact same candidate as TryCommitTrade, without saving or publishing.</summary>
    public StashOperationResult EvaluateTrade(ProfileId profileId, MerchantTradeTicket ticket) =>
        PlanTrade(profileId, ticket, out _);

    private StashOperationResult PlanTrade(ProfileId profileId, MerchantTradeTicket ticket, out LocalProfileSnapshot candidate)
    {
        candidate = null;
        if (ticket == null || !profileId.IsValid || profileId != _profileId || !ticket.IsWellFormed)
            return StashOperationResult.InvalidInventory;

        lock (_sync)
        {
            var receipt = new ShopTransactionReceipt(ticket.TransactionId, _profileId);
            LocalProfileSnapshot current = _repository.Snapshot;
            // Replay receipts before the reservation gate, matching backend idempotency.
            if (IsShopReceiptApplied(current, receipt))
                return StashOperationResult.AlreadyApplied;

            StashOperationResult gate = BeginTownMutation(out current);
            if (gate != StashOperationResult.Success) return gate;

            LocalProfileSnapshot next = current.Clone();
            long saleTotal = 0;
            foreach (MerchantPricedTradeLine sale in ticket.Sales)
            {
                int saleIndex = FindIndex(next.Loadout, sale.LootId);
                if (saleIndex < 0 ||
                    !TryRemoveAt(next.Loadout, next.Loadout[saleIndex].Slot, sale.LootId, sale.Amount) ||
                    saleTotal > long.MaxValue - sale.Total)
                    return StashOperationResult.InvalidInventory;
                saleTotal += sale.Total;
            }

            long purchaseTotal = 0;
            var purchased = new List<StashItem>(ticket.Purchases.Count);
            foreach (MerchantPricedTradeLine purchase in ticket.Purchases)
            {
                if (purchaseTotal > long.MaxValue - purchase.Total)
                    return StashOperationResult.InvalidInventory;
                purchaseTotal += purchase.Total;
                purchased.Add(new StashItem(purchase.LootId, purchase.Amount));
            }

            if (next.Currency > long.MaxValue - saleTotal)
                return StashOperationResult.InvalidInventory;
            long projectedCurrency = next.Currency + saleTotal - purchaseTotal;
            if (projectedCurrency < 0 ||
                ExceedsCapacity(next.Loadout, purchased, LocalProfileSnapshot.MaxLoadoutSlots) ||
                !TryMerge(next.Loadout, purchased, LocalProfileSnapshot.MaxLoadoutSlots))
                return StashOperationResult.InvalidInventory;

            next.Currency = projectedCurrency;
            RecordShopReceipt(next, receipt);
            candidate = next;
            return StashOperationResult.Success;
        }
    }

    public StashOperationResult EvaluateBuyback(ProfileId profileId, ShopTransactionId transactionId,
        LootId lootId, int amount, long totalPrice) =>
        PlanBuyback(profileId, transactionId, lootId, amount, totalPrice, out _);

    public StashOperationResult TryApplyBuyback(ProfileId profileId, ShopTransactionId transactionId,
        LootId lootId, int amount, long totalPrice)
    {
        lock (_sync)
        {
            var result = PlanBuyback(profileId, transactionId, lootId, amount, totalPrice, out var next);
            return result == StashOperationResult.Success ? Commit(next) : result;
        }
    }

    private StashOperationResult PlanBuyback(ProfileId profileId, ShopTransactionId transactionId,
        LootId lootId, int amount, long totalPrice, out LocalProfileSnapshot next)
    {
        next = null;
        if (!profileId.IsValid || profileId != _profileId || !transactionId.IsValid ||
            !lootId.IsValid || amount < 1 || amount > 999 || totalPrice < 0)
            return StashOperationResult.InvalidInventory;
        lock (_sync)
        {
            if (!IsAvailable || _repository.Snapshot == null) return StashOperationResult.InvalidInventory;
            var receipt = new ShopTransactionReceipt(transactionId, profileId);
            if (IsShopReceiptApplied(_repository.Snapshot, receipt)) return StashOperationResult.AlreadyApplied;
            var gate = BeginTownMutation(out var current);
            if (gate != StashOperationResult.Success) return gate;
            var items = new[] { new StashItem(lootId, amount) };
            if (current.Currency < totalPrice || ExceedsCapacity(current.Loadout, items, LocalProfileSnapshot.MaxLoadoutSlots))
                return StashOperationResult.InvalidInventory;
            var candidate = current.Clone();
            if (!TryMerge(candidate.Loadout, items, LocalProfileSnapshot.MaxLoadoutSlots)) return StashOperationResult.InvalidInventory;
            candidate.Currency -= totalPrice;
            RecordShopReceipt(candidate, receipt);
            next = candidate;
            return StashOperationResult.Success;
        }
    }

    /// <summary>Atomically caches authoritative shop state and its receipt. V3 uses full inventory hydration.</summary>
    public StashOperationResult ApplyBuybackAcknowledgement(ProfileId profileId, ShopTransactionId transactionId,
        Grimhold.Backend.ShopTransactionResult response)
    {
        lock (_sync)
        {
            if (!IsAvailable || _repository.Snapshot == null || profileId != _profileId || !transactionId.IsValid ||
                response.currency < 0 || response.revision < 0 || response.loadout == null)
                return StashOperationResult.InvalidInventory;
            var receipt = new ShopTransactionReceipt(transactionId, profileId);
            if (IsShopReceiptApplied(_repository.Snapshot, receipt)) return StashOperationResult.AlreadyApplied;
            var gate = BeginTownMutation(out var current);
            if (gate != StashOperationResult.Success) return gate;
            var next = current.Clone();
            next.Loadout.Clear();
            if (current.InventorySchemaVersion == 3)
            {
                if (!ApplicationStashServiceBootstrapper.HydrateSnapshot(_profileId, next,
                    new Grimhold.Backend.InventoryData
                    {
                        inventorySchemaVersion = 3, loadout = response.loadout, stash = response.stash,
                        currency = response.currency, preparedEquipment = response.preparedEquipment,
                        revision = response.revision
                    }, null, _lootCatalog, _abilityCatalog)) return StashOperationResult.HydrationFailed;
                if (next.Loadout.Count != response.loadout.Length ||
                    (response.stash != null && next.Stash.Count != response.stash.Length))
                    return StashOperationResult.InvalidInventory;
            }
            else
            {
                // Legacy acknowledgements require no Store catalog; keep their original validation.
                var seenSlots = new HashSet<int>();
                for (int i = 0; i < response.loadout.Length; i++)
                {
                    var item = response.loadout[i];
                    var lootId = new LootId(item.lootId);
                    int slot = current.InventorySchemaVersion == 2 ? item.slot : i;
                    if (!lootId.IsValid || item.amount <= 0 || slot < 0 || !seenSlots.Add(slot))
                        return StashOperationResult.InvalidInventory;
                    next.Loadout.Add(new StashItem(lootId, item.amount, slot));
                }
                next.Loadout.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            }
            next.Currency = response.currency;
            next.RemoteRevision = response.revision;
            RecordShopReceipt(next, receipt);
            return Commit(next);
        }
    }

    private static bool IsShopReceiptApplied(LocalProfileSnapshot snapshot, ShopTransactionReceipt receipt)
    {
        if (receipt.TransactionId.Timestamp <= snapshot.ShopIdempotencyWatermark)
            return true;

        foreach (var applied in snapshot.AppliedShopTransactionReceipts)
            if (applied.Equals(receipt)) return true;
        return false;
    }

    private static void RecordShopReceipt(LocalProfileSnapshot next, ShopTransactionReceipt receipt)
    {
        next.AppliedShopTransactionReceipts.Add(receipt);
        next.AppliedShopTransactionReceipts.Sort((a, b) => a.TransactionId.Timestamp.CompareTo(b.TransactionId.Timestamp));

        while (next.AppliedShopTransactionReceipts.Count > LocalProfileSnapshot.MaxAppliedShopTransactionReceipts)
        {
            var oldest = next.AppliedShopTransactionReceipts[0];
            next.AppliedShopTransactionReceipts.RemoveAt(0);
            if (oldest.TransactionId.Timestamp > next.ShopIdempotencyWatermark)
                next.ShopIdempotencyWatermark = oldest.TransactionId.Timestamp;
        }
    }

    public StashOperationResult TryConsumeStashSlot(int slot, LootId id, int amount)
    {
        if (!IsAvailable || _repository.Snapshot == null) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        if (!TryRemoveAt(next.Stash, slot, id, amount)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    public StashOperationResult TryConsumeLoot(LootId lootId, int amount)
    {
        if (!lootId.IsValid || amount <= 0 || !IsAvailable || _repository.Snapshot == null) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        if (!TryRemove(next.Stash, lootId, amount)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    /// <summary>
    /// Reads the confirmed snapshot for a Town Stash, Loadout or Equipment mutation. A reservation
    /// owns Inventory and Equipment until it is confirmed or rolled back, so none of these
    /// operations may run while one is pending: a later rollback would overwrite their result.
    /// </summary>
    private StashOperationResult BeginTownMutation(out LocalProfileSnapshot current)
    {
        current = _repository.Snapshot;
        if (!IsAvailable || current == null) return StashOperationResult.InvalidInventory;
        return current.PendingReservation != null
            ? StashOperationResult.ReservationPending
            : StashOperationResult.Success;
    }

    // Each transfer is planned once: the Try method commits the planned snapshot and the Evaluate
    // method discards it, so a dry run always reports exactly what the transfer would return. A
    // plan that succeeds with no snapshot means there is nothing to change.

    public StashOperationResult TryTransferToLoadout(LootId lootId, int amount) =>
        CommitPlan(PlanTransferToLoadout(lootId, amount, out LocalProfileSnapshot next), next);

    public StashOperationResult TryTransferToStash(LootId lootId, int amount) =>
        CommitPlan(PlanTransferToStash(lootId, amount, out LocalProfileSnapshot next), next);

    public StashOperationResult TryTransferAllToLoadout() =>
        CommitPlan(PlanTransferAllToLoadout(out LocalProfileSnapshot next), next);

    public StashOperationResult TryTransferAllToStash() =>
        CommitPlan(PlanTransferAllToStash(out LocalProfileSnapshot next), next);

    /// <summary>Reports what <see cref="TryTransferToLoadout"/> would return now, changing nothing.</summary>
    public StashOperationResult EvaluateTransferToLoadout(LootId lootId, int amount) =>
        PlanTransferToLoadout(lootId, amount, out _);

    /// <summary>Reports what <see cref="TryTransferToStash"/> would return now, changing nothing.</summary>
    public StashOperationResult EvaluateTransferToStash(LootId lootId, int amount) =>
        PlanTransferToStash(lootId, amount, out _);

    /// <summary>Reports what <see cref="TryTransferAllToLoadout"/> would return now, changing nothing.</summary>
    public StashOperationResult EvaluateTransferAllToLoadout() => PlanTransferAllToLoadout(out _);

    /// <summary>Reports what <see cref="TryTransferAllToStash"/> would return now, changing nothing.</summary>
    public StashOperationResult EvaluateTransferAllToStash() => PlanTransferAllToStash(out _);

    // Compatibility order is unambiguous only when the list has one stack per definition.
    public StashOperationResult TryOrder(InventoryList list, IReadOnlyList<LootId> lootIds) =>
        CommitPlan(PlanOrder(list, ResolveSlotOrder(list, lootIds), out LocalProfileSnapshot next), next);

    public StashOperationResult EvaluateOrder(InventoryList list, IReadOnlyList<LootId> lootIds) =>
        PlanOrder(list, ResolveSlotOrder(list, lootIds), out _);

    private IReadOnlyList<int> ResolveSlotOrder(InventoryList list, IReadOnlyList<LootId> lootIds)
    {
        if (lootIds == null) return null;
        var source = list == InventoryList.Stash ? GetStash() : GetLoadout();
        var slots = new List<int>();
        var ids = new HashSet<LootId>();
        foreach (StashItem item in source) if (!ids.Add(item.LootId)) return null;
        foreach (LootId id in lootIds)
        {
            int index = FindIndex(source, id);
            if (index < 0) return null;
            slots.Add(source[index].Slot);
        }
        return slots;
    }

    public StashOperationResult TryOrderSlots(InventoryList list, IReadOnlyList<int> slots) =>
        CommitPlan(PlanOrder(list, slots, out LocalProfileSnapshot next), next);
    public StashOperationResult EvaluateOrderSlots(InventoryList list, IReadOnlyList<int> slots) => PlanOrder(list, slots, out _);

    private StashOperationResult PlanOrder(InventoryList list, IReadOnlyList<int> slots, out LocalProfileSnapshot next)
    {
        next = null;
        var gate = BeginTownMutation(out var current);
        if (gate != StashOperationResult.Success) return gate;
        if ((list != InventoryList.Stash && list != InventoryList.Loadout) || slots == null)
            return StashOperationResult.InvalidInventory;
        var source = list == InventoryList.Stash ? current.Stash : current.Loadout;
        if (!HasValidSlots(source) || slots.Count != source.Count) return StashOperationResult.InvalidInventory;
        var seen = new HashSet<int>();
        var ordered = new List<StashItem>();
        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            int index = FindSlotIndex(source, slots[i]);
            if (index < 0 || !seen.Add(slots[i])) return StashOperationResult.InvalidInventory;
            ordered.Add(source[index].AtSlot(i));
            changed |= source[index].Slot != i;
        }
        if (!changed) return StashOperationResult.Success;
        next = current.Clone();
        var target = list == InventoryList.Stash ? next.Stash : next.Loadout;
        target.Clear(); target.AddRange(ordered);
        return StashOperationResult.Success;
    }

    private StashOperationResult CommitPlan(StashOperationResult plan, LocalProfileSnapshot next) =>
        plan == StashOperationResult.Success && next != null ? Commit(next) : plan;

    private int SourceSlot(LootId id, bool fromStash)
    {
        var source = fromStash ? GetStash() : GetLoadout();
        int index = FindIndex(source, id);
        return index < 0 ? -1 : source[index].Slot;
    }

    private StashOperationResult PlanTransferToLoadout(LootId id, int amount, out LocalProfileSnapshot next) =>
        PlanTransfer(SourceSlot(id, true), id, amount, true, out next);
    private StashOperationResult PlanTransferToStash(LootId id, int amount, out LocalProfileSnapshot next) =>
        PlanTransfer(SourceSlot(id, false), id, amount, false, out next);

    public StashOperationResult EvaluateTransfer(int sourceSlot, LootId id, int amount, bool fromStash) =>
        PlanTransfer(sourceSlot, id, amount, fromStash, out _);
    public StashOperationResult TryTransfer(int sourceSlot, LootId id, int amount, bool fromStash) =>
        CommitPlan(PlanTransfer(sourceSlot, id, amount, fromStash, out var next), next);

    private StashOperationResult PlanTransfer(int sourceSlot, LootId id, int amount, bool fromStash,
        out LocalProfileSnapshot next)
    {
        next = null;
        // A reservation owns the lists even when legacy lookup finds no source slot.
        // Keep its refusal ahead of every inventory-input check for Try and Evaluate alike.
        var gate = BeginTownMutation(out var current);
        if (gate != StashOperationResult.Success) return gate;
        if (!id.IsValid || amount <= 0 || sourceSlot < 0) return StashOperationResult.InvalidInventory;
        var candidate = current.Clone();
        var source = fromStash ? candidate.Stash : candidate.Loadout;
        var destination = fromStash ? candidate.Loadout : candidate.Stash;
        int index = FindSlotIndex(source, sourceSlot);
        if (index < 0 || source[index].LootId != id) return StashOperationResult.InvalidInventory;
        int actual = Math.Min(amount, source[index].Amount);
        var incoming = new[] { new StashItem(id, actual, -1, source[index].Instance) };
        int capacity = fromStash ? LocalProfileSnapshot.MaxLoadoutSlots : StashCapacity;
        if (ExceedsCapacity(destination, incoming, capacity)) return StashOperationResult.InventoryFull;
        if (!TryRemoveAt(source, sourceSlot, id, actual) || !TryMerge(destination, incoming, capacity))
            return StashOperationResult.InvalidInventory;
        next = candidate;
        return StashOperationResult.Success;
    }

    /// <summary>Plans an addressed split without saving or publishing ownership changes.</summary>
    public StashOperationResult EvaluateSplit(InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId)
    {
        lock (_sync) return PlanSplit(list, sourceSlot, amount, destinationSlot, lootId, out _);
    }

    /// <summary>Creates one stack in an empty slot of the same list, atomically and without compaction.</summary>
    public StashOperationResult TrySplit(InventoryList list, int sourceSlot, int amount, int destinationSlot, LootId lootId)
    {
        lock (_sync)
        {
            var result = PlanSplit(list, sourceSlot, amount, destinationSlot, lootId, out var next);
            return CommitPlan(result, next);
        }
    }

    private StashOperationResult PlanSplit(InventoryList list, int sourceSlot, int amount, int destinationSlot,
        LootId lootId, out LocalProfileSnapshot next)
    {
        next = null;
        // A pending reservation owns the lists, even for malformed or missing-source input.
        var gate = BeginTownMutation(out var current);
        if (gate != StashOperationResult.Success) return gate;
        if ((list != InventoryList.Stash && list != InventoryList.Loadout) || !lootId.IsValid ||
            sourceSlot < 0 || destinationSlot < 0 || sourceSlot == destinationSlot || amount <= 0)
            return StashOperationResult.InvalidInventory;

        var items = list == InventoryList.Stash ? current.Stash : current.Loadout;
        if (!HasValidSlots(items)) return StashOperationResult.InvalidInventory;
        int index = FindSlotIndex(items, sourceSlot);
        if (index < 0 || items[index].LootId != lootId || amount >= items[index].Amount)
            return StashOperationResult.InvalidInventory;
        if (FindSlotIndex(items, destinationSlot) >= 0) return StashOperationResult.InvalidInventory;
        int capacity = list == InventoryList.Stash ? StashCapacity : LocalProfileSnapshot.MaxLoadoutSlots;
        if (destinationSlot >= capacity || items.Count >= capacity) return StashOperationResult.InventoryFull;

        var candidate = current.Clone();
        var target = list == InventoryList.Stash ? candidate.Stash : candidate.Loadout;
        StashItem source = items[index];
        // Use addressed removal, never the receive/merge rule: duplicate IDs stay separate.
        if (!TryRemoveAt(target, sourceSlot, lootId, amount)) return StashOperationResult.InvalidInventory;
        target.Add(new StashItem(source.LootId, amount, destinationSlot));
        target.Sort((a, b) => a.Slot.CompareTo(b.Slot));
        next = candidate;
        return StashOperationResult.Success;
    }

    public StashOperationResult EvaluateMove(InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id) =>
        PlanMove(from, fromSlot, to, toSlot, id, out _);
    public StashOperationResult TryMove(InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id) =>
        CommitPlan(PlanMove(from, fromSlot, to, toSlot, id, out var next), next);

    private StashOperationResult PlanMove(InventoryList from, int fromSlot, InventoryList to, int toSlot, LootId id,
        out LocalProfileSnapshot next)
    {
        next = null;
        var gate = BeginTownMutation(out var current);
        if (gate != StashOperationResult.Success) return gate;
        if ((from != InventoryList.Stash && from != InventoryList.Loadout) ||
            (to != InventoryList.Stash && to != InventoryList.Loadout)) return StashOperationResult.InvalidInventory;
        var candidate = current.Clone();
        var source = from == InventoryList.Stash ? candidate.Stash : candidate.Loadout;
        var target = to == InventoryList.Stash ? candidate.Stash : candidate.Loadout;
        if (!ProfileInventoryRules.TryMove(source, fromSlot, target, toSlot, id,
            from == InventoryList.Stash ? StashCapacity : LocalProfileSnapshot.MaxLoadoutSlots,
            to == InventoryList.Stash ? StashCapacity : LocalProfileSnapshot.MaxLoadoutSlots))
            return StashOperationResult.InvalidInventory;
        next = candidate;
        return StashOperationResult.Success;
    }

    private StashOperationResult PlanTransferAllToLoadout(out LocalProfileSnapshot next)
    {
        next = null;
        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success) return gate;
        if (current.Stash.Count == 0) return StashOperationResult.Success;
        LocalProfileSnapshot candidate = current.Clone();
        if (ExceedsCapacity(candidate.Loadout, candidate.Stash, LocalProfileSnapshot.MaxLoadoutSlots))
            return StashOperationResult.InventoryFull;
        if (!TryMerge(candidate.Loadout, candidate.Stash, LocalProfileSnapshot.MaxLoadoutSlots)) return StashOperationResult.InvalidInventory;
        candidate.Stash.Clear();
        next = candidate;
        return StashOperationResult.Success;
    }

    private StashOperationResult PlanTransferAllToStash(out LocalProfileSnapshot next)
    {
        next = null;
        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success) return gate;
        if (current.Loadout.Count == 0) return StashOperationResult.Success;
        LocalProfileSnapshot candidate = current.Clone();
        if (!CanAdmitStashItems(current.Stash, candidate.Loadout)) return StashOperationResult.InventoryFull;
        if (!TryMerge(candidate.Stash, candidate.Loadout, StashCapacity)) return StashOperationResult.InvalidInventory;
        candidate.Loadout.Clear();
        next = candidate;
        return StashOperationResult.Success;
    }

    // Capacity is admission-only: accepted remote excess may remain and existing IDs may merge.
    private bool CanAdmitStashItems(IReadOnlyList<StashItem> current, IReadOnlyList<StashItem> incoming)
    {
        return !ExceedsCapacity(current, incoming, StashCapacity);
    }

    public StashOperationResult TryImportItems(IReadOnlyList<StashItem> items)
    {
        if (!HasValidItems(items)) return StashOperationResult.InvalidInventory;
        var next = _repository.Snapshot.Clone();
        if (ExceedsCapacity(next.Loadout, items, LocalProfileSnapshot.MaxLoadoutSlots))
            return StashOperationResult.PersistenceFailed;
        if (!TryMerge(next.Loadout, items, LocalProfileSnapshot.MaxLoadoutSlots)) return StashOperationResult.InvalidInventory;
        return Commit(next);
    }

    /// <summary>
    /// Moves one unit from the persistent Inventory into an Equipment slot in one atomic commit.
    /// A replaced unit returns to Inventory after the new unit is removed, so capacity is evaluated
    /// against the final swap state rather than an invalid intermediate state.
    /// </summary>
    public StashOperationResult TryAssignPreparedEquipment(EquipmentSlot slot, LootId lootId)
    {
        if (!EquipmentSlotRules.IsEquipmentSlot(slot) || !lootId.IsValid || _lootCatalog == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success)
        {
            return gate;
        }

        if (current.RequiresEquipmentHydration) return StashOperationResult.InvalidInventory;
        if (FindAmount(current.Loadout, lootId) < 1 ||
            !_lootCatalog.TryGet(lootId.Value, out LootDefinition definition) || definition == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        LocalProfileSnapshot next = current.Clone();
        PreparedEquipmentLoadout candidate = next.PreparedEquipment;
        EquipmentSlot displacedSecondSlot = EquipmentSlot.None;
        if (!EquipmentSlotRules.IsCompatible(definition, slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        WeaponSetSlot targetSet = EquipmentSlotRules.GetWeaponSet(slot);
        if (EquipmentSlotRules.IsOffHandSlot(slot) &&
            PreparedEquipmentLoadout.IsOffHandBlocked(candidate, targetSet, _lootCatalog))
        {
            return StashOperationResult.InvalidInventory;
        }

        if (definition.Category == LootCategory.Weapon)
        {
            WeaponDefinition weapon = definition.WeaponDefinition;
            if (weapon.Handedness == WeaponHandedness.TwoHanded)
            {
                displacedSecondSlot = EquipmentSlotRules.GetOffHandSlot(targetSet);
                candidate = candidate.Without(displacedSecondSlot);
            }
        }

        candidate = candidate.With(slot, lootId);

        if (definition.Category == LootCategory.Weapon &&
            !PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                candidate, current.CharacterAttributes, _lootCatalog, out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                candidate,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return StashOperationResult.InvalidInventory;
        }

        candidate = candidate.With(slot, lootId, current.Loadout[FindIndex(current.Loadout, lootId)].Instance);
        if (!TryRemove(next.Loadout, lootId, 1))
        {
            return StashOperationResult.InvalidInventory;
        }

        LootId previousLootId = next.PreparedEquipment.Get(slot);
        if (!TryReturnEquippedUnit(next.Loadout, previousLootId, next.PreparedEquipment.GetInstance(slot)))
        {
            return StashOperationResult.PersistenceFailed;
        }

        if (displacedSecondSlot != EquipmentSlot.None &&
            !TryReturnEquippedUnit(next.Loadout, next.PreparedEquipment.Get(displacedSecondSlot), next.PreparedEquipment.GetInstance(displacedSecondSlot)))
        {
            return StashOperationResult.PersistenceFailed;
        }

        next.PreparedEquipment = candidate;
        return Commit(next);
    }

    public StashOperationResult EvaluateEquipFromStash(
        EquipmentSlot slot, LootId lootId, out PreparedEquipmentLoadout equipment)
    {
        StashOperationResult result = PlanEquipFromStash(slot, lootId, out LocalProfileSnapshot next);
        equipment = result == StashOperationResult.Success ? next.PreparedEquipment : default;
        return result;
    }

    public StashOperationResult TryEquipFromStash(EquipmentSlot slot, LootId lootId)
    {
        StashOperationResult result = PlanEquipFromStash(slot, lootId, out LocalProfileSnapshot next);
        return result == StashOperationResult.Success ? Commit(next) : result;
    }

    private StashOperationResult PlanEquipFromStash(
        EquipmentSlot slot, LootId lootId, out LocalProfileSnapshot next)
    {
        next = null;
        if (!EquipmentSlotRules.IsEquipmentSlot(slot) || !lootId.IsValid || _lootCatalog == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success)
        {
            return gate;
        }

        if (current.RequiresEquipmentHydration) return StashOperationResult.InvalidInventory;
        if (FindAmount(current.Stash, lootId) < 1 ||
            !_lootCatalog.TryGet(lootId.Value, out LootDefinition definition) || definition == null)
        {
            return StashOperationResult.InvalidInventory;
        }

        next = current.Clone();
        PreparedEquipmentLoadout candidate = next.PreparedEquipment;
        EquipmentSlot displacedSecondSlot = EquipmentSlot.None;
        if (!EquipmentSlotRules.IsCompatible(definition, slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        WeaponSetSlot targetSet = EquipmentSlotRules.GetWeaponSet(slot);
        if (EquipmentSlotRules.IsOffHandSlot(slot) &&
            PreparedEquipmentLoadout.IsOffHandBlocked(candidate, targetSet, _lootCatalog))
        {
            return StashOperationResult.InvalidInventory;
        }

        if (definition.Category == LootCategory.Weapon)
        {
            WeaponDefinition weapon = definition.WeaponDefinition;
            if (weapon.Handedness == WeaponHandedness.TwoHanded)
            {
                displacedSecondSlot = EquipmentSlotRules.GetOffHandSlot(targetSet);
                candidate = candidate.Without(displacedSecondSlot);
            }
        }

        candidate = candidate.With(slot, lootId);

        if (definition.Category == LootCategory.Weapon &&
            !PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                candidate, current.CharacterAttributes, _lootCatalog, out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                candidate,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return StashOperationResult.InvalidInventory;
        }

        candidate = candidate.With(slot, lootId, current.Stash[FindIndex(current.Stash, lootId)].Instance);
        if (!TryRemove(next.Stash, lootId, 1))
        {
            return StashOperationResult.InvalidInventory;
        }

        var displaced = new List<StashItem>();
        LootId previousLootId = next.PreparedEquipment.Get(slot);
        if (previousLootId.IsValid) displaced.Add(new StashItem(previousLootId, 1, -1, next.PreparedEquipment.GetInstance(slot)));
        if (displacedSecondSlot != EquipmentSlot.None)
        {
            LootId second = next.PreparedEquipment.Get(displacedSecondSlot);
            if (second.IsValid) displaced.Add(new StashItem(second, 1, -1, next.PreparedEquipment.GetInstance(displacedSecondSlot)));
        }
        // No intermediate Loadout slot is needed for the Stash unit. Displaced units use
        // the same admission rule as all other receives, including overflow rejection.
        if (ExceedsCapacity(next.Loadout, displaced, LocalProfileSnapshot.MaxLoadoutSlots))
            return StashOperationResult.InventoryFull;
        if (!TryMerge(next.Loadout, displaced, LocalProfileSnapshot.MaxLoadoutSlots))
            return StashOperationResult.PersistenceFailed;

        next.PreparedEquipment = candidate;
        return StashOperationResult.Success;
    }

    private static bool TryReturnEquippedUnit(List<StashItem> inventory, LootId lootId, EquipmentInstance instance = null)
    {
        if (!lootId.IsValid) return true;
        return TryMerge(inventory, new[] { new StashItem(lootId, 1, -1, instance) }, LocalProfileSnapshot.MaxLoadoutSlots);
    }

    /// <summary>Moves one equipped unit back into the persistent Inventory atomically.</summary>
    public StashOperationResult TryClearPreparedEquipment(EquipmentSlot slot)
    {
        if (!EquipmentSlotRules.IsEquipmentSlot(slot))
        {
            return StashOperationResult.InvalidInventory;
        }

        StashOperationResult gate = BeginTownMutation(out LocalProfileSnapshot current);
        if (gate != StashOperationResult.Success)
        {
            return gate;
        }

        if (current.RequiresEquipmentHydration) return StashOperationResult.InvalidInventory;
        LootId previousLootId = current.PreparedEquipment.Get(slot);
        if (!previousLootId.IsValid)
        {
            return StashOperationResult.Success;
        }

        LocalProfileSnapshot next = current.Clone();
        if ((FindIndex(next.Loadout, previousLootId) < 0 &&
             next.Loadout.Count >= LocalProfileSnapshot.MaxLoadoutSlots) ||
            !TryMerge(next.Loadout, new[] { new StashItem(previousLootId, 1, -1, current.PreparedEquipment.GetInstance(slot)) }, LocalProfileSnapshot.MaxLoadoutSlots))
        {
            return StashOperationResult.PersistenceFailed;
        }

        next.PreparedEquipment = next.PreparedEquipment.Without(slot);
        return Commit(next);
    }

    /// <summary>
    /// Normalizes the local Loadout and prepared Weapon Equipment so the aggregate holds exactly
    /// one valid effective Main Hand weapon before a raid reservation is attempted.
    /// The operation is atomic, deterministic and idempotent: a profile that is already prepared
    /// commits nothing, so retrying a launch never grants or duplicates a recovery weapon.
    /// </summary>
    public ExpeditionPreparationResult TryPrepareExpeditionEquipment()
    {
        lock (_sync)
        {
            ExpeditionPreparationResult result = InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation preparation);
            return result != ExpeditionPreparationResult.Success || preparation.WeaponSource == RecoveryWeaponPreparation.Source.Prepared
                ? result
                : TryPrepareRecoveryWeapon(_repository.Snapshot);
        }
    }

    /// <summary>Read-only classification; opening or cancelling consent never changes ownership.</summary>
    public ExpeditionPreparationResult InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation preparation)
    {
        lock (_sync) return InspectRecoveryWeaponPreparationCore(out preparation);
    }

    private ExpeditionPreparationResult InspectRecoveryWeaponPreparationCore(out RecoveryWeaponPreparation preparation)
    {
        preparation = null;
        LocalProfileSnapshot current = _repository.Snapshot;
        if (!IsAvailable || current == null || current.ProfileId != _profileId || _lootCatalog == null)
        {
            return ExpeditionPreparationResult.ProfileUnavailable;
        }

        // A reservation already owns the prepared equipment; re-preparing would double-grant.
        if (current.PendingReservation != null)
        {
            preparation = new RecoveryWeaponPreparation(this, current, RecoveryWeaponPreparation.Source.Prepared);
            return ExpeditionPreparationResult.Success;
        }

        PreparedEquipmentLoadout prepared = current.PreparedEquipment;

        // A persisted assignment that no longer resolves to a usable weapon is corruption.
        // It fails explicitly instead of being overwritten or hidden behind a recovery grant.
        if (prepared.HasAnyWeapon && !PreparedEquipmentLoadout.TryValidate(
                prepared,
                _lootCatalog,
                requireWeapon: false,
                out _))
        {
            return ExpeditionPreparationResult.InvalidPreparedWeapon;
        }

        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                prepared,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return ExpeditionPreparationResult.AttributeRequirementsNotMet;
        }

        // Admission carries the active Set explicitly, so either valid Main Hand is sufficient.
        if (PreparedEquipmentLoadout.HasUsableWeapon(prepared, _lootCatalog))
        {
            preparation = new RecoveryWeaponPreparation(this, current, RecoveryWeaponPreparation.Source.Prepared);
            return ExpeditionPreparationResult.Success;
        }

        if (!PreparedEquipmentLoadout.IsUsableWeaponDefinition(_recoveryWeaponLootId, _lootCatalog))
            return ExpeditionPreparationResult.RecoveryWeaponUnavailable;
        var recovery = new PreparedEquipmentLoadout(_recoveryWeaponLootId, default);
        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(recovery, current.CharacterAttributes, _lootCatalog, out _))
            return ExpeditionPreparationResult.AttributeRequirementsNotMet;
        RecoveryWeaponPreparation.Source source = FindAmount(current.Loadout, _recoveryWeaponLootId) > 0
            ? RecoveryWeaponPreparation.Source.Loadout
            : FindAmount(current.Stash, _recoveryWeaponLootId) > 0
                ? RecoveryWeaponPreparation.Source.Stash
                : RecoveryWeaponPreparation.Source.NewGrant;
        preparation = new RecoveryWeaponPreparation(this, current, source);
        return ExpeditionPreparationResult.Success;
    }

    public bool IsRecoveryWeaponPreparationCurrent(RecoveryWeaponPreparation preparation)
    {
        lock (_sync)
            return preparation != null && preparation.Matches(this, _repository.Snapshot) && IsAvailable;
    }

    /// <summary>Commits the inspected profile revision through the existing recovery ownership helper.</summary>
    public ExpeditionPreparationResult TryPrepareExpeditionEquipment(RecoveryWeaponPreparation preparation)
    {
        lock (_sync)
        {
            if (!IsRecoveryWeaponPreparationCurrent(preparation))
                return ExpeditionPreparationResult.ProfileUnavailable;
            ExpeditionPreparationResult result = InspectRecoveryWeaponPreparation(out RecoveryWeaponPreparation current);
            if (result != ExpeditionPreparationResult.Success || current.WeaponSource != preparation.WeaponSource)
                return result == ExpeditionPreparationResult.Success ? ExpeditionPreparationResult.InvalidPreparedWeapon : result;
            return current.WeaponSource == RecoveryWeaponPreparation.Source.Prepared
                ? ExpeditionPreparationResult.Success
                : TryPrepareRecoveryWeapon(_repository.Snapshot);
        }
    }

    /// <summary>
    /// Guarantees Town access to exactly one recovery weapon when no weapon is prepared. A unit
    /// already owned is reused before a new one is granted, so the grant cannot accumulate.
    /// </summary>
    private ExpeditionPreparationResult TryPrepareRecoveryWeapon(LocalProfileSnapshot current)
    {
        if (!PreparedEquipmentLoadout.IsUsableWeaponDefinition(_recoveryWeaponLootId, _lootCatalog))
        {
            return ExpeditionPreparationResult.RecoveryWeaponUnavailable;
        }

        var recovery = new PreparedEquipmentLoadout(_recoveryWeaponLootId, default);
        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                recovery,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return ExpeditionPreparationResult.AttributeRequirementsNotMet;
        }

        LocalProfileSnapshot next = current.Clone();
        if (FindAmount(next.Loadout, _recoveryWeaponLootId) >= 1)
        {
            TryRemove(next.Loadout, _recoveryWeaponLootId, 1);
        }
        else if (FindAmount(next.Stash, _recoveryWeaponLootId) >= 1)
        {
            TryRemove(next.Stash, _recoveryWeaponLootId, 1);
        }

        // Only Set A Main Hand is granted: all other prepared Equipment remains untouched.
        next.PreparedEquipment = next.PreparedEquipment
            .With(EquipmentSlot.WeaponSetAMainHand, _recoveryWeaponLootId);
        return Commit(next) == StashOperationResult.Success
            ? ExpeditionPreparationResult.Success
            : ExpeditionPreparationResult.PersistenceFailed;
    }

    public StashOperationResult TryCreateLoadoutReservation(
        string reservationId,
        out PendingLoadoutReservation reservation)
    {
        reservation = null;
        if (string.IsNullOrWhiteSpace(reservationId) || !IsAvailable)
        {
            Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. string.IsNullOrWhiteSpace(reservationId)={string.IsNullOrWhiteSpace(reservationId)}, !IsAvailable={!IsAvailable}");
            return StashOperationResult.InvalidInventory;
        }
        
        var current = _repository.Snapshot;
        if (current.PendingReservation != null)
        {
            if (!string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
            {
                Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Pending reservation ID mismatch. Existing: {current.PendingReservation.ReservationId}, Requested: {reservationId}");
                return StashOperationResult.InvalidInventory;
            }

            if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                    current.PendingReservation.PreparedEquipment,
                    current.CharacterAttributes,
                    _lootCatalog,
                    out string validationError1))
            {
                Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Attribute requirements not met: {validationError1}");
                return StashOperationResult.AttributeRequirementsNotMet;
            }

            reservation = current.PendingReservation.Clone();
            return StashOperationResult.Success;
        }

        if (!HasValidSlots(current.Loadout)) return StashOperationResult.InvalidInventory;
        // Backend data may leave the Loadout over capacity; the excess must be moved
        // to the Stash before it can enter a raid, whose inventory is capped.
        if (current.Loadout.Count > LocalProfileSnapshot.MaxLoadoutSlots)
        {
            return StashOperationResult.InventoryFull;
        }

        int totalPhysicalSlots = CalculatePhysicalSlots(current.Loadout, current.PreparedEquipment, _lootCatalog);
        if (totalPhysicalSlots > LocalProfileSnapshot.MaxLoadoutSlots)
        {
            Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Physical slots required ({totalPhysicalSlots}) exceeds MaxLoadoutSlots ({LocalProfileSnapshot.MaxLoadoutSlots}).");
            return StashOperationResult.InventoryFull;
        }

        if (!PreparedEquipmentLoadout.TryValidate(
                current.PreparedEquipment,
                _lootCatalog,
                requireWeapon: true,
                out string validationError2))
        {
            Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Validation failed: {validationError2}");
            return StashOperationResult.InvalidInventory;
        }

        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                current.PreparedEquipment,
                current.CharacterAttributes,
                _lootCatalog,
                out string validationError3))
        {
            Debug.LogError($"[LocalProfileStore] TryCreateLoadoutReservation failed. Attribute requirements not met: {validationError3}");
            return StashOperationResult.AttributeRequirementsNotMet;
        }

        var next = current.Clone();
        next.PendingReservation = new PendingLoadoutReservation(
            reservationId,
            next.Loadout,
            next.PreparedEquipment);
        next.Loadout.Clear();
        next.PreparedEquipment = default;
        StashOperationResult result = Commit(next);
        if (result == StashOperationResult.Success)
        {
            reservation = _repository.Snapshot.PendingReservation.Clone();
        }
        return result;
    }

    public StashOperationResult TryConfirmLoadoutReservation(string reservationId)
    {
        var current = _repository.Snapshot;
        if (current.PendingReservation == null || !string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
            return StashOperationResult.InvalidInventory;
        var next = current.Clone();
        next.PendingReservation = null;
        return Commit(next);
    }

    public StashOperationResult TryRollbackLoadoutReservation(string reservationId)
    {
        var current = _repository.Snapshot;
        if (current.PendingReservation == null || !string.Equals(current.PendingReservation.ReservationId, reservationId, StringComparison.Ordinal))
            return StashOperationResult.InvalidInventory;
        var next = current.Clone();
        RestoreSlots(next.Loadout, next.PendingReservation.Items);
        PreparedEquipmentLoadout restoredWeapons = next.PendingReservation.PreparedEquipment;
        if (!PreparedEquipmentLoadout.TryValidate(
                restoredWeapons,
                _lootCatalog,
                requireWeapon: true,
                out _))
        {
            return StashOperationResult.PersistenceFailed;
        }
        if (!PreparedEquipmentLoadout.TryValidateWeaponRequirements(
                restoredWeapons,
                current.CharacterAttributes,
                _lootCatalog,
                out _))
        {
            return StashOperationResult.AttributeRequirementsNotMet;
        }
        next.PreparedEquipment = restoredWeapons;
        next.PendingReservation = null;
        return Commit(next);
    }

    /// <summary>
    /// Commits the exact authoritative raid inventory into the persistent Loadout.
    ///
    /// A valid raid admission consumes the pending reservation and leaves the
    /// persistent Loadout empty. A new receipt therefore fails atomically when
    /// that invariant is not true; extracted items are never redirected to Stash.
    /// </summary>
    public StashOperationResult TryCommitExtraction(
        ExtractionReceipt receipt, 
        IReadOnlyList<StashItem> items,
        PreparedEquipmentLoadout preparedEquipment,
        long consolidatedExperience,
        int resultingLevel,
        long resultingExperience)
    {
        if (!receipt.IsValid || receipt.ProfileId != _profileId || !HasValidItems(items, allowEmpty: true, allowDuplicates: true))
            return StashOperationResult.InvalidInventory;

        if (items.Count > LocalProfileSnapshot.MaxLoadoutSlots)
            return StashOperationResult.PersistenceFailed;

        LocalProfileSnapshot current = _repository.Snapshot;
        foreach (ExtractionReceipt applied in current.AppliedExtractionReceipts)
            if (applied.Equals(receipt)) return StashOperationResult.AlreadySecured;

        if (current.Loadout.Count != 0)
            return StashOperationResult.PersistenceFailed;

        LocalProfileSnapshot next = current.Clone();

        if (_missionCatalog != null && items != null)
        {
            for (int i = 0; i < next.ActiveMissions.Count; i++)
            {
                var instance = next.ActiveMissions[i];
                if (instance.State != MissionState.Activa) continue;
                if (!_missionCatalog.TryGet(instance.MissionId.Value, out var definition)) continue;

                var extractionEvent = new MissionContributionEvent(
                    ObjectiveFamily.Extraccion,
                    1,
                    _profileId,
                    null,
                    receipt.RaidId);
                MissionProgressEngine.TryApplyProgress(instance, definition, extractionEvent);

                for (int j = 0; j < items.Count; j++)
                {
                    var item = items[j];
                    if (!item.IsValid) continue;
                    var itemEvent = new MissionContributionEvent(
                        ObjectiveFamily.ExtraccionDeObjetos,
                        item.Amount,
                        _profileId,
                        item.LootId.Value,
                        receipt.RaidId);
                    MissionProgressEngine.TryApplyProgress(instance, definition, itemEvent);
                }
            }
        }

        if (!TryMerge(next.Loadout, items, LocalProfileSnapshot.MaxLoadoutSlots))
            return StashOperationResult.PersistenceFailed;
        next.AppliedExtractionReceipts.Add(receipt);
        while (next.AppliedExtractionReceipts.Count > LocalProfileSnapshot.MaxAppliedExtractionReceipts)
            next.AppliedExtractionReceipts.RemoveAt(0);

        next.Level = resultingLevel;
        next.CurrentExperience = resultingExperience;
        next.LastAppliedProgressionResultSequence = receipt.ResultSequence;

        var progressionReceipt = new ProgressionReceipt(
            receipt.RaidId,
            _profileId,
            receipt.ResultSequence,
            resultingExperience,
            resultingLevel);

        next.LastProgressionReceipt = progressionReceipt;
        next.AppliedProgressionReceipts.Add(progressionReceipt);
        while (next.AppliedProgressionReceipts.Count > LocalProfileSnapshot.MaxAppliedProgressionReceipts)
            next.AppliedProgressionReceipts.RemoveAt(0);

        next.PreparedEquipment = preparedEquipment;

        next.PendingExtractionCommit = new PendingExtractionCommit(
            receipt,
            items,
            preparedEquipment,
            consolidatedExperience,
            resultingLevel);

        var progression = new ExperienceApplicationResult(current.Level, current.CurrentExperience,
            next.Level, next.CurrentExperience, Math.Max(0, next.Level - current.Level));
        return Commit(next, progression);
    }
    public void ClearPendingExtractionCommit()
    {
        LocalProfileSnapshot current = _repository.Snapshot;
        if (current == null || current.PendingExtractionCommit == null)
            return;
            
        LocalProfileSnapshot next = current.Clone();
        next.PendingExtractionCommit = null;
        Commit(next);
    }

    public StashOperationResult TrySyncProgression(int level, long experience, Grimhold.Backend.CharacterAttributesData attributes)
    {
        LocalProfileSnapshot current = _repository.Snapshot;
        if (current == null) return StashOperationResult.InvalidInventory;

        LocalProfileSnapshot next = current.Clone();
        next.Level = level;
        next.CurrentExperience = experience;

        if (CharacterAttributeState.TryCreate(
                attributes.vitality, attributes.resistance, attributes.strength,
                attributes.dexterity, attributes.intelligence, attributes.luck,
                attributes.availablePoints,
                out var state))
        {
            if (!TryRevalidatePreparedAbilities(
                    current,
                    state,
                    out PreparedAbilityLoadout revalidatedAbilities,
                    out string abilityError))
            {
                Debug.LogError($"[LocalProfileStore] Progression sync found invalid prepared abilities: {abilityError}");
                return StashOperationResult.InvalidInventory;
            }

            next.CharacterAttributes = state;
            next.PreparedAbilities = revalidatedAbilities;
        }

        return Commit(next);
    }

    private bool TryRevalidatePreparedAbilities(
        LocalProfileSnapshot snapshot,
        in CharacterAttributeState attributes,
        out PreparedAbilityLoadout revalidated,
        out string error) =>
        PreparedAbilityLoadout.TryRevalidateAfterAttributeChange(
            snapshot.PreparedAbilities,
            snapshot.UnlockedAbilities,
            attributes,
            _abilityCatalog,
            out revalidated,
            out error);

    private StashOperationResult Commit(LocalProfileSnapshot next, ExperienceApplicationResult? progression = null)
    {
        lock (_sync)
        {
            SynchronizeActiveResourceMissions(next);
            string error = null;
            bool saved = IsAvailable && _repository.TrySave(next, out error);
            if (!saved)
            {
                Debug.LogError(
                    $"[LocalProfileStore] Commit failed. Available={IsAvailable}; " +
                    $"Status={Status}; Error={error ?? _repository.LastError ?? "none"}.");
                return StashOperationResult.PersistenceFailed;
            }
            if (progression.HasValue) ProgressionCommitted?.Invoke(_profileId, progression.Value);
            ProfileCommitted?.Invoke(_profileId);
            return StashOperationResult.Success;
        }
    }

    private static bool HasValidItems(IReadOnlyList<StashItem> items, bool allowEmpty = false, bool allowDuplicates = false)
    {
        if (items == null || (!allowEmpty && items.Count == 0)) return false;
        var seen = new HashSet<LootId>();
        foreach (StashItem item in items)
            if (!item.IsValid || (!allowDuplicates && !seen.Add(item.LootId))) return false;
        return true;
    }

    private static int CalculatePhysicalSlots(
        IReadOnlyList<StashItem> loadout,
        PreparedEquipmentLoadout equipment,
        LootDefinitionCatalog catalog)
    {
        if (catalog == null)
        {
            return loadout != null ? loadout.Count : 0;
        }

        var counts = new Dictionary<LootId, int>();
        if (loadout != null)
        {
            for (int i = 0; i < loadout.Count; i++)
            {
                StashItem item = loadout[i];
                if (item.LootId.IsValid && item.Amount > 0)
                {
                    counts[item.LootId] = counts.TryGetValue(item.LootId, out int existing)
                        ? existing + item.Amount
                        : item.Amount;
                }
            }
        }

        EquipmentSlot[] slots = EquipmentSlotRules.AllSlots;
        for (int i = 0; i < slots.Length; i++)
        {
            LootId eqId = equipment.Get(slots[i]);
            if (eqId.IsValid)
            {
                counts[eqId] = counts.TryGetValue(eqId, out int existing)
                    ? existing + 1
                    : 1;
            }
        }

        int totalSlots = 0;
        foreach (KeyValuePair<LootId, int> pair in counts)
        {
            if (catalog.TryGet(pair.Key.Value, out LootDefinition def) && def != null && def.StackMax > 0)
            {
                totalSlots += (pair.Value + def.StackMax - 1) / def.StackMax;
            }
            else
            {
                totalSlots += pair.Value;
            }
        }

        return totalSlots;
    }

}
