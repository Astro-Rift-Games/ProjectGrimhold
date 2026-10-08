/// <summary>
/// Tick rules for drawing an aim stance. Entering the stance starts a draw; once the per-weapon draw time has
/// elapsed the stance is fully drawn and a shot uses the weapon's short aimed release delay instead of its normal
/// release seconds. Everything is derived from ticks, so resimulation and Host Migration stay deterministic.
/// </summary>
public static class AimStanceDraw
{
    /// <summary>The start tick while no stance is held.</summary>
    public const int NoStart = -1;

    /// <summary>The start tick after this tick: set on entering the stance, kept while held, reset on leaving.</summary>
    public static int NextStartTick(int previousStartTick, bool stanceAccepted, int currentTick)
    {
        if (!stanceAccepted)
        {
            return NoStart;
        }

        return previousStartTick >= 0 ? previousStartTick : currentTick;
    }

    /// <summary>
    /// Fully drawn from the tick the draw time, rounded up to ticks like a release deadline, has elapsed.
    /// A stance that never started, or an invalid draw time, is never drawn.
    /// </summary>
    public static bool IsFullyDrawn(int currentTick, int startTick, float drawSeconds, float deltaTime)
    {
        return startTick >= 0 &&
            AttackTiming.TryGetReleaseTick(startTick, drawSeconds, deltaTime, out int drawnTick) &&
            currentTick >= drawnTick;
    }

    /// <summary>
    /// Clip time of the drawn pose while the stance is held: the attack clip plays from its start to the drawn frame
    /// over the draw time, then holds that frame.
    /// </summary>
    public static float DrawClipSeconds(float elapsedSinceStance, float drawSeconds, float drawnClipSeconds)
    {
        if (drawSeconds <= 0f)
        {
            return drawnClipSeconds;
        }

        float progress = elapsedSinceStance <= 0f ? 0f : elapsedSinceStance / drawSeconds;
        return drawnClipSeconds * (progress >= 1f ? 1f : progress);
    }

    /// <summary>The aimed release delay applies only to a stance weapon that is fully drawn.</summary>
    public static bool TrySelectAimedReleaseSeconds(WeaponDefinition weapon, bool fullyDrawn, out float seconds)
    {
        seconds = 0f;
        if (!fullyDrawn || !PlayerAimStanceRules.WeaponAllows(weapon))
        {
            return false;
        }

        seconds = weapon.AimedReleaseSeconds;
        return true;
    }
}
