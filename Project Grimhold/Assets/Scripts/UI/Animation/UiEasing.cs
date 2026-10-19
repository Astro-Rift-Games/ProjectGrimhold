/// <summary>Easing curves for local UI presentation. Pure math, no Unity state.</summary>
public static class UiEasing
{
    /// <summary>Symmetric smoothstep, so reversing a transition mid-way never jumps.</summary>
    public static float Smooth(float t)
    {
        if (t <= 0f)
        {
            return 0f;
        }

        if (t >= 1f)
        {
            return 1f;
        }

        return t * t * (3f - 2f * t);
    }
}
