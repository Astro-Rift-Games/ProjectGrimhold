using System;
using UnityEngine;

/// <summary>Visual state of one Raid ability HUD slot, derived only from confirmed state.</summary>
public enum RaidAbilityHudSlotState : byte
{
    Empty,
    Ready,
    OnCooldown,
    Preparing,
    Executing
}

/// <summary>Plain facts about one slot, copied from confirmed gameplay state by a source adapter.</summary>
public struct RaidAbilityHudSlotFacts
{
    public bool IsPrepared;
    public AbilityExecutionPhase Phase;
    public bool IsOnCooldown;
    public float RemainingCooldownSeconds;
    public float TotalCooldownSeconds;
    public AbilityResourceType Resource;
    public float Cost;
    public bool HasResourceReading;
    public float AvailableResource;
}

/// <summary>Immutable display model of one slot. Value equality enables view dirty checking.</summary>
public readonly struct RaidAbilityHudSlotModel : IEquatable<RaidAbilityHudSlotModel>
{
    public RaidAbilityHudSlotState State { get; }

    /// <summary>Remaining cooldown in [0, 1]; 1 right after activation.</summary>
    public float CooldownFill { get; }

    /// <summary>Remaining cooldown rounded up to tenths of a second, or zero.</summary>
    public float CooldownSeconds { get; }

    /// <summary>True when the confirmed balance is known and below the ability cost.</summary>
    public bool InsufficientResource { get; }

    public RaidAbilityHudSlotModel(
        RaidAbilityHudSlotState state,
        float cooldownFill,
        float cooldownSeconds,
        bool insufficientResource)
    {
        State = state;
        CooldownFill = cooldownFill;
        CooldownSeconds = cooldownSeconds;
        InsufficientResource = insufficientResource;
    }

    public bool Equals(RaidAbilityHudSlotModel other) =>
        State == other.State &&
        Mathf.Approximately(CooldownFill, other.CooldownFill) &&
        Mathf.Approximately(CooldownSeconds, other.CooldownSeconds) &&
        InsufficientResource == other.InsufficientResource;

    public override bool Equals(object obj) => obj is RaidAbilityHudSlotModel other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(State, CooldownFill, CooldownSeconds, InsufficientResource);
}

/// <summary>Pure projection from slot facts to a display model. No Fusion, no Unity objects.</summary>
public static class RaidAbilityHudModelBuilder
{
    public static RaidAbilityHudSlotModel Build(in RaidAbilityHudSlotFacts facts)
    {
        if (!facts.IsPrepared)
        {
            return new RaidAbilityHudSlotModel(RaidAbilityHudSlotState.Empty, 0f, 0f, false);
        }

        float remaining = IsFinite(facts.RemainingCooldownSeconds) && facts.RemainingCooldownSeconds > 0f
            ? facts.RemainingCooldownSeconds
            : 0f;
        bool onCooldown = facts.IsOnCooldown && remaining > 0f;
        float fill = onCooldown ? Normalize(facts.TotalCooldownSeconds, remaining) : 0f;
        float seconds = onCooldown ? Mathf.Ceil(remaining * 10f) * 0.1f : 0f;

        RaidAbilityHudSlotState state = facts.Phase switch
        {
            AbilityExecutionPhase.Executing => RaidAbilityHudSlotState.Executing,
            AbilityExecutionPhase.Preparing => RaidAbilityHudSlotState.Preparing,
            _ => onCooldown ? RaidAbilityHudSlotState.OnCooldown : RaidAbilityHudSlotState.Ready
        };

        bool insufficient = facts.HasResourceReading &&
            IsFinite(facts.AvailableResource) &&
            facts.AvailableResource < facts.Cost;

        return new RaidAbilityHudSlotModel(state, fill, seconds, insufficient);
    }

    private static float Normalize(float total, float remaining)
    {
        if (!IsFinite(total) || total <= 0f)
        {
            return 0f;
        }

        float normalized = remaining / total;
        return IsFinite(normalized) ? Mathf.Clamp01(normalized) : 0f;
    }

    private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>Short Spanish owner-feedback strings for outcomes that are not visible in confirmed state.</summary>
public static class RaidAbilityHudMessages
{
    public static string ForRejection(AbilityActivationFailure failure) => failure switch
    {
        AbilityActivationFailure.Cooldown => "En enfriamiento",
        AbilityActivationFailure.InsufficientResource => "Recurso insuficiente",
        AbilityActivationFailure.RequirementsNotMet => "Requisitos no cumplidos",
        AbilityActivationFailure.AlreadyExecuting => "Ya en ejecución",
        AbilityActivationFailure.BehaviourRejected => "No se puede usar ahora",
        AbilityActivationFailure.InvalidPlan => "No se puede usar ahora",
        AbilityActivationFailure.AimUnavailable => "Apuntado no disponible",
        _ => "Habilidad no disponible"
    };

    public static string ForInterruption(AbilityExecutionStopReason reason) => "Interrumpida";
}
