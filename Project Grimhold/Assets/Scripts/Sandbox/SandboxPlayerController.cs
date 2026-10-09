#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Dev-only scene controller that lets the sandbox UI customize the single local player live.
/// Every mutation is queued and applied by State Authority inside <see cref="FixedUpdateNetwork"/>;
/// any peer may request one. Nothing is added to the player prefab: the controller resolves the
/// avatar at runtime and drives existing controllers (plus the dev-guarded sandbox hooks on them).
/// </summary>
/// <remarks>
/// God mode refills Health every tick (no invulnerability flag exists), so a single burst larger than
/// Max Health can still Down the player. Infinite Mana refills every tick. Stamina is not supported:
/// <see cref="PlayerStaminaNetworkController"/> exposes no refill hook.
/// </remarks>
[DisallowMultipleComponent]
public sealed class SandboxPlayerController : NetworkBehaviour
{
    private enum RequestKind : byte
    {
        SetAbilitySlots = 1,
        ResetCooldowns = 2,
        SetEquipment = 3,
        SetHealth = 4,
        RefillHealth = 5,
        RefillMana = 6,
        SetGodMode = 7,
        SetInfiniteMana = 8,
        Teleport = 9,
        SetIgnoreSessionRules = 10,
        CastSlot = 11
    }

    private enum AttributeOpKind : byte
    {
        Adjust = 0,
        Reset = 1,
        ResetAll = 2
    }

    private readonly struct AttributeOp
    {
        public readonly AttributeOpKind Kind;
        public readonly CharacterAttribute Attribute;
        public readonly int Amount;

        public AttributeOp(AttributeOpKind kind, CharacterAttribute attribute = default, int amount = 0)
        {
            Kind = kind;
            Attribute = attribute;
            Amount = amount;
        }
    }

    private readonly struct Request
    {
        public readonly RequestKind Kind;
        public readonly int A;
        public readonly int B;
        public readonly float Value;
        public readonly Vector2 Position;

        public Request(RequestKind kind, int a = 0, int b = 0, float value = 0f, Vector2 position = default)
        {
            Kind = kind;
            A = a;
            B = b;
            Value = value;
            Position = position;
        }
    }

    private const int MaxPendingRequests = 32;
    private const int MaxPendingAttributeOps = 64;
    private const int MaxAttributeDispatchFailures = 120;

    [SerializeField] private AbilityDefinitionCatalog _abilityCatalog;
    [SerializeField] private LootDefinitionCatalog _lootCatalog;

    private readonly Queue<Request> _pending = new Queue<Request>();
    private readonly Queue<AttributeOp> _attributeOps = new Queue<AttributeOp>();
    private readonly List<int> _stepBuffer = new List<int>();
    private PlayerCharacter _player;
    private int _lastAttributeDispatchTick = -1;
    private int _attributeDispatchFailures;

    /// <summary>
    /// When true (the default, set by State Authority at spawn) the sandbox ignores session rules:
    /// cast-time attribute requirements are skipped. Resource, cooldown and aim rules still apply.
    /// </summary>
    [Networked] public NetworkBool IgnoreSessionRules { get; private set; }

    /// <summary>Outcome of the last sandbox cast processed on State Authority (not replicated).</summary>
    public AbilityActivationFailure LastCastFailure { get; private set; }

    /// <summary>Number of sandbox casts processed on State Authority (not replicated).</summary>
    public int CastCount { get; private set; }

    [Networked] public NetworkBool GodMode { get; private set; }
    [Networked] public NetworkBool InfiniteMana { get; private set; }

    /// <summary>Resolves the local player avatar (State Authority or Input Authority), if spawned.</summary>
    public bool TryGetPlayer(out PlayerCharacter player)
    {
        if (_player == null || _player.Object == null || !_player.Object.IsValid)
        {
            _player = null;
            foreach (PlayerCharacter candidate in FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
            {
                if (candidate.Object != null && candidate.Object.IsValid &&
                    (candidate.Object.HasStateAuthority || candidate.Object.HasInputAuthority))
                {
                    _player = candidate;
                    break;
                }
            }
        }

        player = _player;
        return player != null;
    }

    // ---- Attributes: thin facade over the existing runtime override controller -------------------

    // The override controller accepts only +/-1 and +/-5 and holds ONE pending request per tick, so any other
    // amount (or a second click in the same tick) used to be dropped silently. Requests are split into
    // supported steps and dispatched at most one per Fusion tick by Update.
    public bool RequestAttributeAdjustment(CharacterAttribute attribute, int amount)
    {
        _stepBuffer.Clear();
        if (!SandboxRuleBypass.TryDecomposeAdjustment(amount, _stepBuffer) ||
            _attributeOps.Count + _stepBuffer.Count > MaxPendingAttributeOps)
        {
            return false;
        }

        foreach (int step in _stepBuffer)
        {
            _attributeOps.Enqueue(new AttributeOp(AttributeOpKind.Adjust, attribute, step));
        }

        return true;
    }

    public bool RequestAttributeReset(CharacterAttribute attribute) =>
        EnqueueAttributeOp(new AttributeOp(AttributeOpKind.Reset, attribute));

    public bool RequestAttributeResetAll() => EnqueueAttributeOp(new AttributeOp(AttributeOpKind.ResetAll));

    private bool EnqueueAttributeOp(in AttributeOp op)
    {
        if (_attributeOps.Count >= MaxPendingAttributeOps)
        {
            return false;
        }

        _attributeOps.Enqueue(op);
        return true;
    }

    private void Update()
    {
        if (_attributeOps.Count == 0 || Object == null || !Object.IsValid || Runner == null)
        {
            return;
        }

        int tick = Runner.Tick.Raw;
        if (tick == _lastAttributeDispatchTick)
        {
            return;
        }

        if (!TryGetAttributeOverrides(out var overrides))
        {
            DropAttributeOpsAfterRepeatedFailure();
            return;
        }

        AttributeOp op = _attributeOps.Peek();
        bool accepted;
        switch (op.Kind)
        {
            case AttributeOpKind.Adjust: accepted = overrides.RequestAdjustment(op.Attribute, op.Amount); break;
            case AttributeOpKind.Reset: accepted = overrides.RequestReset(op.Attribute); break;
            default: accepted = overrides.RequestResetAll(); break;
        }

        if (accepted)
        {
            _attributeOps.Dequeue();
            _attributeDispatchFailures = 0;
            _lastAttributeDispatchTick = tick;
        }
        else
        {
            DropAttributeOpsAfterRepeatedFailure();
        }
    }

    private void DropAttributeOpsAfterRepeatedFailure()
    {
        if (++_attributeDispatchFailures < MaxAttributeDispatchFailures)
        {
            return;
        }

        Debug.LogWarning($"{nameof(SandboxPlayerController)}: dropped attribute requests (override controller unavailable).", this);
        _attributeOps.Clear();
        _attributeDispatchFailures = 0;
    }

    // ---- Requests (any peer) ---------------------------------------------------------------------

    /// <summary>Sets Slot1/Slot2 by <see cref="AbilityDefinitionCatalog.Definitions"/> index; negative clears.</summary>
    public bool RequestAbilitySlots(int slot1CatalogIndex, int slot2CatalogIndex) =>
        Submit(new Request(RequestKind.SetAbilitySlots, slot1CatalogIndex, slot2CatalogIndex));

    /// <summary>Starts Slot1 (1) or Slot2 (2) through the real activation path on State Authority.</summary>
    public bool RequestCast(int slot) =>
        (slot == 1 || slot == 2) && Submit(new Request(RequestKind.CastSlot, slot));

    public bool RequestResetCooldowns() => Submit(new Request(RequestKind.ResetCooldowns));

    /// <summary>Equips a <see cref="LootDefinitionCatalog"/> item (by lootId) into a slot; unknown id rejects.</summary>
    public bool RequestEquip(LootId lootId, EquipmentSlot slot)
    {
        if (_lootCatalog == null || !_lootCatalog.TryGetIndex(lootId, out int index))
        {
            return false;
        }

        return Submit(new Request(RequestKind.SetEquipment, index, (int)slot));
    }

    public bool RequestUnequip(EquipmentSlot slot) =>
        Submit(new Request(RequestKind.SetEquipment, -1, (int)slot));

    public bool RequestSetHealth(float health) => Submit(new Request(RequestKind.SetHealth, value: health));
    public bool RequestRefillHealth() => Submit(new Request(RequestKind.RefillHealth));
    public bool RequestRefillMana() => Submit(new Request(RequestKind.RefillMana));
    public bool RequestSetIgnoreSessionRules(bool enabled) =>
        Submit(new Request(RequestKind.SetIgnoreSessionRules, enabled ? 1 : 0));
    public bool RequestSetGodMode(bool enabled) => Submit(new Request(RequestKind.SetGodMode, enabled ? 1 : 0));
    public bool RequestSetInfiniteMana(bool enabled) =>
        Submit(new Request(RequestKind.SetInfiniteMana, enabled ? 1 : 0));
    public bool RequestTeleport(Vector2 position) =>
        Submit(new Request(RequestKind.Teleport, position: position));

    // ---- Transport -------------------------------------------------------------------------------

    private bool Submit(in Request request)
    {
        if (Object == null || !Object.IsValid)
        {
            Debug.LogError($"{nameof(SandboxPlayerController)} is not spawned.", this);
            return false;
        }

        if (HasStateAuthority)
        {
            return Enqueue(request);
        }

        RPC_Submit((byte)request.Kind, request.A, request.B, request.Value, request.Position);
        return true;
    }

    // Any peer may call: the controller is a scene object, so no peer holds Input Authority on it.
    // Dev-only; the request is re-validated on the State Authority.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_Submit(byte kind, int a, int b, float value, Vector2 position)
    {
        if (!Enum.IsDefined(typeof(RequestKind), kind))
        {
            Debug.LogError($"{nameof(SandboxPlayerController)} rejected an unknown request kind.", this);
            return;
        }

        Enqueue(new Request((RequestKind)kind, a, b, value, position));
    }

    private bool Enqueue(in Request request)
    {
        if (_pending.Count >= MaxPendingRequests)
        {
            Debug.LogWarning($"{nameof(SandboxPlayerController)} dropped a request: queue is full.", this);
            return false;
        }

        _pending.Enqueue(request);
        return true;
    }

    // ---- Authority simulation --------------------------------------------------------------------

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            IgnoreSessionRules = true; // Sandbox default: no session rules.
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward || !TryGetPlayer(out PlayerCharacter player) ||
            !player.Object.HasStateAuthority)
        {
            return;
        }

        while (_pending.Count > 0)
        {
            Apply(_pending.Dequeue(), player);
        }

        if (player.TryGetComponent(out PlayerAbilityRuntimeNetworkController abilityRuntime))
        {
            abilityRuntime.SandboxSetIgnoreSessionRules(IgnoreSessionRules);
        }

        if (GodMode)
        {
            RefillHealth(player);
        }

        if (InfiniteMana)
        {
            RefillMana(player);
        }
    }

    private void Apply(in Request request, PlayerCharacter player)
    {
        switch (request.Kind)
        {
            case RequestKind.SetAbilitySlots: ApplyAbilitySlots(request.A, request.B, player); break;
            case RequestKind.ResetCooldowns:
                if (player.TryGetComponent(out PlayerAbilityRuntimeNetworkController cooldowns) &&
                    !cooldowns.SandboxResetCooldowns())
                {
                    Debug.LogWarning($"{nameof(SandboxPlayerController)}: ability runtime is not initialized.", this);
                }
                break;
            case RequestKind.CastSlot: ApplyCast(request.A, player); break;
            case RequestKind.SetEquipment: ApplyEquipment(request.A, request.B, player); break;
            case RequestKind.SetHealth: ApplyHealth(request.Value, player); break;
            case RequestKind.RefillHealth: RefillHealth(player); break;
            case RequestKind.RefillMana: RefillMana(player); break;
            case RequestKind.SetIgnoreSessionRules: IgnoreSessionRules = request.A != 0; break;
            case RequestKind.SetGodMode: GodMode = request.A != 0; break;
            case RequestKind.SetInfiniteMana: InfiniteMana = request.A != 0; break;
            case RequestKind.Teleport: ApplyTeleport(request.Position, player); break;
        }
    }

    private void ApplyCast(int slotNumber, PlayerCharacter player)
    {
        if ((slotNumber != 1 && slotNumber != 2) ||
            !player.TryGetComponent(out PlayerAbilityRuntimeNetworkController runtime))
        {
            Debug.LogError($"{nameof(SandboxPlayerController)} rejected a cast request.", this);
            return;
        }

        UniversalAbilitySlot slot = slotNumber == 1 ? UniversalAbilitySlot.Slot1 : UniversalAbilitySlot.Slot2;
        LastCastFailure = runtime.SandboxCast(slot);
        CastCount++;
    }

    private void ApplyAbilitySlots(int index1, int index2, PlayerCharacter player)
    {
        if (_abilityCatalog == null || !player.TryGetComponent(out PlayerAbilityRuntimeNetworkController runtime) ||
            !TryResolveAbilityId(index1, out AbilityId slot1) || !TryResolveAbilityId(index2, out AbilityId slot2))
        {
            Debug.LogError($"{nameof(SandboxPlayerController)} rejected an ability slot request.", this);
            return;
        }

        if (!runtime.SandboxOverrideSlots(slot1, slot2, out string error))
        {
            Debug.LogWarning($"{nameof(SandboxPlayerController)}: ability slots rejected. {error}", this);
        }
    }

    private bool TryResolveAbilityId(int index, out AbilityId id)
    {
        id = default;
        if (index < 0)
        {
            return true;
        }

        IReadOnlyList<AbilityDefinition> definitions = _abilityCatalog.Definitions;
        if (index >= definitions.Count || definitions[index] == null)
        {
            return false;
        }

        id = definitions[index].AbilityId;
        return id.IsValid;
    }

    private void ApplyEquipment(int catalogIndex, int slotValue, PlayerCharacter player)
    {
        if (!EquipmentSlotRules.IsValidSlotValue(slotValue) ||
            !player.TryGetComponent(out PlayerWeaponEquipmentNetworkController equipment))
        {
            Debug.LogError($"{nameof(SandboxPlayerController)} rejected an equipment request.", this);
            return;
        }

        if (!equipment.SandboxSetEquipment(catalogIndex, (EquipmentSlot)slotValue, out string error))
        {
            Debug.LogWarning($"{nameof(SandboxPlayerController)}: equipment rejected. {error}", this);
        }
    }

    private void ApplyHealth(float requested, PlayerCharacter player)
    {
        if (!SandboxLoadoutRules.TryClampValue(
                requested, SandboxLoadoutRules.MinimumSetHealth, player.MaxHealth, out float target))
        {
            Debug.LogWarning($"{nameof(SandboxPlayerController)}: health {requested} is out of range.", this);
            return;
        }

        float delta = target - player.Health;
        if (delta > 0f)
        {
            player.ApplyHealing(new HealRequest(delta));
        }
        else if (delta < 0f)
        {
            // True damage bypasses mitigation, so the result is exact; target >= 1 never kills.
            player.ApplyDamage(new DamageRequest(
                player.Id, player.Id, -delta, DamageType.TrueDamage, Vector2.zero, Vector2.zero, Runner.Tick));
        }
    }

    private static void RefillHealth(PlayerCharacter player)
    {
        float missing = player.MaxHealth - player.Health;
        if (missing > 0f)
        {
            player.ApplyHealing(new HealRequest(missing));
        }
    }

    private static void RefillMana(PlayerCharacter player)
    {
        if (!player.TryGetComponent(out PlayerManaNetworkController mana) || !mana.TryGetMaximumMana(out float maximum))
        {
            return;
        }

        float missing = maximum - mana.CurrentMana;
        if (missing > 0f)
        {
            mana.TryRestore(missing);
        }
    }

    private void ApplyTeleport(Vector2 position, PlayerCharacter player)
    {
        if (!float.IsFinite(position.x) || !float.IsFinite(position.y) ||
            !player.TryGetComponent(out NetworkTransform networkTransform))
        {
            Debug.LogError($"{nameof(SandboxPlayerController)} rejected a teleport request.", this);
            return;
        }

        networkTransform.Teleport(new Vector3(position.x, position.y, player.transform.position.z));
    }

    private bool TryGetAttributeOverrides(out RuntimeAttributeOverrideNetworkController overrides)
    {
        overrides = null;
        return TryGetPlayer(out PlayerCharacter player) &&
            player.TryGetComponent(out RaidAvatarParticipantLink link) &&
            link.TryResolveParticipant(out NetworkRaidParticipant participant) &&
            participant.TryGetComponent(out overrides);
    }
}
#endif
