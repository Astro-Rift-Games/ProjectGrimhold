/// <summary>
/// Selects how a two-handed weapon's baked attack presents the second hand.
/// Static presentation data read only by the directional attack generator; never replicated.
/// </summary>
public enum SecondHandPresentation : byte
{
    /// <summary>The second hand stays on the weapon's secondary grip point for the whole attack.</summary>
    HoldsSecondaryGrip = 0,

    /// <summary>The second hand keeps its own authored trajectory instead of holding the weapon.</summary>
    FollowsAuthoredMotion = 1
}
