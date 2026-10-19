#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

[Category("TASK158")]
public sealed class UiAnimationStateTests
{
    private const float Tolerance = 0.0001f;

    [Test]
    public void Ease_IsClampedAndMonotonic()
    {
        Assert.That(UiEasing.Smooth(-1f), Is.EqualTo(0f));
        Assert.That(UiEasing.Smooth(0f), Is.EqualTo(0f));
        Assert.That(UiEasing.Smooth(0.5f), Is.EqualTo(0.5f).Within(Tolerance));
        Assert.That(UiEasing.Smooth(1f), Is.EqualTo(1f));
        Assert.That(UiEasing.Smooth(2f), Is.EqualTo(1f));
        Assert.That(UiEasing.Smooth(0.25f), Is.LessThan(UiEasing.Smooth(0.75f)));
    }

    [Test]
    public void Panel_StartsHiddenAndInvisible()
    {
        var state = new UiPanelTransitionState();

        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Hidden));
        Assert.That(state.IsLogicallyVisible, Is.False);
        Assert.That(state.Visibility, Is.EqualTo(0f));
    }

    [Test]
    public void Panel_ShowIsLogicallyVisibleImmediatelyAndCompletesAfterDuration()
    {
        var state = new UiPanelTransitionState();

        state.BeginShow();
        Assert.That(state.IsLogicallyVisible, Is.True);
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Showing));

        Assert.That(state.Advance(0.1f, 0.2f, 0.1f), Is.False);
        Assert.That(state.Visibility, Is.EqualTo(0.5f).Within(Tolerance));

        state.Advance(0.1f, 0.2f, 0.1f);
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Shown));
        Assert.That(state.Visibility, Is.EqualTo(1f));
    }

    [Test]
    public void Panel_HideIsLogicallyHiddenImmediatelyAndReportsCompletionOnce()
    {
        var state = new UiPanelTransitionState();
        state.SnapShown();

        state.BeginHide();
        Assert.That(state.IsLogicallyVisible, Is.False);
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Hiding));

        Assert.That(state.Advance(0.05f, 0.2f, 0.1f), Is.False);
        Assert.That(state.Advance(0.05f, 0.2f, 0.1f), Is.True);
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Hidden));
        Assert.That(state.Visibility, Is.EqualTo(0f));
        Assert.That(state.Advance(0.05f, 0.2f, 0.1f), Is.False);
    }

    [Test]
    public void Panel_ReversingMidHideContinuesFromCurrentVisibility()
    {
        var state = new UiPanelTransitionState();
        state.SnapShown();
        state.BeginHide();
        state.Advance(0.05f, 0.2f, 0.1f);
        float midway = state.Visibility;

        state.BeginShow();

        Assert.That(state.Visibility, Is.EqualTo(midway).Within(Tolerance));
        Assert.That(state.IsLogicallyVisible, Is.True);
        Assert.That(state.Advance(0.1f, 0.2f, 0.1f), Is.False);
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Shown));
    }

    [Test]
    public void Panel_ZeroDurationsCompleteInOneStep()
    {
        var state = new UiPanelTransitionState();

        state.BeginShow();
        state.Advance(0f, 0f, 0f);
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Shown));

        state.BeginHide();
        Assert.That(state.Advance(0f, 0f, 0f), Is.True);
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Hidden));
    }

    [Test]
    public void Panel_HideWhileHiddenAndShowWhileShownAreNoOps()
    {
        var state = new UiPanelTransitionState();

        state.BeginHide();
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Hidden));

        state.SnapShown();
        state.BeginShow();
        Assert.That(state.Phase, Is.EqualTo(UiPanelTransitionPhase.Shown));
    }

    [Test]
    public void Response_TargetScaleFollowsPointerStateAndInteractability()
    {
        Assert.That(UiPointerResponse.TargetScale(true, false, false, 1.05f, 0.95f), Is.EqualTo(1f));
        Assert.That(UiPointerResponse.TargetScale(true, true, false, 1.05f, 0.95f), Is.EqualTo(1.05f));
        Assert.That(UiPointerResponse.TargetScale(true, true, true, 1.05f, 0.95f), Is.EqualTo(0.95f));
        Assert.That(UiPointerResponse.TargetScale(true, false, true, 1.05f, 0.95f), Is.EqualTo(0.95f));
        Assert.That(UiPointerResponse.TargetScale(false, true, true, 1.05f, 0.95f), Is.EqualTo(1f));
    }

    [Test]
    public void Response_ApproachConvergesWithoutOvershootAndSnaps()
    {
        float value = UiPointerResponse.Approach(1f, 1.05f, 20f, 0.016f);
        Assert.That(value, Is.GreaterThan(1f).And.LessThan(1.05f));

        Assert.That(UiPointerResponse.Approach(1.0499f, 1.05f, 20f, 0.016f), Is.EqualTo(1.05f));
        Assert.That(UiPointerResponse.Approach(1f, 1.05f, 20f, 10f), Is.EqualTo(1.05f));
        Assert.That(UiPointerResponse.Approach(1f, 1.05f, 20f, 0f), Is.EqualTo(1f));
    }

    [Test]
    public void Pulse_IsIdleUntilPlayedAndRestsAtNeutralWhenFinished()
    {
        var pulse = new UiPulseState();
        Assert.That(pulse.IsPlaying, Is.False);
        Assert.That(pulse.Scale, Is.EqualTo(1f));
        Assert.That(pulse.OffsetX, Is.EqualTo(0f));

        pulse.Play(UiPulseKind.Success);
        pulse.Advance(0.1f, 0.3f);
        Assert.That(pulse.IsPlaying, Is.True);

        pulse.Advance(1f, 0.3f);
        Assert.That(pulse.IsPlaying, Is.False);
        Assert.That(pulse.Scale, Is.EqualTo(1f));
        Assert.That(pulse.OffsetX, Is.EqualTo(0f));
    }

    [Test]
    public void Pulse_SuccessScalesUpWithoutMovingAndRejectionShakesWithoutScaling()
    {
        var pulse = new UiPulseState();

        pulse.Play(UiPulseKind.Success);
        pulse.Advance(0.075f, 0.3f);
        Assert.That(pulse.Scale, Is.GreaterThan(1f));
        Assert.That(pulse.OffsetX, Is.EqualTo(0f));

        pulse.Play(UiPulseKind.Rejection);
        Assert.That(pulse.Scale, Is.EqualTo(1f));
        pulse.Advance(0.02f, 0.3f);
        Assert.That(pulse.Scale, Is.EqualTo(1f));
        Assert.That(pulse.OffsetX, Is.Not.EqualTo(0f));
    }

    [Test]
    public void Pulse_PlayingAgainRestartsFromTheBeginning()
    {
        var pulse = new UiPulseState();
        pulse.Play(UiPulseKind.Success);
        pulse.Advance(0.29f, 0.3f);

        pulse.Play(UiPulseKind.Success);
        pulse.Advance(0.075f, 0.3f);

        Assert.That(pulse.IsPlaying, Is.True);
        Assert.That(pulse.Scale, Is.GreaterThan(1.05f));
    }

    [TestCase(SessionConnectionState.ConnectingTown, true)]
    [TestCase(SessionConnectionState.ConnectingRaid, true)]
    [TestCase(SessionConnectionState.ReturningTown, true)]
    [TestCase(SessionConnectionState.MainMenu, false)]
    [TestCase(SessionConnectionState.Town, false)]
    [TestCase(SessionConnectionState.PreparingRaid, false)]
    [TestCase(SessionConnectionState.Raid, false)]
    [TestCase(SessionConnectionState.Failed, false)]
    public void FadePolicy_CoversOnlyWhileConnectingOrReturning(SessionConnectionState state, bool covered)
    {
        Assert.That(SceneTransitionFadePolicy.ShouldCover(state), Is.EqualTo(covered));
    }

    [Test]
    public void Visibility_WithoutTransitionTogglesImmediately()
    {
        var root = new UnityEngine.GameObject("Panel");
        try
        {
            UiPanelVisibility.SetVisible(root, false);
            Assert.That(root.activeSelf, Is.False);
            Assert.That(UiPanelVisibility.IsVisible(root), Is.False);

            UiPanelVisibility.SetVisible(root, true);
            Assert.That(root.activeSelf, Is.True);
            Assert.That(UiPanelVisibility.IsVisible(root), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void Visibility_NullRootIsHiddenAndIgnored()
    {
        Assert.That(UiPanelVisibility.IsVisible(null), Is.False);
        Assert.DoesNotThrow(() => UiPanelVisibility.SetVisible(null, true));
    }
}
#endif
