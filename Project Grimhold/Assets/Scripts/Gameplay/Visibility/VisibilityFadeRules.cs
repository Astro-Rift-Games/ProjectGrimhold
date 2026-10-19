using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Balance timings that soften visibility changes: a short grace after losing sight,
    /// then a fade-out, and a fade-in when sight is regained.
    /// </summary>
    public readonly struct VisibilityFadeTimings
    {
        /// <summary>Snaps between hidden and visible with no grace or fade.</summary>
        public static readonly VisibilityFadeTimings Instant = new VisibilityFadeTimings(0f, 0f, 0f);

        public VisibilityFadeTimings(float graceSeconds, float fadeInSeconds, float fadeOutSeconds)
        {
            GraceSeconds = Mathf.Max(0f, graceSeconds);
            FadeInSeconds = Mathf.Max(0f, fadeInSeconds);
            FadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
        }

        public float GraceSeconds { get; }
        public float FadeInSeconds { get; }
        public float FadeOutSeconds { get; }
    }

    /// <summary>Presentation state of one target: its opacity and how long it has been out of sight.</summary>
    public readonly struct VisibilityFadeState
    {
        /// <summary>Fully transparent and already past any grace period.</summary>
        public static readonly VisibilityFadeState Hidden = new VisibilityFadeState(0f, float.PositiveInfinity);

        public VisibilityFadeState(float alpha, float unseenSeconds)
        {
            Alpha = Mathf.Clamp01(alpha);
            UnseenSeconds = Mathf.Max(0f, unseenSeconds);
        }

        /// <summary>Opacity in [0, 1]. Zero means the target is not rendered at all.</summary>
        public float Alpha { get; }

        /// <summary>Seconds since the target was last seen; zero while it is seen.</summary>
        public float UnseenSeconds { get; }
    }

    /// <summary>
    /// Pure, frame-rate independent fade rules. Being seen fades the target in from its
    /// current opacity; losing sight keeps it fully visible during the grace period and
    /// then fades it out.
    /// </summary>
    public static class VisibilityFadeRules
    {
        public static VisibilityFadeState Step(
            VisibilityFadeState state,
            bool seen,
            float deltaTime,
            VisibilityFadeTimings timings)
        {
            float elapsed = Mathf.Max(0f, deltaTime);
            if (seen)
            {
                float fadedIn = timings.FadeInSeconds <= 0f ? 1f : state.Alpha + elapsed / timings.FadeInSeconds;
                return new VisibilityFadeState(fadedIn, 0f);
            }

            float previousUnseen = state.UnseenSeconds;
            float unseen = previousUnseen + elapsed;
            if (state.Alpha <= 0f)
            {
                return new VisibilityFadeState(0f, unseen);
            }

            float fadingTime = unseen - Mathf.Max(previousUnseen, timings.GraceSeconds);
            if (fadingTime <= 0f)
            {
                return new VisibilityFadeState(state.Alpha, unseen);
            }

            float fadedOut = timings.FadeOutSeconds <= 0f ? 0f : state.Alpha - fadingTime / timings.FadeOutSeconds;
            return new VisibilityFadeState(fadedOut, unseen);
        }
    }
}
