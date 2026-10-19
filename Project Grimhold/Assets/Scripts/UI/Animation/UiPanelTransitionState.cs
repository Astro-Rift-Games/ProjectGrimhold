/// <summary>
/// Local show/hide progress for a UI panel. Logical visibility changes immediately so callers
/// that ask whether a panel is open never wait for the animation; only the visual progress lags.
/// </summary>
public sealed class UiPanelTransitionState
{
    private float _progress;

    public UiPanelTransitionPhase Phase { get; private set; } = UiPanelTransitionPhase.Hidden;

    public bool IsLogicallyVisible =>
        Phase == UiPanelTransitionPhase.Showing || Phase == UiPanelTransitionPhase.Shown;

    public bool IsAnimating =>
        Phase == UiPanelTransitionPhase.Showing || Phase == UiPanelTransitionPhase.Hiding;

    /// <summary>Eased visual visibility in [0, 1].</summary>
    public float Visibility => UiEasing.Smooth(_progress);

    public void BeginShow()
    {
        if (IsLogicallyVisible)
        {
            return;
        }

        Phase = UiPanelTransitionPhase.Showing;
    }

    public void BeginHide()
    {
        if (!IsLogicallyVisible)
        {
            return;
        }

        Phase = UiPanelTransitionPhase.Hiding;
    }

    public void SnapShown()
    {
        _progress = 1f;
        Phase = UiPanelTransitionPhase.Shown;
    }

    public void SnapHidden()
    {
        _progress = 0f;
        Phase = UiPanelTransitionPhase.Hidden;
    }

    /// <summary>Advances the visual progress. Returns true only on the step that completes a hide.</summary>
    public bool Advance(float deltaTime, float showDuration, float hideDuration)
    {
        if (Phase == UiPanelTransitionPhase.Showing)
        {
            _progress = showDuration > 0f ? _progress + deltaTime / showDuration : 1f;
            if (_progress >= 1f)
            {
                SnapShown();
            }

            return false;
        }

        if (Phase == UiPanelTransitionPhase.Hiding)
        {
            _progress = hideDuration > 0f ? _progress - deltaTime / hideDuration : 0f;
            if (_progress <= 0f)
            {
                SnapHidden();
                return true;
            }
        }

        return false;
    }
}
