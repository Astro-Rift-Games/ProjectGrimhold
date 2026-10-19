using System;

/// <summary>Pointer hover/press scale response for UI controls. Pure math, no Unity state.</summary>
public static class UiPointerResponse
{
    private const float SnapThreshold = 0.0005f;

    public static float TargetScale(bool interactable, bool hovered, bool pressed, float hoverScale, float pressedScale)
    {
        if (!interactable)
        {
            return 1f;
        }

        if (pressed)
        {
            return pressedScale;
        }

        return hovered ? hoverScale : 1f;
    }

    /// <summary>Frame-rate independent exponential approach that never overshoots and snaps when close.</summary>
    public static float Approach(float current, float target, float sharpness, float deltaTime)
    {
        if (deltaTime <= 0f)
        {
            return current;
        }

        float blend = 1f - (float)Math.Exp(-sharpness * deltaTime);
        float next = current + (target - current) * blend;
        return Math.Abs(target - next) <= SnapThreshold ? target : next;
    }
}
