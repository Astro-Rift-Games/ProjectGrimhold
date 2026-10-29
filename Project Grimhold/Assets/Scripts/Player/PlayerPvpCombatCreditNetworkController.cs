using System.Collections.Generic;
using Fusion;
using UnityEngine;

/// <summary>
/// Freezes provisional PvP credit at Downed entry. Only definitive defeat of that same cycle
/// confirms rewards; recovery cancels them. Stable participant ids survive avatar reference remapping.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerPvpCombatCreditNetworkController : NetworkBehaviour, IDownedCycleObserver
{
    private const float GrantRetryIntervalSeconds = 1f;

    [SerializeField, Min(0)] private long _pvpBaseExperience = 25;

    // Requested limit 1 (GD 05 §19 table). GD 19 §6.2 grants every eligible teammate; 0 keeps that unlimited option.
    [SerializeField, Min(0), Tooltip("Maximum Assists per Downed cycle. 0 = unlimited (every eligible teammate).")]
    private int _maxAssistsPerTarget = 1;

    [Networked] public int CreditCycle { get; private set; }
    [Networked] public int LastHitParticipantValue { get; private set; }
    [Networked] public int AssistMask { get; private set; }
    [Networked] public NetworkBool IsConfirmed { get; private set; }
    [Networked] public int PendingGrantMask { get; private set; }
    [Networked] private TickTimer GrantRetryTimer { get; set; }
    [Networked, Capacity(RaidSessionRules.MaxParticipants)]
    public NetworkArray<int> SupportReceivedTicks { get; }
    [Networked] private int SupportReceivedKindBits { get; set; }

    private PlayerCharacter _character;
    private RaidAvatarParticipantLink _participantLink;
    private CombatContributionTracker _tracker;
    private EntityRegistry _registry;
    private NetworkSpawnManager _spawnManager;
    private bool _configurationValid;
    private readonly HashSet<RaidParticipantId> _contributors = new();
    // Latest in-window contribution or support tick per candidate participant; 0 = not a candidate.
    private readonly int[] _candidateTicks = new int[RaidSessionRules.MaxParticipants];
    private readonly List<PlayerPvpCombatCreditNetworkController> _supportRecipients = new(RaidSessionRules.MaxParticipants);

    public override void Spawned()
    {
        _character = GetComponent<PlayerCharacter>();
        _participantLink = GetComponent<RaidAvatarParticipantLink>();
        _tracker = GetComponent<CombatContributionTracker>();
        _registry = Runner.GetComponent<EntityRegistry>();
        _spawnManager = Runner.GetComponent<NetworkSpawnManager>();
        _configurationValid = _character != null && _participantLink != null && _tracker != null && _pvpBaseExperience >= 0;
        if (!_configurationValid)
        {
            Debug.LogError($"{nameof(PlayerPvpCombatCreditNetworkController)} requires co-located " +
                $"{nameof(PlayerCharacter)}, {nameof(RaidAvatarParticipantLink)}, {nameof(CombatContributionTracker)} " +
                "and non-negative PvP Experience.", this);
        }

        if (HasStateAuthority && !HostMigrationRestoreUtility.IsRestoreSpawn(this))
        {
            CreditCycle = 0;
            ClearCredit();
            for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
            {
                SupportReceivedTicks.Set(i, 0);
            }

            SupportReceivedKindBits = 0;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority || !Runner.IsForward || PendingGrantMask == 0 ||
            !GrantRetryTimer.ExpiredOrNotRunning(Runner)) return;
        TryGrantPendingRewards();
    }

    public void RecordSupportReceived(RaidParticipantId supporter, int tick)
    {
        if (!HasStateAuthority || !supporter.IsValid) return;
        SupportReceivedTicks.Set(supporter.Value - 1, tick);
        SupportReceivedKindBits = CombatContributionTracker.WriteKind(
            SupportReceivedKindBits, supporter.Value - 1, CombatContributionKind.Healing);
    }

    public bool TryGetLatestSupportReceived(RaidParticipantId supporter, out int tick, out CombatContributionKind kind)
    {
        tick = 0;
        kind = CombatContributionKind.None;
        if (Object == null || !Object.IsValid || !supporter.IsValid) return false;
        tick = SupportReceivedTicks.Get(supporter.Value - 1);
        kind = CombatContributionTracker.ReadKind(SupportReceivedKindBits, supporter.Value - 1);
        return tick > 0 && kind != CombatContributionKind.None;
    }

    public void NotifyDownedEntered(int cycle, EntityId causerId, int simulationTick)
    {
        if (!HasStateAuthority || !_configurationValid) return;
        CreditCycle = cycle;
        ClearCredit();
        _contributors.Clear();
        System.Array.Clear(_candidateTicks, 0, _candidateTicks.Length);

        // Unlinked/direct-development avatars have no Raid roster and cannot produce PvP credit.
        if (_registry != null && _spawnManager != null &&
            _participantLink.TryResolveParticipant(out NetworkRaidParticipant target) &&
            target.RaidParticipantId.IsValid &&
            _registry.TryGetDamageable(causerId, out IDamageable source) &&
            source is PlayerCharacter attacker &&
            TryResolveSourceParticipant(attacker, out NetworkRaidParticipant lastHitter) &&
            _spawnManager.TryGetRaidInitialAffiliations(out RaidInitialAffiliationSnapshot affiliations) &&
            affiliations.TryAreInitialTeammates(lastHitter.RaidParticipantId, target.RaidParticipantId, out bool allies) &&
            !allies)
        {
            RaidParticipantId lastHitId = lastHitter.RaidParticipantId;
            LastHitParticipantValue = lastHitId.Value;
            _tracker.GetValidContributors(simulationTick, _contributors);
            foreach (RaidParticipantId contributor in _contributors)
            {
                if (_tracker.TryGetLatestContribution(contributor, out int tick, out _)) AddCandidate(contributor, tick);
            }

            CollectSupporters(lastHitId, simulationTick);
            foreach (RaidParticipantId contributor in _contributors) CollectSupporters(contributor, simulationTick);

            int eligibleMask = 0;
            for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
            {
                if (_candidateTicks[i] > 0 && RaidParticipantId.TryCreate(i + 1, out RaidParticipantId candidate) &&
                    candidate != lastHitId && candidate != target.RaidParticipantId &&
                    affiliations.TryAreInitialTeammates(candidate, lastHitId, out bool teammate) && teammate)
                {
                    eligibleMask |= 1 << i;
                }
            }

            AssistMask = SelectAssistMask(eligibleMask, _candidateTicks, _maxAssistsPerTarget);
        }

        // Only credits reset per cycle. Effective Active contributions remain eligible until their window expires.
        _contributors.Clear();
        System.Array.Clear(_candidateTicks, 0, _candidateTicks.Length);
    }

    /// <summary>
    /// Keeps at most <paramref name="maxAssists"/> eligible bits (0 = all). Deterministic choice: the most recent
    /// in-window contribution before Downed wins; a tie on the same tick goes to the lowest participant id.
    /// </summary>
    internal static int SelectAssistMask(int eligibleMask, int[] candidateTicks, int maxAssists)
    {
        if (maxAssists <= 0) return eligibleMask;

        int selected = 0;
        for (int picked = 0; picked < maxAssists; picked++)
        {
            int best = -1;
            for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
            {
                int bit = 1 << i;
                if ((eligibleMask & bit) == 0 || (selected & bit) != 0) continue;
                if (best < 0 || candidateTicks[i] > candidateTicks[best]) best = i;
            }

            if (best < 0) break;
            selected |= 1 << best;
        }

        return selected;
    }

    private void AddCandidate(RaidParticipantId participant, int tick)
    {
        int index = participant.Value - 1;
        if (tick > _candidateTicks[index]) _candidateTicks[index] = tick;
    }

    public void NotifyDownedCycleDefeated(int cycle)
    {
        if (!HasStateAuthority || cycle != CreditCycle || LastHitParticipantValue == 0 || IsConfirmed) return;
        IsConfirmed = true;
        PendingGrantMask = (1 << (LastHitParticipantValue - 1)) | AssistMask;
        TryGrantPendingRewards();
    }

    public void NotifyDownedCycleRecovered(int cycle)
    {
        if (!HasStateAuthority || cycle != CreditCycle) return;
        ClearCredit();
    }

    /// <summary>Original source identity survives Downed, Defeat and loss of current-avatar control.</summary>
    internal static bool TryResolveSourceParticipant(PlayerCharacter character, out NetworkRaidParticipant participant)
    {
        participant = null;
        return character != null && character.TryGetComponent(out RaidAvatarParticipantLink link) &&
            link.TryResolveParticipant(out participant) && participant.RaidParticipantId.IsValid;
    }

    private void CollectSupporters(RaidParticipantId recipient, int simulationTick)
    {
        if (!_spawnManager.TryGetRaidParticipant(recipient, out NetworkRaidParticipant participant)) return;
        if (participant.TryResolveCurrentAvatar(out NetworkObject avatar) &&
            avatar.TryGetComponent(out PlayerPvpCombatCreditNetworkController credit))
        {
            CollectSupporters(credit, simulationTick);
            return;
        }

        // Defeat clears CurrentAvatarId but leaves the original body spawned. Do not erase effective
        // support because of that lifecycle transition. This runner-scoped fallback runs only at entry,
        // never in FixedUpdateNetwork, and retains no object references in the replicated credit.
        _supportRecipients.Clear();
        Runner.GetAllBehaviours(_supportRecipients);
        for (int i = 0; i < _supportRecipients.Count; i++)
        {
            PlayerPvpCombatCreditNetworkController candidate = _supportRecipients[i];
            if (candidate != null && candidate.Object != null && candidate.Object.IsValid &&
                candidate._participantLink != null && candidate._participantLink.TryResolveParticipant(out var owner) &&
                owner.RaidParticipantId == recipient) CollectSupporters(candidate, simulationTick);
        }
        _supportRecipients.Clear();
    }

    private void CollectSupporters(PlayerPvpCombatCreditNetworkController credit, int simulationTick)
    {
        for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
        {
            int tick = credit.SupportReceivedTicks.Get(i);
            int elapsed = simulationTick - tick;
            if (tick > 0 && elapsed >= 0 && elapsed <= _tracker.AssistWindowTicks &&
                RaidParticipantId.TryCreate(i + 1, out RaidParticipantId supporter)) AddCandidate(supporter, tick);
        }
    }

    private void TryGrantPendingRewards()
    {
        for (int i = 0; i < RaidSessionRules.MaxParticipants; i++)
        {
            int bit = 1 << i;
            if ((PendingGrantMask & bit) == 0 || !RaidParticipantId.TryCreate(i + 1, out RaidParticipantId id) ||
                _spawnManager == null || !_spawnManager.TryGetRaidParticipant(id, out NetworkRaidParticipant participant)) continue;

            bool isLastHit = id.Value == LastHitParticipantValue;
            long amount = isLastHit ? _pvpBaseExperience : _pvpBaseExperience / 2;
            if (participant.State != RaidParticipantState.Raiding || amount <= 0)
            {
                // Keep the confirmed relationship, but terminal participants cannot earn new XP.
                PendingGrantMask &= ~bit;
                continue;
            }

            PlayerExpeditionExperienceLedger ledger = participant.ExperienceLedger;
            if (ledger != null && ledger.TryRegisterNormalReward(
                    isLastHit ? ExpeditionExperienceSource.PvpKill : ExpeditionExperienceSource.PvpAssist, amount, out _))
            {
                // Ledger acceptance precedes consumption, synchronously in the same authoritative tick.
                PendingGrantMask &= ~bit;
            }
        }
        GrantRetryTimer = PendingGrantMask == 0 ? TickTimer.None : TickTimer.CreateFromSeconds(Runner, GrantRetryIntervalSeconds);
    }

    private void ClearCredit()
    {
        LastHitParticipantValue = 0;
        AssistMask = 0;
        IsConfirmed = false;
        PendingGrantMask = 0;
        GrantRetryTimer = TickTimer.None;
    }
}
