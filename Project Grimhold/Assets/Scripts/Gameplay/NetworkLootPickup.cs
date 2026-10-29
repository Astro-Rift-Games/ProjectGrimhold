using Fusion;
using UnityEngine;

/// <summary>
/// Synchronized network component that manages world loot pickup.
/// Implements IPickup and interacts only under State Authority.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkLootPickup : NetworkBehaviour, IPickup
{
    [Header("Loot Configuration")]
    [SerializeField]
    private LootDefinition _lootDefinition;

    [SerializeField]
    private int _amount = 1;

    [SerializeField]
    private LootDefinitionCatalog _lootCatalog;

    [SerializeField]
    private SpriteRenderer _worldRenderer;

    [Header("World Presentation")]
    [SerializeField]
    private string _sortingLayerName = "Default";

    [SerializeField]
    private int _sortingOrder = 2;

    [Networked]
    private NetworkBool IsConsumed { get; set; }

    [Networked]
    private NetworkBool IsPublished { get; set; }

    /// <summary>Whether replicated loot identity and quantity are ready for interaction.</summary>
    [Networked]
    public NetworkBool IsInitialized { get; private set; }

    /// <summary>Deterministic shared-catalog index replicated to every peer.</summary>
    [Networked]
    public int LootCatalogIndex { get; private set; }

    [Networked]
    private int SynchronizedAmount { get; set; }

    [Networked]
    public int FirstAcquisitionEligibleAmount { get; private set; }

    [Networked]
    private RaidLootPickupCompactOriginState RaidOriginState { get; set; }

    /// <summary>Remaining cooldown for reusable expedition utilities (Doc 14 TASK-234).</summary>
    [Networked]
    public TickTimer CooldownExpiration { get; set; }

    public bool IsAvailable => IsInitialized && IsPublished && !IsConsumed;

    public new EntityId Id => new EntityId(unchecked((int)Object.Id.Raw));

    private Collider2D[] _cachedColliders;
    private EntityRegistry _registry;
    private bool _isRegistered;
    private EntityId _registeredId;
    private LootEntry _spawnContentOverride;
    private bool _hasSpawnContentOverride;
    private int _spawnEligibleAmountOverride;
    private RaidLootOriginTransfer _spawnOriginOverride;
    private TickTimer _spawnCooldownOverride;
    private LootDefinition _resolvedLootDefinition;

    /// <summary>Static loot definition resolved locally from replicated catalog identity.</summary>
    public LootDefinition LootDefinition => _resolvedLootDefinition;

    /// <summary>Replicated quantity delivered by a successful pickup interaction.</summary>
    public int Amount => IsInitialized ? SynchronizedAmount : 0;

    /// <summary>Shared catalog used to translate stable network indices.</summary>
    public LootDefinitionCatalog LootCatalog => _lootCatalog;

    /// <summary>Sorting layer applied to the renderer of every resolved world sprite.</summary>
    public string SortingLayerName => _sortingLayerName;

    /// <summary>Sorting order applied to the renderer of every resolved world sprite.</summary>
    public int SortingOrder => _sortingOrder;

    private void Awake()
    {
        _cachedColliders = GetComponentsInChildren<Collider2D>(true);
        if (_worldRenderer == null)
        {
            _worldRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        ApplyWorldPresentation();
    }

    public override void Spawned()
    {
        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            InitializeAuthoritativeState();
        }

        RefreshResolvedLoot();
        _registry = Runner.GetComponent<EntityRegistry>();
        ApplyPublicationState();
        RefreshRegistration();
    }

    public override void FixedUpdateNetwork()
    {
        ApplyPublicationState();
        RefreshRegistration();
    }

    public override void Render()
    {
        RefreshResolvedLoot();
        ApplyPublicationState();
        RefreshRegistration();
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (_isRegistered && _registry != null)
        {
            _registry.TryUnregisterEntity(_registeredId, this);
            _isRegistered = false;
        }

        _resolvedLootDefinition = null;
        _spawnContentOverride = default;
        _hasSpawnContentOverride = false;
        _spawnOriginOverride = null;
        _spawnCooldownOverride = default;
        _spawnStartsPublished = true;
    }

    /// <summary>
    /// Supplies the loot stack during Fusion's authoritative pre-spawn callback.
    /// The catalog index and quantity are written to replicated state from
    /// <see cref="Spawned"/> before the object is published to proxies.
    /// </summary>
    internal bool TrySetSpawnContentOverride(
        NetworkRunner runner,
        NetworkObject expectedObject,
        in LootEntry entry)
    {
        return TrySetSpawnContentOverride(
            runner, expectedObject, entry, true, entry.Amount, RaidLootOriginTransfer.Dungeon(entry.Amount));
    }

    /// <summary>
    /// Supplies spawn content and whether the pickup can immediately enter the world.
    /// Inventory drops start unpublished until their source extraction commits.
    /// </summary>
    internal bool TrySetSpawnContentOverride(
        NetworkRunner runner,
        NetworkObject expectedObject,
        in LootEntry entry,
        bool initiallyPublished)
    {
        return TrySetSpawnContentOverride(
            runner, expectedObject, entry, initiallyPublished, entry.Amount,
            RaidLootOriginTransfer.Dungeon(entry.Amount));
    }

    internal bool TrySetSpawnContentOverride(
        NetworkRunner runner,
        NetworkObject expectedObject,
        in LootEntry entry,
        bool initiallyPublished,
        int firstAcquisitionEligibleAmount)
    {
        return TrySetSpawnContentOverride(
            runner, expectedObject, entry, initiallyPublished, firstAcquisitionEligibleAmount,
            RaidLootOriginTransfer.Dungeon(entry.Amount));
    }

    internal bool TrySetSpawnContentOverride(
        NetworkRunner runner,
        NetworkObject expectedObject,
        in LootEntry entry,
        bool initiallyPublished,
        int firstAcquisitionEligibleAmount,
        RaidLootOriginTransfer originTransfer)
    {
        return TrySetSpawnContentOverride(
            runner,
            expectedObject,
            entry,
            initiallyPublished,
            firstAcquisitionEligibleAmount,
            originTransfer,
            default);
    }

    internal bool TrySetSpawnContentOverride(
        NetworkRunner runner,
        NetworkObject expectedObject,
        in LootEntry entry,
        bool initiallyPublished,
        int firstAcquisitionEligibleAmount,
        RaidLootOriginTransfer originTransfer,
        TickTimer cooldownExpiration)
    {
        if (_hasSpawnContentOverride || runner == null || !runner.IsServer ||
            expectedObject == null || expectedObject.gameObject != gameObject ||
            expectedObject.GetComponent<NetworkLootPickup>() != this ||
            !entry.IsValid || _lootCatalog == null ||
            !_lootCatalog.TryGetIndex(entry.LootId, out _) ||
            firstAcquisitionEligibleAmount < 0 ||
            firstAcquisitionEligibleAmount > entry.Amount || originTransfer == null ||
            !originTransfer.TryGetTotal(out int originTotal) || originTotal != entry.Amount ||
            originTransfer.Count > RaidLootOriginPackedBuffer.OriginsPerLoot)
        {
            return false;
        }

        _spawnContentOverride = entry;
        _hasSpawnContentOverride = true;
        _spawnStartsPublished = initiallyPublished;
        _spawnEligibleAmountOverride = firstAcquisitionEligibleAmount;
        _spawnOriginOverride = originTransfer;
        _spawnCooldownOverride = cooldownExpiration;
        if (_lootCatalog != null && _lootCatalog.TryGet(entry.LootId.Value, out LootDefinition overrideDef))
        {
            _lootDefinition = overrideDef;
            _resolvedLootDefinition = overrideDef;
        }
        return true;
    }

    /// <summary>
    /// Prevalidates publication of a provisional inventory drop without mutating state.
    /// </summary>
    internal bool ValidateDropPublication(NetworkRunner runner, NetworkObject expectedObject)
    {
        return HasStateAuthority && runner == Runner && expectedObject == Object &&
            IsInitialized && !IsPublished && !IsConsumed && _resolvedLootDefinition != null &&
            FirstAcquisitionEligibleAmount == 0 &&
            TryResolveRaidOriginTransfer(out RaidLootOriginTransfer origins) &&
            origins.TryGetTotal(out int originTotal) && originTotal == SynchronizedAmount;
    }

    /// <summary>
    /// Publishes a prevalidated provisional drop immediately after source extraction commits.
    /// </summary>
    internal void CommitDropPublication(NetworkRunner runner, NetworkObject expectedObject)
    {
        if (!ValidateDropPublication(runner, expectedObject))
        {
            throw new System.InvalidOperationException(
                $"{nameof(NetworkLootPickup)} publication contract was violated.");
        }

        IsPublished = true;
        ApplyPublicationState();
        RefreshRegistration();
    }

    public bool CanInteract(in InteractionRequest request)
    {
        if (request.TargetId != Id) return false;
        if (!IsInitialized || _resolvedLootDefinition == null) return false;
        if (SynchronizedAmount <= 0) return false;
        if (!IsAvailable) return false;
        if (request.InteractorId.Value == 0) return false;

        return true;
    }

    public InteractionResult Interact(in InteractionRequest request)
    {
        // 1. Reject if no State Authority
        if (!HasStateAuthority)
        {
            return InteractionResult.Rejected(InteractionFailureReason.MissingStateAuthority);
        }

        // 2. Validate request and availability
        if (!CanInteract(request))
        {
            return InteractionResult.Rejected(InteractionFailureReason.TargetUnavailable);
        }

        // Resolve the destination capability before reserving this consumable source.
        if (_registry == null || !_registry.TryGetLootReceiver(request.InteractorId, out var receiver) || receiver == null)
        {
            return ToInteractionResult(
                LootTransferResult.Rejected(LootTransferFailureReason.DestinationNotFound),
                false);
        }

        int totalAmount = SynchronizedAmount;
        var transferRequest = CreateTransferRequest(request, totalAmount);

        // The pickup's reservation prevents two authoritative interactions from
        // delivering the same consumable source while reception is validated.
        IsConsumed = true;
        LootTransferFailureReason failureReason = receiver.ValidateReceive(transferRequest);

        if (failureReason != LootTransferFailureReason.None && !IsCapacityFailure(failureReason))
        {
            IsConsumed = false;
            return ToInteractionResult(LootTransferResult.Rejected(failureReason), false);
        }

        if (receiver is not IRaidLootOriginReceiver originReceiver ||
            !originReceiver.IsRaidLootOriginAware ||
            !TryResolveRaidOriginTransfer(out RaidLootOriginTransfer originTransfer))
        {
            IsConsumed = false;
            throw new System.InvalidOperationException("Raid pickup provenance composition is unavailable.");
        }

        RaidLootOriginTransfer takenOrigins = originTransfer;
        RaidLootOriginTransfer remainderOrigins = RaidLootOriginTransfer.Empty;
        if (failureReason == LootTransferFailureReason.None)
        {
            failureReason = originReceiver.ValidateRaidLootOriginReceive(transferRequest, originTransfer);
        }

        // Doc 07 section 11: a capacity-limited stack is collected partially with the
        // maximum admissible quantity; the remainder stays in this pickup with its origins.
        if (failureReason != LootTransferFailureReason.None && IsCapacityFailure(failureReason))
        {
            int transferableAmount = ResolveTransferableAmount(
                receiver, originReceiver, request, originTransfer, totalAmount - 1, ref failureReason);
            if (transferableAmount > 0)
            {
                transferRequest = CreateTransferRequest(request, transferableAmount);
                originTransfer.TrySplit(transferableAmount, out takenOrigins, out remainderOrigins);
                failureReason = LootTransferFailureReason.None;
            }
        }

        if (failureReason != LootTransferFailureReason.None)
        {
            IsConsumed = false;
            return ToInteractionResult(LootTransferResult.Rejected(failureReason), false);
        }

        int transferredAmount = transferRequest.RequestedAmount;
        if (!LootFirstAcquisitionRules.TryResolveExtraction(
                totalAmount,
                FirstAcquisitionEligibleAmount,
                transferredAmount,
                out int eligibleAmount,
                out int remainingAmount,
                out int remainingEligibleAmount))
        {
            IsConsumed = false;
            throw new System.InvalidOperationException(
                $"{nameof(NetworkLootPickup)} first-acquisition state is inconsistent with its quantity.");
        }

        // Encode the remainder before any commit so a provenance failure cannot leave a
        // receiver holding units that this pickup still represents.
        RaidLootPickupCompactOriginState remainderOriginState = default;
        if (remainingAmount > 0 &&
            !RaidLootPickupOriginStateCodec.TryEncode(remainderOrigins, remainingAmount, out remainderOriginState))
        {
            IsConsumed = false;
            throw new System.InvalidOperationException(
                $"{nameof(NetworkLootPickup)} could not encode the provenance of its remaining stack.");
        }

        // Commit cannot reject after successful prevalidation while State Authority
        // retains control. Any inability to apply is an integration contract violation.
        originReceiver.CommitRaidLootReceive(transferRequest, takenOrigins);
        if (eligibleAmount > 0 && _resolvedLootDefinition.ExtractionValuePerUnit > 0 &&
            _registry.TryGetExtractionProgressReceiver(
                request.InteractorId,
                out IExtractionProgressReceiver progressReceiver))
        {
            long progressAmount = checked(
                (long)_resolvedLootDefinition.ExtractionValuePerUnit * eligibleAmount);
            progressReceiver.TryApplyContribution(new ExtractionProgressContribution(
                ExtractionProgressSourceType.LootFirstAcquisition,
                Id,
                progressAmount,
                request.SimulationTick));
        }
        if (eligibleAmount > 0 && _resolvedLootDefinition != null && _resolvedLootDefinition.Category == LootCategory.Treasure &&
            _registry != null && _registry.TryGetDungeonInstabilitySink(out IDungeonInstabilitySink sink))
        {
            sink.TryRecordTreasureFirstAcquisition(_resolvedLootDefinition.Family);
        }

        FirstAcquisitionEligibleAmount = remainingEligibleAmount;

        if (!CooldownExpiration.ExpiredOrNotRunning(Runner))
        {
            if (receiver is Component receiverComp &&
                receiverComp.TryGetComponent<IUtilityCooldownReceiver>(out var cooldownReceiver))
            {
                cooldownReceiver.SetUtilityCooldown(_resolvedLootDefinition.LootId, CooldownExpiration);
            }
        }

        if (receiver is ILootPickupFeedbackSink feedbackSink)
        {
            feedbackSink.PublishPickupGrant(transferRequest);
        }
        LootTransferResult transferResult = LootTransferResult.Succeeded(transferRequest);

        if (remainingAmount > 0)
        {
            SynchronizedAmount = remainingAmount;
            RaidOriginState = remainderOriginState;
            IsConsumed = false;
            return ToInteractionResult(transferResult, false);
        }

        Runner.Despawn(Object);
        return ToInteractionResult(transferResult, true);
    }

    private LootTransferRequest CreateTransferRequest(in InteractionRequest request, int amount) =>
        new(Id, request.InteractorId, _resolvedLootDefinition.LootId, amount, request.SimulationTick);

    /// <summary>
    /// Finds the largest canonical prefix of this stack, up to <paramref name="maximumAmount"/>, that
    /// both side-effect-free receiver validators accept. Canonical prefixes are nested, so admissibility
    /// is monotonic and a binary search is exact. Returns zero and the one-unit failure when none fits.
    /// </summary>
    private int ResolveTransferableAmount(
        ILootReceiver receiver,
        IRaidLootOriginReceiver originReceiver,
        in InteractionRequest request,
        RaidLootOriginTransfer originTransfer,
        int maximumAmount,
        ref LootTransferFailureReason failureReason)
    {
        int admissible = 0;
        int low = 1;
        int high = maximumAmount;
        while (low <= high)
        {
            int candidate = low + (high - low) / 2;
            LootTransferFailureReason candidateFailure = ValidatePartialReceive(
                receiver, originReceiver, request, originTransfer, candidate);
            if (candidateFailure == LootTransferFailureReason.None)
            {
                admissible = candidate;
                low = candidate + 1;
                continue;
            }

            if (candidate == 1)
            {
                failureReason = candidateFailure;
            }
            high = candidate - 1;
        }

        return admissible;
    }

    private LootTransferFailureReason ValidatePartialReceive(
        ILootReceiver receiver,
        IRaidLootOriginReceiver originReceiver,
        in InteractionRequest request,
        RaidLootOriginTransfer originTransfer,
        int amount)
    {
        if (!originTransfer.TrySplit(amount, out RaidLootOriginTransfer taken, out _))
        {
            throw new System.InvalidOperationException(
                $"{nameof(NetworkLootPickup)} provenance does not match its quantity.");
        }

        LootTransferRequest candidateRequest = CreateTransferRequest(request, amount);
        LootTransferFailureReason failure = receiver.ValidateReceive(candidateRequest);
        return failure != LootTransferFailureReason.None
            ? failure
            : originReceiver.ValidateRaidLootOriginReceive(candidateRequest, taken);
    }

    private static bool IsCapacityFailure(LootTransferFailureReason failureReason) =>
        failureReason is LootTransferFailureReason.InventoryFull or
            LootTransferFailureReason.MaxTransportableExceeded or
            LootTransferFailureReason.Overflow;

    private void InitializeAuthoritativeState()
    {
        LootEntry entry = _hasSpawnContentOverride
            ? _spawnContentOverride
            : _lootDefinition != null
                ? new LootEntry(_lootDefinition.LootId, _amount)
                : default;

        if (!entry.IsValid || _lootCatalog == null ||
            !_lootCatalog.TryGetIndex(entry.LootId, out int catalogIndex))
        {
            Debug.LogError(
                $"{nameof(NetworkLootPickup)} could not initialize '{name}' because its loot entry or catalog is invalid.",
                this);
            IsInitialized = false;
            return;
        }

        LootCatalogIndex = catalogIndex;
        SynchronizedAmount = entry.Amount;
        if (_lootCatalog != null && _lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition authoritativeDefinition))
        {
            _lootDefinition = authoritativeDefinition;
            _resolvedLootDefinition = authoritativeDefinition;
        }
        FirstAcquisitionEligibleAmount = _hasSpawnContentOverride
            ? _spawnEligibleAmountOverride
            : entry.Amount;
        CooldownExpiration = _hasSpawnContentOverride ? _spawnCooldownOverride : default;
        IsConsumed = false;
        IsPublished = _hasSpawnContentOverride ? _spawnStartsPublished : true;
        RaidLootOriginTransfer originTransfer = _hasSpawnContentOverride
            ? _spawnOriginOverride
            : RaidLootOriginTransfer.Dungeon(entry.Amount);
        if (!TryWriteRaidOriginTransfer(originTransfer, entry.Amount))
        {
            Debug.LogError($"{nameof(NetworkLootPickup)} could not initialize Raid provenance for '{name}'.", this);
            IsInitialized = false;
            return;
        }
        IsInitialized = true;
    }

    internal bool TryResolveRaidOriginTransfer(out RaidLootOriginTransfer transfer)
        => RaidLootPickupOriginStateCodec.TryDecode(RaidOriginState, SynchronizedAmount, out transfer);

    private bool TryWriteRaidOriginTransfer(RaidLootOriginTransfer transfer, int expectedAmount)
    {
        if (!RaidLootPickupOriginStateCodec.TryEncode(transfer, expectedAmount, out var state))
        {
            return false;
        }
        RaidOriginState = state;
        return true;
    }

    private void RefreshResolvedLoot()
    {
        if (!IsInitialized || _lootCatalog == null ||
            !_lootCatalog.TryGetByIndex(LootCatalogIndex, out LootDefinition definition))
        {
            _resolvedLootDefinition = null;
            return;
        }

        _lootDefinition = definition;

        if (_resolvedLootDefinition == definition)
        {
            return;
        }

        _resolvedLootDefinition = definition;
        if (_worldRenderer != null)
        {
            _worldRenderer.sprite = definition.WorldSprite;
            ApplyWorldPresentation();
        }
    }

    private void ApplyWorldPresentation()
    {
        if (_worldRenderer == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_sortingLayerName))
        {
            _worldRenderer.sortingLayerName = _sortingLayerName;
        }

        _worldRenderer.sortingOrder = _sortingOrder;
    }

    private bool _spawnStartsPublished = true;

    private void ApplyPublicationState()
    {
        bool published = IsPublished;
        if (_worldRenderer != null)
        {
            _worldRenderer.enabled = published;
        }

        if (_cachedColliders == null)
        {
            return;
        }

        for (int i = 0; i < _cachedColliders.Length; i++)
        {
            if (_cachedColliders[i] != null)
            {
                _cachedColliders[i].enabled = published;
            }
        }
    }

    private void RefreshRegistration()
    {
        if (!IsPublished)
        {
            if (_isRegistered && _registry != null)
            {
                _registry.TryUnregisterEntity(_registeredId, this);
                _isRegistered = false;
            }

            return;
        }

        if (_isRegistered || _registry == null)
        {
            return;
        }

        _registeredId = Id;
        _isRegistered = _registry.TryRegisterEntity(_registeredId, this, _cachedColliders);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_worldRenderer == null)
        {
            _worldRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        ApplyWorldPresentation();
    }
#endif

    private static InteractionResult ToInteractionResult(
        in LootTransferResult transferResult,
        bool isConsumed)
    {
        if (transferResult.Success)
        {
            return InteractionResult.Succeeded(isConsumed);
        }

        return transferResult.FailureReason switch
        {
            LootTransferFailureReason.MissingAuthority =>
                InteractionResult.Rejected(InteractionFailureReason.MissingStateAuthority),
            LootTransferFailureReason.DestinationNotFound =>
                InteractionResult.Rejected(InteractionFailureReason.ReceiverNotFound),
            LootTransferFailureReason.OutOfRange =>
                InteractionResult.Rejected(InteractionFailureReason.OutOfRange),
            _ => InteractionResult.Rejected(
                InteractionFailureReason.LootRejected,
                transferResult.FailureReason)
        };
    }
}
