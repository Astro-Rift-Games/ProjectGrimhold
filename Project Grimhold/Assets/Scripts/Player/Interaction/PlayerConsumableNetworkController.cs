using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Owns timed and aimed authoritative consumable execution (Doc 14 TASK-233).
/// Presentation observes replicated state. Immediate consumption rule applies
/// to AimedQuickSlot throwables (Explosive Bomb, Throwing Dagger, Smoke Bomb).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerLootReceiver))]
public sealed class PlayerConsumableNetworkController : NetworkBehaviour,
    IUtilityCooldownProvider,
    IUtilityCooldownReceiver
{
    public static readonly LootId ExplosiveBombId = new("CON_EXPLOSIVE_BOMB");
    public static readonly LootId ThrowingDaggerId = new("CON_THROWING_DAGGER");
    public static readonly LootId SmokeBombId = new("CON_SMOKE_BOMB");
    public static readonly LootId RuneCompassId = new("UTL_RUNE_COMPASS");
    public static readonly LootId ArcaneDetectorId = new("UTL_ARCANE_DETECTOR");

    [SerializeField] private LootDefinitionCatalog _lootCatalog;
    [SerializeField] private MonoBehaviour _characterSource;

    [Header("Throwables (Doc 14 TASK-233)")]
    [SerializeField] private NetworkPrefabRef _throwingDaggerPrefab;
    [SerializeField] private NetworkPrefabRef _explosiveBombPrefab;
    [SerializeField] private NetworkPrefabRef _smokeZonePrefab;
    [SerializeField] private LayerMask _throwableImpactLayerMask;
    [SerializeField, Min(1f)] private float _maxThrowDistance = 7f;

    private ICharacter _character;
    private PlayerLootReceiver _lootReceiver;
    private NetworkMatchController _matchController;
    private PlayerAbilityRuntimeNetworkController _abilityRuntime;
    private PlayerCombatNetworkController _combat;
    private PlayerExtractionController _extraction;
    private IMovementState _movementState;
    private FusionProjectileSpawner _projectileSpawner;
    private EntityRegistry _registry;
    private bool _dependenciesValid;
    private bool _spawned;
    private uint _clientNextSequence = 1;
    private uint _clientInFlightSequence;
    private uint _pendingSequence;
    private int _pendingCatalogIndex;
    private Vector2 _pendingTargetPosition;
    private readonly Queue<Confirmation> _confirmations = new();

    [Networked] private TickTimer ConsumptionTimer { get; set; }
    [Networked] private int ConsumptionCatalogIndex { get; set; }
    [Networked] private uint ConsumptionRequestSequence { get; set; }
    [Networked] private uint ServerLastProcessedSequence { get; set; }
    [Networked] private Vector2 AimTargetPosition { get; set; }
    [Networked] private NetworkBool IsAimedThrowActive { get; set; }
    [Networked] private TickTimer RuneCompassCooldown { get; set; }
    [Networked] private TickTimer ArcaneDetectorCooldown { get; set; }

    public event Action<LootId> ConsumeConfirmed;
    public event Action<ConsumableFailureReason> ConsumeRejected;
    public event Action<CardinalDirection, float> RuneCompassActivated;
    public event Action<int, float> ArcaneDetectorActivated;
    public bool HasRequestInFlight => _clientInFlightSequence > 0;
    // Keep the gate closed through the completion tick, until the authoritative transaction clears it.
    public bool IsConsuming => _spawned && Object != null && Object.IsValid && ConsumptionTimer.IsRunning;
    public float VoluntaryMovementSpeedMultiplier
    {
        get
        {
            if (IsAimedThrowActive) return 0.5f;
            if (IsConsuming && TryGetConsumingDefinition(out LootDefinition def))
            {
                if (def.LootId == ArcaneDetectorId) return 0.5f;
                if (def.ConsumableDefinition != null) return def.ConsumableDefinition.MovementSpeedMultiplier;
            }
            return 1f;
        }
    }

    private void Awake() => CacheDependencies();

    public override void Spawned()
    {
        CacheDependencies();
        _dependenciesValid = ValidateDependencies();
        _matchController = Runner.GetComponent<NetworkMatchController>();
        _registry = Runner.GetComponent<EntityRegistry>();
        _spawned = true;
        _clientNextSequence = ServerLastProcessedSequence + 1;
        _clientInFlightSequence = 0;
        _pendingSequence = 0;
        _pendingTargetPosition = Vector2.zero;
        _confirmations.Clear();

        if (_throwableImpactLayerMask.value == 0)
        {
            _throwableImpactLayerMask = (1 << 6) | (1 << 7) | (1 << 10) | (1 << 11);
        }
        // Copied execution and deadlines are deliberately not reset during restoration.
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        _spawned = false;
        _clientInFlightSequence = 0;
        _pendingSequence = 0;
        _pendingTargetPosition = Vector2.zero;
        _confirmations.Clear();
    }

    public bool TryGetConsumingDefinition(out LootDefinition definition)
    {
        definition = null;
        return IsConsuming && _lootCatalog != null &&
            _lootCatalog.TryGetByIndex(ConsumptionCatalogIndex, out definition);
    }

    public bool TryRequestConsume(LootId lootId, Vector2 targetPosition = default)
    {
        if (!_spawned || !HasInputAuthority || !_dependenciesValid || !IsGameplayPhaseActive()) return false;
        if (PlayerDownedGate.IsDowned(_character))
        {
            _confirmations.Enqueue(new Confirmation(-1, false, ConsumableFailureReason.Downed));
            return false;
        }
        if (HasRequestInFlight || IsConsuming)
        {
            _confirmations.Enqueue(new Confirmation(-1, false, ConsumableFailureReason.ConsumptionInProgress));
            return false;
        }
        if (_lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int catalogIndex)) return false;
        if (_lootReceiver != null && _lootReceiver.GetLootAmount(lootId) <= 0)
        {
            _confirmations.Enqueue(new Confirmation(catalogIndex, false, ConsumableFailureReason.InsufficientAmount));
            return false;
        }

        if (lootId == RuneCompassId && !RuneCompassCooldown.ExpiredOrNotRunning(Runner))
        {
            _confirmations.Enqueue(new Confirmation(catalogIndex, false, ConsumableFailureReason.CooldownActive));
            return false;
        }

        if (lootId == ArcaneDetectorId && !ArcaneDetectorCooldown.ExpiredOrNotRunning(Runner))
        {
            _confirmations.Enqueue(new Confirmation(catalogIndex, false, ConsumableFailureReason.CooldownActive));
            return false;
        }

        if (_lootCatalog.TryGetByIndex(catalogIndex, out LootDefinition definition) &&
            definition.UseMode == ItemUseMode.AimedQuickSlot)
        {
            if (targetPosition == default)
            {
                Vector2 dir = _movementState != null ? _movementState.FacingDirection : Vector2.down;
                if (dir.sqrMagnitude < 0.001f) dir = Vector2.down;
                targetPosition = (Vector2)transform.position + dir * 3f;
            }
            else
            {
                Vector2 offset = targetPosition - (Vector2)transform.position;
                if (offset.magnitude > _maxThrowDistance)
                {
                    targetPosition = (Vector2)transform.position + offset.normalized * _maxThrowDistance;
                }
            }
        }

        uint sequence = _clientNextSequence++;
        _clientInFlightSequence = sequence;
        RpcInvokeInfo info = RPC_RequestConsume(catalogIndex, sequence, targetPosition);
        if (info.SendMessageResult == RpcSendMessageResult.Sent ||
            HasStateAuthority && info.LocalInvokeResult == RpcLocalInvokeResult.Invoked) return true;
        _clientInFlightSequence = 0;
        return false;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward || !_dependenciesValid) return;

        if (IsConsuming)
        {
            if (!IsGameplayPhaseActive() || _character == null || !_character.IsAlive ||
                PlayerDownedGate.IsDowned(_character) ||
                _extraction != null && _extraction.State == ExtractionState.Extracted)
            {
                CancelConsumption();
            }
            else if (ConsumptionTimer.Expired(Runner))
            {
                uint sequence = ConsumptionRequestSequence;
                int index = ConsumptionCatalogIndex;
                ConsumptionTimer = TickTimer.None;

                if (IsAimedThrowActive)
                {
                    IsAimedThrowActive = false;
                    ExecuteSmokeZone(AimTargetPosition);
                }
                else if (_lootCatalog != null && _lootCatalog.TryGetByIndex(index, out LootDefinition def) && def.LootId == ArcaneDetectorId)
                {
                    ExecuteArcaneDetector();
                    RPC_ReceiveConsumeConfirmation(sequence, index, true, (int)ConsumableFailureReason.None);
                }
                else
                {
                    ConsumableResult result = ProcessAuthoritativeConsume(index);
                    RPC_ReceiveConsumeConfirmation(sequence, index, result.Success, (int)result.FailureReason);
                }
            }
        }

        if (_pendingSequence == 0) return;
        uint pending = _pendingSequence;
        int catalogIndex = _pendingCatalogIndex;
        Vector2 targetPosition = _pendingTargetPosition;
        _pendingSequence = 0;

        ConsumableFailureReason failure = ValidateStart(catalogIndex, out LootDefinition definition);
        if (failure != ConsumableFailureReason.None)
        {
            RPC_ReceiveConsumeConfirmation(pending, catalogIndex, false, (int)failure);
            return;
        }

        PlayerReviveGate.InterruptIfReviving(_character);

        if (definition.Category == LootCategory.Utility)
        {
            if (definition.LootId == RuneCompassId)
            {
                if (!RuneCompassCooldown.ExpiredOrNotRunning(Runner))
                {
                    RPC_ReceiveConsumeConfirmation(pending, catalogIndex, false, (int)ConsumableFailureReason.CooldownActive);
                    return;
                }

                RuneCompassCooldown = TickTimer.CreateFromSeconds(Runner, 30.0f);
                CardinalDirection direction = ExecuteRuneCompass();
                RPC_ReceiveCompassActivation(pending, (byte)direction, CardinalDirectionHelper.DisplayDuration);
                RPC_ReceiveConsumeConfirmation(pending, catalogIndex, true, (int)ConsumableFailureReason.None);
                return;
            }
            else if (definition.LootId == ArcaneDetectorId)
            {
                if (!ArcaneDetectorCooldown.ExpiredOrNotRunning(Runner))
                {
                    RPC_ReceiveConsumeConfirmation(pending, catalogIndex, false, (int)ConsumableFailureReason.CooldownActive);
                    return;
                }

                ConsumptionCatalogIndex = catalogIndex;
                ConsumptionRequestSequence = pending;
                ConsumptionTimer = TickTimer.CreateFromSeconds(Runner, 2.0f);
                return;
            }
        }

        if (definition.UseMode == ItemUseMode.AimedQuickSlot)
        {
            // Immediate consumption rule (TASK-233): 1 unit deducted immediately at throw start
            var request = new LootTransferRequest(_character.Id, _character.Id, definition.LootId, 1, Runner.Tick);
            if (_lootReceiver.ValidateExtraction(request) != LootTransferFailureReason.None ||
                !_lootReceiver.TryResolveRaidLootOriginTransfer(request, RaidLootOriginSelectionOrder.PlayerFirst,
                    out RaidLootOriginTransfer origin))
            {
                RPC_ReceiveConsumeConfirmation(pending, catalogIndex, false, (int)ConsumableFailureReason.InsufficientAmount);
                return;
            }
            _lootReceiver.CommitRaidLootExtraction(request, origin);
            RPC_ReceiveConsumeConfirmation(pending, catalogIndex, true, (int)ConsumableFailureReason.None);

            targetPosition = ResolveAimedTargetPosition(targetPosition);

            if (definition.LootId == ThrowingDaggerId)
            {
                Vector2 dir = targetPosition - (Vector2)transform.position;
                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = _movementState != null ? _movementState.FacingDirection : Vector2.down;
                    if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
                }
                else
                {
                    dir.Normalize();
                }
                ExecuteThrowingDagger(dir);
            }
            else if (definition.LootId == ExplosiveBombId)
            {
                ExecuteExplosiveBomb(targetPosition);
            }
            else if (definition.LootId == SmokeBombId)
            {
                ConsumptionCatalogIndex = catalogIndex;
                ConsumptionRequestSequence = pending;
                AimTargetPosition = targetPosition;
                IsAimedThrowActive = true;
                ConsumptionTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            }
        }
        else
        {
            ConsumptionCatalogIndex = catalogIndex;
            ConsumptionRequestSequence = pending;
            ConsumptionTimer = TickTimer.CreateFromSeconds(Runner, definition.ConsumableDefinition.ConsumptionDurationSeconds);
        }
    }

    /// <summary>Called directly by authoritative damage resolution, never by a presentation event.</summary>
    public void CancelConsumption()
    {
        if (!_spawned || !HasStateAuthority || Runner == null || !Runner.IsForward || !IsConsuming) return;
        uint sequence = ConsumptionRequestSequence;
        int index = ConsumptionCatalogIndex;
        ConsumptionTimer = TickTimer.None;
        IsAimedThrowActive = false;
        RPC_ReceiveConsumeConfirmation(sequence, index, false, (int)ConsumableFailureReason.Interrupted);
    }

    private bool IsGameplayPhaseActive() => _matchController == null ||
        _matchController.Phase == NetworkMatchController.MatchPhase.InProgress;

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority, InvokeLocal = true,
        HostMode = RpcHostMode.SourceIsHostPlayer)]
    private RpcInvokeInfo RPC_RequestConsume(int catalogIndex, uint requestSequence, Vector2 targetPosition, RpcInfo info = default)
    {
        if (!HasStateAuthority || info.Source != Object.InputAuthority || requestSequence == 0 ||
            requestSequence <= ServerLastProcessedSequence) return default;
        ServerLastProcessedSequence = requestSequence;
        if (_pendingSequence != 0 || IsConsuming)
        {
            RPC_ReceiveConsumeConfirmation(requestSequence, catalogIndex, false,
                (int)ConsumableFailureReason.ConsumptionInProgress);
            return default;
        }
        _pendingSequence = requestSequence;
        _pendingCatalogIndex = catalogIndex;
        _pendingTargetPosition = targetPosition;
        return default;
    }

    private ConsumableFailureReason ValidateStart(int catalogIndex, out LootDefinition definition)
    {
        definition = null;
        if (!_dependenciesValid || !IsGameplayPhaseActive()) return ConsumableFailureReason.TargetUnavailable;
        if (_character == null || !_character.IsAlive) return ConsumableFailureReason.TargetDead;
        if (PlayerDownedGate.IsDowned(_character)) return ConsumableFailureReason.Downed;
        if (_abilityRuntime != null && _abilityRuntime.HasActiveExecution) return ConsumableFailureReason.AbilityInProgress;
        if (IsConsuming) return ConsumableFailureReason.ConsumptionInProgress;
        if (_extraction != null && _extraction.State == ExtractionState.Extracted) return ConsumableFailureReason.TargetUnavailable;
        if (_combat != null && _combat.HasPendingAttackRelease) return ConsumableFailureReason.TargetUnavailable;
        if (_lootCatalog == null || !_lootCatalog.TryGetByIndex(catalogIndex, out definition))
            return ConsumableFailureReason.InvalidLoot;
        if (_lootReceiver.GetLootAmount(definition.LootId) <= 0)
            return ConsumableFailureReason.InsufficientAmount;

        if (definition.UseMode == ItemUseMode.AimedQuickSlot)
        {
            return ConsumableFailureReason.None;
        }

        if (definition.Category == LootCategory.Utility)
        {
            if (definition.LootId == RuneCompassId && !RuneCompassCooldown.ExpiredOrNotRunning(Runner))
            {
                return ConsumableFailureReason.CooldownActive;
            }
            if (definition.LootId == ArcaneDetectorId && !ArcaneDetectorCooldown.ExpiredOrNotRunning(Runner))
            {
                return ConsumableFailureReason.CooldownActive;
            }
            return ConsumableFailureReason.None;
        }

        if (definition.ConsumableDefinition == null || !definition.ConsumableDefinition.TryValidate(out _))
            return ConsumableFailureReason.InvalidLoot;
        if (!definition.ConsumableDefinition.Effect.CanApply(_character, out string reason))
        {
            return reason switch
            {
                "La salud ya está al máximo." => ConsumableFailureReason.HealthFull,
                "El maná ya está al máximo." => ConsumableFailureReason.ManaFull,
                "El objetivo está muerto." => ConsumableFailureReason.TargetDead,
                "El objetivo está derribado." => ConsumableFailureReason.Downed,
                _ => ConsumableFailureReason.EffectFailed
            };
        }
        return ConsumableFailureReason.None;
    }

    // Retains the existing completion seam for deterministic integration tests.
    private ConsumableResult ProcessAuthoritativeConsume(int catalogIndex)
    {
        ConsumableFailureReason failure = ValidateStart(catalogIndex, out LootDefinition definition);
        if (failure != ConsumableFailureReason.None) return ConsumableResult.Rejected(failure);
        var request = new LootTransferRequest(_character.Id, _character.Id, definition.LootId, 1, Runner.Tick);
        // Resolve every fallible inventory/provenance condition BEFORE applying the irreversible effect.
        if (_lootReceiver.ValidateExtraction(request) != LootTransferFailureReason.None ||
            !_lootReceiver.TryResolveRaidLootOriginTransfer(request, RaidLootOriginSelectionOrder.PlayerFirst,
                out RaidLootOriginTransfer origin))
            return ConsumableResult.Rejected(ConsumableFailureReason.InsufficientAmount);
        if (!definition.ConsumableDefinition.Effect.TryApplyEffect(_character, out string reason))
        {
            failure = reason switch
            {
                "El objetivo está muerto." => ConsumableFailureReason.TargetDead,
                "La salud ya está al máximo." => ConsumableFailureReason.HealthFull,
                "El maná ya está al máximo." => ConsumableFailureReason.ManaFull,
                "No hay autoridad para curar." => ConsumableFailureReason.MissingAuthority,
                _ => ConsumableFailureReason.EffectFailed
            };
            return ConsumableResult.Rejected(failure);
        }
        _lootReceiver.CommitRaidLootExtraction(request, origin);
        return ConsumableResult.Ok();
    }

    private Vector2 ResolveAimedTargetPosition(Vector2 rawTarget)
    {
        Vector2 origin = (Vector2)transform.position;
        Vector2 offset = rawTarget - origin;
        float dist = offset.magnitude;
        if (dist > _maxThrowDistance)
        {
            dist = _maxThrowDistance;
            offset = offset.normalized * dist;
        }

        if (dist < 0.001f)
        {
            Vector2 facing = _movementState != null ? _movementState.FacingDirection : Vector2.down;
            if (facing.sqrMagnitude < 0.001f) facing = Vector2.down;
            return origin + facing * 3f;
        }

        int obstacleMask = (1 << 6) | (1 << 11);
        RaycastHit2D hit = Physics2D.Raycast(origin, offset.normalized, dist, obstacleMask);
        if (hit.collider != null)
        {
            return hit.point - offset.normalized * 0.2f;
        }

        return origin + offset;
    }

    private void ExecuteThrowingDagger(Vector2 direction)
    {
        if (_projectileSpawner == null)
        {
            _projectileSpawner = GetComponent<FusionProjectileSpawner>();
        }

        NetworkPrefabRef prefab = _throwingDaggerPrefab;

        int mask = _throwableImpactLayerMask.value != 0 ? _throwableImpactLayerMask.value : ((1 << 6) | (1 << 7) | (1 << 10) | (1 << 11));

        ProjectileSpawnRequest request = new ProjectileSpawnRequest(
            ownerId: _character.Id,
            origin: (Vector2)transform.position,
            direction: direction,
            damage: 20f,
            damageType: DamageType.Physical,
            speed: 16f,
            maximumRange: 12f,
            lifetimeSeconds: 2f,
            projectilePrefab: prefab,
            impactLayerMask: mask,
            simulationTick: Runner.Tick,
            knockbackForce: 0f,
            isUnbounded: false,
            enemyOnly: false
        );

        if (_projectileSpawner != null && prefab.IsValid)
        {
            _projectileSpawner.Spawn(in request);
        }
        else if (prefab.IsValid && Runner != null)
        {
            Quaternion rot = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            Runner.TrySpawn(prefab, out NetworkObject _, (Vector2)transform.position, rot, null, (_, no) =>
            {
                if (no.TryGetComponent(out NetworkProjectile proj))
                {
                    proj.InitializeNetworkState(in request, mask);
                    proj.SetRuntimeDependencies(GetComponent<IDamageResolver>());
                }
            });
        }
    }

    private void ExecuteExplosiveBomb(Vector2 targetPosition)
    {
        if (!_explosiveBombPrefab.IsValid || Runner == null) return;

        Vector2 startPos = (Vector2)transform.position;
        Runner.TrySpawn(_explosiveBombPrefab, out NetworkObject _, startPos, Quaternion.identity, null, (_, no) =>
        {
            if (no.TryGetComponent(out NetworkExplosiveBomb bomb))
            {
                bomb.InitializeNetworkState(_character.Id, startPos, targetPosition, GetComponent<IDamageResolver>());
            }
        });
    }

    private void ExecuteSmokeZone(Vector2 targetPosition)
    {
        if (!_smokeZonePrefab.IsValid || Runner == null) return;

        Runner.TrySpawn(_smokeZonePrefab, out NetworkObject _, targetPosition, Quaternion.identity, null, (_, no) =>
        {
            if (no.TryGetComponent(out NetworkSmokeZone zone))
            {
                zone.Configure(3.0f, 8.0f);
            }
        });
    }

    internal void ConfigureThrowables(NetworkPrefabRef daggerPrefab, NetworkPrefabRef bombPrefab, NetworkPrefabRef smokeZonePrefab)
    {
        _throwingDaggerPrefab = daggerPrefab;
        _explosiveBombPrefab = bombPrefab;
        _smokeZonePrefab = smokeZonePrefab;
    }

    public TickTimer GetUtilityCooldown(LootId lootId)
    {
        if (lootId == RuneCompassId) return RuneCompassCooldown;
        if (lootId == ArcaneDetectorId) return ArcaneDetectorCooldown;
        return default;
    }

    public void ClearUtilityCooldown(LootId lootId)
    {
        if (lootId == RuneCompassId) RuneCompassCooldown = TickTimer.None;
        else if (lootId == ArcaneDetectorId) ArcaneDetectorCooldown = TickTimer.None;
    }

    public void SetUtilityCooldown(LootId lootId, TickTimer timer)
    {
        if (lootId == RuneCompassId)
        {
            if (RuneCompassCooldown.ExpiredOrNotRunning(Runner) || timer.TargetTick > RuneCompassCooldown.TargetTick)
            {
                RuneCompassCooldown = timer;
            }
        }
        else if (lootId == ArcaneDetectorId)
        {
            if (ArcaneDetectorCooldown.ExpiredOrNotRunning(Runner) || timer.TargetTick > ArcaneDetectorCooldown.TargetTick)
            {
                ArcaneDetectorCooldown = timer;
            }
        }
    }

    private CardinalDirection ExecuteRuneCompass()
    {
        Vector2 playerPos = (Vector2)transform.position;
        if (_registry != null && _registry.TryGetNearestExtractionSanctuary(playerPos, out _, out Vector2 sanctuaryPos))
        {
            return CardinalDirectionHelper.GetCardinalDirection(playerPos, sanctuaryPos);
        }

        var sanctuaries = UnityEngine.Object.FindObjectsByType<ExtractionSanctuary>(FindObjectsSortMode.None);
        if (sanctuaries != null && sanctuaries.Length > 0)
        {
            float minSqr = float.MaxValue;
            Vector2 nearestPos = default;
            bool found = false;
            for (int i = 0; i < sanctuaries.Length; i++)
            {
                if (sanctuaries[i] == null) continue;
                Vector2 pos = sanctuaries[i].transform.position;
                float sqr = (pos - playerPos).sqrMagnitude;
                if (sqr < minSqr)
                {
                    minSqr = sqr;
                    nearestPos = pos;
                    found = true;
                }
            }
            if (found)
            {
                return CardinalDirectionHelper.GetCardinalDirection(playerPos, nearestPos);
            }
        }

        return CardinalDirection.None;
    }

    private void ExecuteArcaneDetector()
    {
        ArcaneDetectorCooldown = TickTimer.CreateFromSeconds(Runner, 20.0f);
        Vector2 playerPos = (Vector2)transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(playerPos, ArcaneElementDetectionRules.DetectionRadius);
        var detectedNetworkIds = new List<NetworkId>();
        var seenObjects = new HashSet<NetworkObject>();

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null) continue;
            NetworkObject no = hits[i].GetComponentInParent<NetworkObject>();
            if (no == null || !no.IsValid || !seenObjects.Add(no)) continue;

            if (no.TryGetComponent(out NetworkLootPickup pickup))
            {
                if (pickup.IsAvailable && ArcaneElementDetectionRules.IsArcaneDefinition(pickup.LootDefinition))
                {
                    detectedNetworkIds.Add(no.Id);
                }
            }
            else if (no.TryGetComponent(out NetworkLootContainer container))
            {
                detectedNetworkIds.Add(no.Id);
            }
        }

        RPC_ReceiveArcaneDetection(detectedNetworkIds.ToArray(), ArcaneElementDetectionRules.HighlightDuration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveCompassActivation(uint sequence, byte directionValue, float duration)
    {
        var direction = (CardinalDirection)directionValue;
        RuneCompassActivated?.Invoke(direction, duration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveArcaneDetection(NetworkId[] detectedIds, float duration)
    {
        int count = detectedIds != null ? detectedIds.Length : 0;
        if (detectedIds != null && Runner != null)
        {
            for (int i = 0; i < detectedIds.Length; i++)
            {
                if (Runner.TryFindObject(detectedIds[i], out NetworkObject no) && no != null)
                {
                    if (!no.TryGetComponent(out ArcaneHighlightEffect effect))
                    {
                        effect = no.gameObject.AddComponent<ArcaneHighlightEffect>();
                    }
                    effect.Initialize(duration, new Color(0.2f, 0.85f, 1f, 1f));
                }
            }
        }

        ArcaneDetectorActivated?.Invoke(count, duration);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_ReceiveConsumeConfirmation(uint sequence, int index, bool success, int failure)
    {
        if (sequence != _clientInFlightSequence) return;
        _clientInFlightSequence = 0;
        _confirmations.Enqueue(new Confirmation(index, success, (ConsumableFailureReason)failure));
    }

    public override void Render()
    {
        while (_confirmations.Count > 0)
        {
            Confirmation confirmation = _confirmations.Dequeue();
            if (confirmation.Success && _lootCatalog.TryGetByIndex(confirmation.Index, out LootDefinition definition))
                ConsumeConfirmed?.Invoke(definition.LootId);
            else if (!confirmation.Success) ConsumeRejected?.Invoke(confirmation.Failure);
        }
    }

    private void CacheDependencies()
    {
        _character = _characterSource != null ? _characterSource as ICharacter : GetComponent<ICharacter>();
        _lootReceiver = GetComponent<PlayerLootReceiver>();
        _abilityRuntime = GetComponent<PlayerAbilityRuntimeNetworkController>();
        _combat = GetComponent<PlayerCombatNetworkController>();
        _extraction = GetComponent<PlayerExtractionController>();
        _movementState = GetComponent<IMovementState>();
        _projectileSpawner = GetComponent<FusionProjectileSpawner>();
    }

    private bool ValidateDependencies()
    {
        if (_character != null && _lootReceiver != null && _lootCatalog != null) return true;
        Debug.LogError($"{nameof(PlayerConsumableNetworkController)} requires character, loot receiver and catalog.", this);
        return false;
    }

    private readonly struct Confirmation
    {
        public readonly int Index;
        public readonly bool Success;
        public readonly ConsumableFailureReason Failure;
        public Confirmation(int index, bool success, ConsumableFailureReason failure)
        { Index = index; Success = success; Failure = failure; }
    }

#if UNITY_EDITOR
    private void OnValidate() => CacheDependencies();
#endif
}
