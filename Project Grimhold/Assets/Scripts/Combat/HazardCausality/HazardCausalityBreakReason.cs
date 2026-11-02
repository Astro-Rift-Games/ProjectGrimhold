/// <summary>Event that ended a causal hazard exposure (Doc 19 §8.2, §14). None means nothing broke yet.</summary>
public enum HazardCausalityBreakReason : byte
{
    None,
    ExitedHazard,
    IndependentIntervention,
    TargetLeftActiveState
}
