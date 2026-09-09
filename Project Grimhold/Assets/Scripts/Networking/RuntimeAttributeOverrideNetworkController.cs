using Fusion;
using UnityEngine;

/// <summary>
/// State-Authority-owned, session-only testing offsets for one Raid participant.
/// The replicated state is never passed to profile or backend persistence.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkRaidParticipant))]
public sealed class RuntimeAttributeOverrideNetworkController : NetworkBehaviour
{
    private enum RequestKind : byte
    {
        None = 0,
        Adjust = 1,
        ResetAttribute = 2,
        ResetAll = 3,
        SetState = 4
    }

    [Networked] private int VitalityOffset { get; set; }
    [Networked] private int ResistanceOffset { get; set; }
    [Networked] private int StrengthOffset { get; set; }
    [Networked] private int DexterityOffset { get; set; }
    [Networked] private int IntelligenceOffset { get; set; }
    [Networked] private int LuckOffset { get; set; }
    [Networked] private int Revision { get; set; }

    private NetworkRaidParticipant _participant;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private RequestKind _pendingKind;
    private CharacterAttribute _pendingAttribute;
    private int _pendingAmount;
    private RuntimeAttributeOverrideState _pendingState;
    private RuntimeAttributeOverrideSession _localSession;
    private bool _localSessionSyncRequested;
#endif

    public int ObservedRevision => Object != null && Object.IsValid ? Revision : 0;

    private void Awake()
    {
        _participant = GetComponent<NetworkRaidParticipant>();
    }

    public override void Spawned()
    {
        _participant ??= GetComponent<NetworkRaidParticipant>();
        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            Commit(default, advanceRevision: false);
        }
    }

    public bool TryGetEffectiveState(
        in CharacterAttributeState persistent,
        out CharacterAttributeState effective) =>
        BuildState().TryApply(persistent, out effective);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public override void FixedUpdateNetwork()
    {
        TrySynchronizeLocalSession();
        if (!HasStateAuthority || _pendingKind == RequestKind.None)
        {
            return;
        }

        RequestKind kind = _pendingKind;
        CharacterAttribute attribute = _pendingAttribute;
        int amount = _pendingAmount;
        _pendingKind = RequestKind.None;

        if (_participant == null ||
            !_participant.TryGetPersistentCharacterAttributeState(out CharacterAttributeState persistent))
        {
            return;
        }

        RuntimeAttributeOverrideState current = BuildState();
        RuntimeAttributeOverrideState candidate;
        switch (kind)
        {
            case RequestKind.Adjust:
                if (!current.TryAdjust(attribute, amount, persistent, out candidate))
                {
                    return;
                }
                break;
            case RequestKind.ResetAttribute:
                candidate = current.Reset(attribute);
                break;
            case RequestKind.ResetAll:
                candidate = current.ResetAll();
                break;
            case RequestKind.SetState:
                candidate = _pendingState;
                if (!candidate.TryApply(persistent, out _))
                {
                    return;
                }
                break;
            default:
                return;
        }

        if (!candidate.Equals(current))
        {
            Commit(candidate, advanceRevision: true);
        }
    }

    public bool RequestAdjustment(CharacterAttribute attribute, int amount)
    {
        if (!IsSupportedAdjustment(amount) || !IsKnownAttribute(attribute) ||
            Object == null || !Object.IsValid)
        {
            return false;
        }

        if (HasStateAuthority)
        {
            return TryQueue(RequestKind.Adjust, attribute, amount);
        }

        if (!HasInputAuthority)
        {
            return false;
        }

        RPC_RequestAdjustment((int)attribute, amount);
        return true;
    }

    public bool RequestReset(CharacterAttribute attribute)
    {
        if (!IsKnownAttribute(attribute) || Object == null || !Object.IsValid)
        {
            return false;
        }

        if (HasStateAuthority)
        {
            return TryQueue(RequestKind.ResetAttribute, attribute, 0);
        }

        if (!HasInputAuthority)
        {
            return false;
        }

        RPC_RequestReset((int)attribute);
        return true;
    }

    public bool RequestResetAll()
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        if (HasStateAuthority)
        {
            return TryQueue(RequestKind.ResetAll, default, 0);
        }

        if (!HasInputAuthority)
        {
            return false;
        }

        RPC_RequestResetAll();
        return true;
    }

    public bool RequestState(in RuntimeAttributeOverrideState state)
    {
        if (Object == null || !Object.IsValid)
        {
            return false;
        }

        if (HasStateAuthority)
        {
            return TryQueueState(state);
        }

        if (!HasInputAuthority)
        {
            return false;
        }

        RPC_RequestState(
            state.Vitality,
            state.Resistance,
            state.Strength,
            state.Dexterity,
            state.Intelligence,
            state.Luck);
        return true;
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestAdjustment(int attributeValue, int amount)
    {
        CharacterAttribute attribute = (CharacterAttribute)attributeValue;
        if (IsKnownAttribute(attribute) && IsSupportedAdjustment(amount))
        {
            TryQueue(RequestKind.Adjust, attribute, amount);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestReset(int attributeValue)
    {
        CharacterAttribute attribute = (CharacterAttribute)attributeValue;
        if (IsKnownAttribute(attribute))
        {
            TryQueue(RequestKind.ResetAttribute, attribute, 0);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestResetAll()
    {
        TryQueue(RequestKind.ResetAll, default, 0);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestState(
        int vitality,
        int resistance,
        int strength,
        int dexterity,
        int intelligence,
        int luck)
    {
        TryQueueState(new RuntimeAttributeOverrideState(
            vitality,
            resistance,
            strength,
            dexterity,
            intelligence,
            luck));
    }

    private bool TryQueue(RequestKind kind, CharacterAttribute attribute, int amount)
    {
        if (_pendingKind != RequestKind.None)
        {
            return false;
        }

        _pendingKind = kind;
        _pendingAttribute = attribute;
        _pendingAmount = amount;
        return true;
    }

    private bool TryQueueState(in RuntimeAttributeOverrideState state)
    {
        if (_pendingKind != RequestKind.None && _pendingKind != RequestKind.SetState)
        {
            return false;
        }

        _pendingKind = RequestKind.SetState;
        _pendingState = state;
        return true;
    }

    private void TrySynchronizeLocalSession()
    {
        if (_localSessionSyncRequested || !HasInputAuthority || _participant == null ||
            !_participant.TryGetPersistentCharacterAttributeState(out _))
        {
            return;
        }

        ApplicationStashContext context = FindAnyObjectByType<ApplicationStashContext>();
        _localSession = context?.RuntimeAttributeOverrides;
        if (_localSession == null)
        {
            return;
        }

        _localSessionSyncRequested = RequestState(_localSession.State);
    }

    private static bool IsSupportedAdjustment(int amount) =>
        amount == -5 || amount == -1 || amount == 1 || amount == 5;

    private static bool IsKnownAttribute(CharacterAttribute attribute) =>
        attribute >= CharacterAttribute.Vitality && attribute <= CharacterAttribute.Luck;
#endif

    private RuntimeAttributeOverrideState BuildState() => new(
        VitalityOffset,
        ResistanceOffset,
        StrengthOffset,
        DexterityOffset,
        IntelligenceOffset,
        LuckOffset);

    private void Commit(in RuntimeAttributeOverrideState state, bool advanceRevision)
    {
        VitalityOffset = state.Vitality;
        ResistanceOffset = state.Resistance;
        StrengthOffset = state.Strength;
        DexterityOffset = state.Dexterity;
        IntelligenceOffset = state.Intelligence;
        LuckOffset = state.Luck;
        if (advanceRevision)
        {
            Revision = unchecked(Revision + 1);
        }
    }
}
