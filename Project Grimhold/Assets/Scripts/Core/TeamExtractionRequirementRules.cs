using System;
using System.Collections.Generic;

/// <summary>
/// Status snapshot of a single team member for team extraction evaluations.
/// </summary>
public readonly struct TeamMemberExtractionSnapshot
{
    public RaidParticipantId ParticipantId { get; }
    public RaidParticipantState State { get; }
    public bool IsConnected { get; }
    public bool IsInsideExact { get; }
    public bool IsInsideTolerant { get; }

    public TeamMemberExtractionSnapshot(
        RaidParticipantId participantId,
        RaidParticipantState state,
        bool isConnected,
        bool isInsideExact,
        bool isInsideTolerant)
    {
        ParticipantId = participantId;
        State = state;
        IsConnected = isConnected;
        IsInsideExact = isInsideExact;
        IsInsideTolerant = isInsideTolerant;
    }

    /// <summary>
    /// A member is required if they are in the active/recoverable expedition set (State == Raiding).
    /// Disconnection does not remove them from the required composition.
    /// Defeated, Aborted, and Extracted participants are excluded.
    /// </summary>
    public bool IsRequiredMember => State == RaidParticipantState.Raiding;
}

/// <summary>
/// Identifies the specific reason preventing team extraction from starting or continuing.
/// </summary>
public enum TeamExtractionCancelReason
{
    None = 0,
    RitualNotCompleted = 1,
    NoRequiredMembers = 2,
    RequiredMemberDisconnected = 3,
    RequiredMemberOutsideArea = 4,
    CompositionChanged = 5
}

/// <summary>
/// Deterministic signature representing the exact composition of required members in a team.
/// </summary>
public readonly struct TeamCompositionSignature : IEquatable<TeamCompositionSignature>
{
    public int Value { get; }
    public bool IsValid => Value != 0;

    public TeamCompositionSignature(int value) => Value = value;

    public bool Equals(TeamCompositionSignature other) => Value == other.Value;
    public override bool Equals(object obj) => obj is TeamCompositionSignature other && Equals(other);
    public override int GetHashCode() => Value;
    public override string ToString() => Value.ToString();

    public static bool operator ==(TeamCompositionSignature left, TeamCompositionSignature right) => left.Equals(right);
    public static bool operator !=(TeamCompositionSignature left, TeamCompositionSignature right) => !left.Equals(right);
}

/// <summary>
/// Evaluation output for team extraction start and continuation queries.
/// </summary>
public readonly struct TeamExtractionEvaluationResult
{
    public bool CanStart { get; }
    public bool ShouldContinue { get; }
    public TeamExtractionCancelReason CancelReason { get; }
    public TeamCompositionSignature Signature { get; }
    public int RequiredMemberCount { get; }
    public int ConnectedRequiredCount { get; }
    public int PresentRequiredCount { get; }

    public TeamExtractionEvaluationResult(
        bool canStart,
        bool shouldContinue,
        TeamExtractionCancelReason cancelReason,
        TeamCompositionSignature signature,
        int requiredMemberCount,
        int connectedRequiredCount,
        int presentRequiredCount)
    {
        CanStart = canStart;
        ShouldContinue = shouldContinue;
        CancelReason = cancelReason;
        Signature = signature;
        RequiredMemberCount = requiredMemberCount;
        ConnectedRequiredCount = connectedRequiredCount;
        PresentRequiredCount = presentRequiredCount;
    }
}

/// <summary>
/// Pure deterministic rules for team extraction countdown start, continuation, cancellation, and restart.
/// Enforces requirements from Game Design doc 06 §15-16, doc 02 §15, and decisions D-5, D-6, A-4.
/// </summary>
public static class TeamExtractionRequirementRules
{
    /// <summary>
    /// Computes a deterministic composition signature from the required members in the given snapshot list.
    /// </summary>
    public static TeamCompositionSignature ComputeSignature(IReadOnlyList<TeamMemberExtractionSnapshot> members)
    {
        if (members == null || members.Count == 0)
        {
            return default;
        }

        // Sort required participant IDs to guarantee stable signature
        var requiredIds = new List<int>();
        for (int i = 0; i < members.Count; i++)
        {
            TeamMemberExtractionSnapshot m = members[i];
            if (m.IsRequiredMember && m.ParticipantId.IsValid)
            {
                requiredIds.Add(m.ParticipantId.Value);
            }
        }

        if (requiredIds.Count == 0)
        {
            return default;
        }

        requiredIds.Sort();
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < requiredIds.Count; i++)
            {
                hash = hash * 31 + requiredIds[i];
            }
            return new TeamCompositionSignature(hash != 0 ? hash : 1);
        }
    }

    /// <summary>
    /// Evaluates whether a team can initiate the extraction countdown in a sanctuary zone.
    /// </summary>
    public static TeamExtractionEvaluationResult EvaluateStart(
        bool isRitualCompleted,
        IReadOnlyList<TeamMemberExtractionSnapshot> members)
    {
        if (!isRitualCompleted)
        {
            return new TeamExtractionEvaluationResult(
                canStart: false,
                shouldContinue: false,
                cancelReason: TeamExtractionCancelReason.RitualNotCompleted,
                signature: default,
                requiredMemberCount: 0,
                connectedRequiredCount: 0,
                presentRequiredCount: 0);
        }

        TeamCompositionSignature signature = ComputeSignature(members);
        if (!signature.IsValid)
        {
            return new TeamExtractionEvaluationResult(
                canStart: false,
                shouldContinue: false,
                cancelReason: TeamExtractionCancelReason.NoRequiredMembers,
                signature: default,
                requiredMemberCount: 0,
                connectedRequiredCount: 0,
                presentRequiredCount: 0);
        }

        int requiredCount = 0;
        int connectedCount = 0;
        int presentCount = 0;
        var firstBlockReason = TeamExtractionCancelReason.None;

        for (int i = 0; i < members.Count; i++)
        {
            TeamMemberExtractionSnapshot m = members[i];
            if (!m.IsRequiredMember)
            {
                continue;
            }

            requiredCount++;

            if (m.IsConnected)
            {
                connectedCount++;
            }
            else if (firstBlockReason == TeamExtractionCancelReason.None)
            {
                firstBlockReason = TeamExtractionCancelReason.RequiredMemberDisconnected;
            }

            if (m.IsInsideExact)
            {
                presentCount++;
            }
            else if (firstBlockReason == TeamExtractionCancelReason.None)
            {
                firstBlockReason = TeamExtractionCancelReason.RequiredMemberOutsideArea;
            }
        }

        bool canStart = firstBlockReason == TeamExtractionCancelReason.None;
        return new TeamExtractionEvaluationResult(
            canStart: canStart,
            shouldContinue: canStart,
            cancelReason: firstBlockReason,
            signature: signature,
            requiredMemberCount: requiredCount,
            connectedRequiredCount: connectedCount,
            presentRequiredCount: presentCount);
    }

    /// <summary>
    /// Evaluates whether an in-progress team extraction countdown should continue or cancel/restart.
    /// </summary>
    public static TeamExtractionEvaluationResult EvaluateContinuation(
        bool isRitualCompleted,
        TeamCompositionSignature activeSignature,
        IReadOnlyList<TeamMemberExtractionSnapshot> members)
    {
        if (!isRitualCompleted)
        {
            return new TeamExtractionEvaluationResult(
                canStart: false,
                shouldContinue: false,
                cancelReason: TeamExtractionCancelReason.RitualNotCompleted,
                signature: default,
                requiredMemberCount: 0,
                connectedRequiredCount: 0,
                presentRequiredCount: 0);
        }

        TeamCompositionSignature currentSignature = ComputeSignature(members);
        if (!currentSignature.IsValid)
        {
            return new TeamExtractionEvaluationResult(
                canStart: false,
                shouldContinue: false,
                cancelReason: TeamExtractionCancelReason.NoRequiredMembers,
                signature: default,
                requiredMemberCount: 0,
                connectedRequiredCount: 0,
                presentRequiredCount: 0);
        }

        if (activeSignature.IsValid && currentSignature != activeSignature)
        {
            // Composition of required members changed -> triggers restart!
            return new TeamExtractionEvaluationResult(
                canStart: false,
                shouldContinue: false,
                cancelReason: TeamExtractionCancelReason.CompositionChanged,
                signature: currentSignature,
                requiredMemberCount: 0,
                connectedRequiredCount: 0,
                presentRequiredCount: 0);
        }

        int requiredCount = 0;
        int connectedCount = 0;
        int presentCount = 0;
        var firstCancelReason = TeamExtractionCancelReason.None;

        for (int i = 0; i < members.Count; i++)
        {
            TeamMemberExtractionSnapshot m = members[i];
            if (!m.IsRequiredMember)
            {
                continue;
            }

            requiredCount++;

            if (m.IsConnected)
            {
                connectedCount++;
            }
            else if (firstCancelReason == TeamExtractionCancelReason.None)
            {
                firstCancelReason = TeamExtractionCancelReason.RequiredMemberDisconnected;
            }

            if (m.IsInsideTolerant)
            {
                presentCount++;
            }
            else if (firstCancelReason == TeamExtractionCancelReason.None)
            {
                firstCancelReason = TeamExtractionCancelReason.RequiredMemberOutsideArea;
            }
        }

        bool shouldContinue = firstCancelReason == TeamExtractionCancelReason.None;
        return new TeamExtractionEvaluationResult(
            canStart: shouldContinue,
            shouldContinue: shouldContinue,
            cancelReason: firstCancelReason,
            signature: currentSignature,
            requiredMemberCount: requiredCount,
            connectedRequiredCount: connectedCount,
            presentRequiredCount: presentCount);
    }
}
