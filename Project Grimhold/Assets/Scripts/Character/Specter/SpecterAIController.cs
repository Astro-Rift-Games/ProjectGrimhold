using System.Collections.Generic;
using UnityEngine;
using Fusion;

namespace ProjectGrimhold.Gameplay.Enemies.Specter
{
    /// <summary>
    /// Host-authoritative AI Controller for the Catacombs Specter (MVP US-78 / TASK-285).
    /// Coordinates alternating incorporeity, Hate List target priority,
    /// reactive & post-attack displacement, and static telegraph area magic attacks.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyCharacter))]
    [RequireComponent(typeof(Kinematic2DMovementMotor))]
    public sealed class SpecterAIController : NetworkBehaviour
    {
        [Header("Movement & Range")]
        [SerializeField, Min(0.1f)] private float _chaseSpeed = 2.2f;
        [SerializeField, Min(1f)] private float _detectionRange = 7.0f;
        [SerializeField, Min(1f)] private float _disengageRange = 12.0f;
        [SerializeField, Min(1f)] private float _attackRange = 5.0f;
        [SerializeField, Min(1f)] private float _leashRadius = 15.0f;

        [Header("Combat Settings")]
        [SerializeField, Min(1f)] private float _detonationDamage = 25f;
        [SerializeField, Min(0f)] private float _detonationKnockback = 2.0f;

        [Header("Physics Layers")]
        [SerializeField] private LayerMask _playerLayer = 1 << 7; // Layer 7: Character
        [SerializeField] private LayerMask _obstacleLayer = (1 << 6) | (1 << 11); // Layer 6: WorldCollision, Layer 11: Obstacles

        [Header("Dependencies")]
        [SerializeField] private EnemyCharacter _character;
        [SerializeField] private Kinematic2DMovementMotor _movementMotor;
        [SerializeField] private Rigidbody2D _rigidbody;
        [SerializeField] private Collider2D _collider;

        // ─────────────────────────────────────────────────────────────────────────
        // Networked State
        // ─────────────────────────────────────────────────────────────────────────

        [Networked] public SpecterStateType CurrentState { get; set; }
        [Networked] public TickTimer StateTimer { get; set; }
        [Networked] public TickTimer ReactiveDisplacementTimer { get; set; }
        [Networked] public TickTimer AttackCooldownTimer { get; set; }
        [Networked] private NetworkBool ReactiveDisplacementPending { get; set; }
        [Networked] public Vector2 AttackTargetPosition { get; set; }
        [Networked] public Vector2 FacingDirection { get; set; }
        [Networked] public Vector2 HomeAreaPosition { get; set; }

        // Host-only runtime tracking
        public EntityId CurrentTargetId { get; private set; }
        private readonly SpecterHateList _hateList = new();
        private readonly Collider2D[] _scanHits = new Collider2D[16];
        private Collider2D[] _allColliders;
        private TickTimer _scanTimer;
        private EntityRegistry _entityRegistry;
        private EnemyStunNetworkEffect _stun;

        public SpecterHateList HateList => _hateList;

        private void Awake()
        {
            CacheDependencies();
        }

        public override void Spawned()
        {
            CacheDependencies();

            if (_playerLayer == 0) _playerLayer = LayerMask.GetMask("Character");
            if (_obstacleLayer == 0) _obstacleLayer = LayerMask.GetMask("WorldCollision", "Obstacles");
            _entityRegistry = Runner != null ? Runner.GetComponent<EntityRegistry>() : null;

            if (HasStateAuthority)
            {
                HomeAreaPosition = transform.position;
                FacingDirection = Vector2.down;
                CurrentState = SpecterStateType.Reposo;
                SetCollidersActive(true);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;
            if (_stun != null && _stun.IsActive)
            {
                InterruptChannelForStun();
                return;
            }
            if (ReactiveDisplacementPending)
            {
                ReactiveDisplacementPending = false;
                TransitionTo(SpecterStateType.Desvanecimiento);
            }

            switch (CurrentState)
            {
                case SpecterStateType.Reposo:
                    UpdateReposo();
                    break;
                case SpecterStateType.Persecucion:
                    UpdatePersecucion();
                    break;
                case SpecterStateType.Canalizacion:
                    UpdateCanalizacion();
                    break;
                case SpecterStateType.Detonacion:
                    UpdateDetonacion();
                    break;
                case SpecterStateType.Recuperacion:
                    UpdateRecuperacion();
                    break;
                case SpecterStateType.Desvanecimiento:
                    UpdateDesvanecimiento();
                    break;
                case SpecterStateType.Desvanecido:
                    UpdateDesvanecido();
                    break;
                case SpecterStateType.Reaparicion:
                    UpdateReaparicion();
                    break;
                case SpecterStateType.Muerte:
                    break;
            }
        }

        /// <summary>Cancel the telegraphed AoE without entering Detonacion. Presentation observes the new state.</summary>
        internal void InterruptChannelForStun()
        {
            if (!HasStateAuthority || Runner == null || !Runner.IsSimulationUpdating || !Runner.IsForward) return;
            if (CurrentState == SpecterStateType.Canalizacion)
                TransitionTo(SpecterStateType.Persecucion);
        }

        public void TransitionTo(SpecterStateType newState)
        {
            if (!HasStateAuthority || CurrentState == SpecterStateType.Muerte) return;

            CurrentState = newState;

            switch (newState)
            {
                case SpecterStateType.Reposo:
                    StateTimer = default;
                    SetCollidersActive(true);
                    break;

                case SpecterStateType.Persecucion:
                    StateTimer = default;
                    SetCollidersActive(true);
                    break;

                case SpecterStateType.Canalizacion:
                    StateTimer = TickTimer.CreateFromSeconds(Runner, SpecterRules.ChannelDuration);
                    SetCollidersActive(true);
                    break;

                case SpecterStateType.Detonacion:
                    StateTimer = TickTimer.CreateFromSeconds(Runner, SpecterRules.DetonationDuration);
                    ExecuteDetonation();
                    SetCollidersActive(true);
                    break;

                case SpecterStateType.Recuperacion:
                    StateTimer = TickTimer.CreateFromSeconds(Runner, SpecterRules.RecoveryDuration);
                    SetCollidersActive(true);
                    break;

                case SpecterStateType.Desvanecimiento:
                    StateTimer = TickTimer.CreateFromSeconds(Runner, SpecterRules.FadeOutDuration);
                    SetCollidersActive(false);
                    break;

                case SpecterStateType.Desvanecido:
                    StateTimer = TickTimer.CreateFromSeconds(Runner, SpecterRules.HiddenDuration);
                    SetCollidersActive(false);
                    ExecuteTeleportRelocation();
                    break;

                case SpecterStateType.Reaparicion:
                    StateTimer = TickTimer.CreateFromSeconds(Runner, SpecterRules.FadeInDuration);
                    SetCollidersActive(true);
                    break;

                case SpecterStateType.Muerte:
                    StateTimer = default;
                    SetCollidersActive(false);
                    break;
            }
        }

        public void OnDamageReceived(EntityId attackerId, float damage, DamageType damageType, bool isFatal)
        {
            if (!HasStateAuthority || CurrentState == SpecterStateType.Muerte) return;
            if (!SpecterRules.CanReceiveDamage(CurrentState)) return;

            bool isMagic = damageType == DamageType.Magical;
            bool isCooldownActive = !ReactiveDisplacementTimer.ExpiredOrNotRunning(Runner);

            if (attackerId.Value != 0 && damage > 0f)
            {
                _hateList.RecordDamage(attackerId, damage);
            }

            if (SpecterRules.CanTriggerReactiveDisplacement(CurrentState, isMagic, isFatal, isCooldownActive))
            {
                ReactiveDisplacementTimer = TickTimer.CreateFromSeconds(Runner, SpecterRules.ReactiveDisplacementCooldown);
                // Damage can arrive outside our FSM tick. Preserve the reaction without advancing while Stunned.
                if (_stun != null && _stun.IsActive)
                    ReactiveDisplacementPending = true;
                else
                    TransitionTo(SpecterStateType.Desvanecimiento);
            }
        }

        private void UpdateReposo()
        {
            if (_scanTimer.ExpiredOrNotRunning(Runner))
            {
                _scanTimer = TickTimer.CreateFromSeconds(Runner, 0.2f);
                if (TryScanForPlayer(out PlayerCharacter detectedPlayer))
                {
                    CurrentTargetId = detectedPlayer.Id;
                    TransitionTo(SpecterStateType.Persecucion);
                }
            }
        }

        private void UpdatePersecucion()
        {
            if (!TryGetPlayer(CurrentTargetId, out PlayerCharacter targetPlayer) || !targetPlayer.IsAlive)
            {
                _hateList.Remove(CurrentTargetId);
                if (TrySelectTarget(out EntityId nextTargetId) && TryGetPlayer(nextTargetId, out targetPlayer))
                {
                    CurrentTargetId = nextTargetId;
                }
                else
                {
                    CurrentTargetId = default;
                    TransitionTo(SpecterStateType.Reposo);
                    return;
                }
            }

            Vector2 myPos = transform.position;
            Vector2 targetPos = targetPlayer.transform.position;
            float distanceToTarget = Vector2.Distance(myPos, targetPos);

            // Leash check
            float distanceToHome = Vector2.Distance(myPos, HomeAreaPosition);
            if (distanceToHome > _leashRadius)
            {
                _hateList.Clear();
                CurrentTargetId = default;
                TransitionTo(SpecterStateType.Reposo);
                return;
            }

            if (distanceToTarget > _disengageRange)
            {
                _hateList.Remove(CurrentTargetId);
                CurrentTargetId = default;
                TransitionTo(SpecterStateType.Reposo);
                return;
            }

            // Check attack readiness: within range, attack cooldown expired, and line of sight unobstructed (§9 / TASK-285)
            if (distanceToTarget <= _attackRange && AttackCooldownTimer.ExpiredOrNotRunning(Runner) && HasLineOfSight(myPos, targetPos))
            {
                AttackTargetPosition = targetPos;
                TransitionTo(SpecterStateType.Canalizacion);
                return;
            }

            // Floating chase movement
            Vector2 dir = (targetPos - myPos).normalized;
            FacingDirection = dir;
            if (_movementMotor != null)
            {
                _movementMotor.Move(dir * _chaseSpeed * Runner.DeltaTime);
            }
        }

        private void UpdateCanalizacion()
        {
            if (StateTimer.Expired(Runner))
            {
                TransitionTo(SpecterStateType.Detonacion);
            }
        }

        private void UpdateDetonacion()
        {
            if (StateTimer.Expired(Runner))
            {
                TransitionTo(SpecterStateType.Recuperacion);
            }
        }

        private void UpdateRecuperacion()
        {
            if (StateTimer.Expired(Runner))
            {
                // After 1.5s recovery concludes, start 3.0s cooldown before next attack channel (§9 / TASK-285)
                AttackCooldownTimer = TickTimer.CreateFromSeconds(Runner, SpecterRules.AttackCooldown);
                // Post-attack displacement does NOT consume reactive displacement cooldown
                TransitionTo(SpecterStateType.Desvanecimiento);
            }
        }

        private void UpdateDesvanecimiento()
        {
            if (StateTimer.Expired(Runner))
            {
                TransitionTo(SpecterStateType.Desvanecido);
            }
        }

        private void UpdateDesvanecido()
        {
            if (StateTimer.Expired(Runner))
            {
                TransitionTo(SpecterStateType.Reaparicion);
            }
        }

        private void UpdateReaparicion()
        {
            if (StateTimer.Expired(Runner))
            {
                TransitionTo(SpecterStateType.Persecucion);
            }
        }

        private void ExecuteDetonation()
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                AttackTargetPosition,
                SpecterRules.DetonationRadius,
                _scanHits,
                _playerLayer);

            if (count == 0) return;

            var candidates = new List<(EntityId id, Vector2 position)>(count);
            var playerMap = new Dictionary<EntityId, PlayerCharacter>();

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _scanHits[i];
                if (col != null)
                {
                    PlayerCharacter player = col.GetComponentInParent<PlayerCharacter>();
                    if (player != null && player.IsAlive && !playerMap.ContainsKey(player.Id))
                    {
                        candidates.Add((player.Id, (Vector2)player.transform.position));
                        playerMap[player.Id] = player;
                    }
                }
            }

            bool LinecastWallCheck(Vector2 from, Vector2 to)
            {
                return Physics2D.Linecast(from, to, _obstacleLayer);
            }

            List<EntityId> affected = SpecterAttackOcclusion.ResolveAffectedTargets(
                AttackTargetPosition,
                SpecterRules.DetonationRadius,
                candidates,
                LinecastWallCheck);

            for (int i = 0; i < affected.Count; i++)
            {
                if (playerMap.TryGetValue(affected[i], out PlayerCharacter hitPlayer))
                {
                    Vector2 knockbackDir = ((Vector2)hitPlayer.transform.position - AttackTargetPosition).normalized;
                    if (knockbackDir == Vector2.zero) knockbackDir = Vector2.up;

                    var request = new DamageRequest(
                        _character != null ? _character.Id : default,
                        hitPlayer.Id,
                        _detonationDamage,
                        DamageType.Magical,
                        knockbackDir,
                        (Vector2)hitPlayer.transform.position,
                        Runner != null ? Runner.Tick.Raw : 0,
                        knockbackForce: _detonationKnockback);

                    hitPlayer.ApplyDamage(request);
                }
            }
        }

        private void ExecuteTeleportRelocation()
        {
            Vector2 myPos = transform.position;
            Vector2 targetPos = myPos;

            var candidates = GetActivePlayerCandidates();
            if (_hateList.SelectTarget(candidates, myPos, out EntityId selectedId))
            {
                CurrentTargetId = selectedId;
                if (TryGetPlayer(selectedId, out PlayerCharacter player))
                {
                    targetPos = player.transform.position;
                }
            }
            else if (candidates.Count > 0)
            {
                CurrentTargetId = candidates[0].Id;
                targetPos = candidates[0].Position;
            }

            // Checks that destination itself is free of walls/obstacles; does not require line-of-sight from origin (TASK-285)
            bool IsPointBlocked(Vector2 pt)
            {
                return Physics2D.OverlapCircle(pt, 0.4f, _obstacleLayer) != null;
            }

            Vector2 destination = SpecterTeleportRules.FindDestination(
                myPos,
                targetPos,
                HomeAreaPosition,
                _leashRadius,
                IsPointBlocked);

            if (TryGetComponent(out NetworkTransform netTransform))
            {
                netTransform.Teleport(new Vector3(destination.x, destination.y, transform.position.z));
            }
            else
            {
                transform.position = destination;
                if (_rigidbody != null)
                {
                    _rigidbody.position = destination;
                }
            }
        }

        private bool TrySelectTarget(out EntityId targetId)
        {
            targetId = default;
            var candidates = GetActivePlayerCandidates();
            if (candidates.Count == 0) return false;

            if (_hateList.SelectTarget(candidates, transform.position, out targetId))
            {
                return true;
            }

            // Fallback to closest candidate
            targetId = candidates[0].Id;
            return true;
        }

        private List<SpecterHateList.CandidateTarget> GetActivePlayerCandidates()
        {
            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                _disengageRange,
                _scanHits,
                _playerLayer);

            var list = new List<SpecterHateList.CandidateTarget>(count);
            var seen = new HashSet<EntityId>();

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _scanHits[i];
                if (col != null)
                {
                    PlayerCharacter player = col.GetComponentInParent<PlayerCharacter>();
                    if (player != null && player.IsAlive && seen.Add(player.Id))
                    {
                        list.Add(new SpecterHateList.CandidateTarget(player.Id, player.transform.position));
                    }
                }
            }

            return list;
        }

        private bool TryScanForPlayer(out PlayerCharacter detectedPlayer)
        {
            detectedPlayer = null;
            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                _detectionRange,
                _scanHits,
                _playerLayer);

            float closestDist = float.MaxValue;
            Vector2 myPos = transform.position;
            var seen = new HashSet<EntityId>();

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _scanHits[i];
                if (col != null)
                {
                    PlayerCharacter player = col.GetComponentInParent<PlayerCharacter>();
                    if (player != null && player.IsAlive && seen.Add(player.Id))
                    {
                        Vector2 playerPos = player.transform.position;
                        if (HasLineOfSight(myPos, playerPos))
                        {
                            float dist = Vector2.Distance(myPos, playerPos);
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                detectedPlayer = player;
                            }
                        }
                    }
                }
            }

            return detectedPlayer != null;
        }

        private bool TryGetPlayer(EntityId entityId, out PlayerCharacter player)
        {
            player = null;
            if (entityId.Value == 0) return false;

            if (_entityRegistry != null && _entityRegistry.TryGetCharacter(entityId, out ICharacter character) && character is PlayerCharacter pc)
            {
                player = pc;
                return true;
            }

            int count = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                _disengageRange + 5f,
                _scanHits,
                _playerLayer);

            for (int i = 0; i < count; i++)
            {
                Collider2D col = _scanHits[i];
                if (col != null)
                {
                    PlayerCharacter p = col.GetComponentInParent<PlayerCharacter>();
                    if (p != null && p.Id == entityId)
                    {
                        player = p;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            return !Physics2D.Linecast(from, to, _obstacleLayer) &&
                   !SmokeZoneRegistry.IsLineOccluded(from, to);
        }

        /// <summary>
        /// Toggles all physical movement and damage hitbox colliders according to incorporeity state (TASK-285).
        /// During Desvanecimiento and Desvanecido, all body colliders are disabled so projectiles pass through cleanly.
        /// </summary>
        private void SetCollidersActive(bool active)
        {
            if (_allColliders == null || _allColliders.Length == 0)
            {
                _allColliders = GetComponentsInChildren<Collider2D>(true);
            }

            for (int i = 0; i < _allColliders.Length; i++)
            {
                var col = _allColliders[i];
                if (col == null) continue;

                if (col.gameObject.name == "LootInteraction")
                {
                    col.enabled = (CurrentState == SpecterStateType.Muerte);
                }
                else
                {
                    col.enabled = active;
                }
            }
        }

        private void CacheDependencies()
        {
            if (_stun == null) _stun = GetComponent<EnemyStunNetworkEffect>();
            if (_character == null) _character = GetComponent<EnemyCharacter>();
            if (_movementMotor == null) _movementMotor = GetComponent<Kinematic2DMovementMotor>();
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody2D>();
            if (_collider == null) _collider = GetComponent<Collider2D>();
            _allColliders = GetComponentsInChildren<Collider2D>(true);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            CacheDependencies();
            if (_playerLayer == 0) _playerLayer = LayerMask.GetMask("Character");
            if (_obstacleLayer == 0) _obstacleLayer = LayerMask.GetMask("WorldCollision", "Obstacles");
        }
#endif
    }
}
