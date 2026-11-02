#if UNITY_INCLUDE_TESTS
using NUnit.Framework;

/// <summary>Doc 19 §8 and §12 T06 causal exposure rules, independent of Fusion and physics.</summary>
public sealed class HazardExposureCausalityRulesTests
{
    private const int Pusher = 101;
    private const int OtherPusher = 202;
    private const int Trap = 501;
    private const int OtherTrap = 502;

    private HazardExposureCausalityState _state;

    [SetUp]
    public void SetUp() => _state = default;

    [Test]
    public void T06_01_PushIntoHazard_ResolvesPusherAsCauser()
    {
        HazardExposureCausalityRules.RecordAcceptedKnockback(ref _state, Pusher, 7, 10);
        Tick(11, knockbackActive: true);
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 11);

        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 11, out int causer), Is.True);
        Assert.That(causer, Is.EqualTo(Pusher));
        Assert.That(_state.LinkedHazardId, Is.EqualTo(Trap));
        Assert.That(_state.LinkedKnockbackSequence, Is.EqualTo(7));
    }

    [Test]
    public void T06_02_HazardKeepsDamagingAfterPushEnds_CausalityPersistsWhileExposed()
    {
        LinkPusherTo(Trap, tick: 11);
        for (int tick = 12; tick <= 300; tick++)
        {
            Tick(tick, knockbackActive: tick < 20);
            HazardExposureCausalityRules.ReportPresence(ref _state, Trap, tick);
        }

        Assert.That(_state.PendingPusherEntityId, Is.Zero, "The finished push no longer links new hazards.");
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 300, out int causer), Is.True);
        Assert.That(causer, Is.EqualTo(Pusher));
    }

    [Test]
    public void T06_03_VoluntaryMoveToAnotherHazard_DoesNotInheritCausality()
    {
        LinkPusherTo(Trap, tick: 11);
        StayIn(Trap, 12, 13);
        HazardExposureCausalityRules.ReportPresence(ref _state, OtherTrap, 13);

        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, OtherTrap, 13, out int causer), Is.False);
        Assert.That(causer, Is.Zero);
    }

    [Test]
    public void T06_09_LeavingAndVoluntarilyReturning_BreaksPreviousCausality()
    {
        LinkPusherTo(Trap, tick: 11);
        Tick(12, knockbackActive: false);
        Tick(13, knockbackActive: false);
        Tick(14, knockbackActive: false);

        Assert.That(_state.LinkedHazardId, Is.Zero);
        Assert.That(_state.LastBreakReason, Is.EqualTo(HazardCausalityBreakReason.ExitedHazard));
        Assert.That(_state.LastBrokenHazardId, Is.EqualTo(Trap));
        Assert.That(_state.LastBrokenPusherEntityId, Is.EqualTo(Pusher));
        Assert.That(_state.LastBrokenKnockbackSequence, Is.EqualTo(7));
        Assert.That(_state.LastBreakTick, Is.EqualTo(13));

        Tick(40, knockbackActive: false);
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 40);
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 40, out _), Is.False);
    }

    [Test]
    public void PresenceWithinOneTickOfOrderingGrace_KeepsCausality()
    {
        LinkPusherTo(Trap, tick: 11);
        Tick(12, knockbackActive: false);

        Assert.That(_state.LinkedHazardId, Is.EqualTo(Trap), "Hazard and target tick order is not guaranteed.");
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 12);
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 12, out _), Is.True);
    }

    [Test]
    public void VoluntaryEntryWithoutPush_RegistersNoCauser()
    {
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 11);

        Assert.That(_state.LinkedHazardId, Is.Zero);
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 11, out _), Is.False);
    }

    [Test]
    public void PushOnTargetAlreadyInsideHazard_IsNotAPushTowardThatHazard()
    {
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 10);
        HazardExposureCausalityRules.RecordAcceptedKnockback(ref _state, Pusher, 7, 10);
        Tick(11, knockbackActive: true);
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 11);

        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 11, out _), Is.False);

        HazardExposureCausalityRules.ReportPresence(ref _state, OtherTrap, 11);
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, OtherTrap, 11, out int causer), Is.True);
        Assert.That(causer, Is.EqualTo(Pusher));
    }

    [Test]
    public void LaterPushIntoAnotherHazard_IsAnIndependentInterventionThatReplacesCausality()
    {
        LinkPusherTo(Trap, tick: 11);
        StayIn(Trap, 12, 20);
        HazardExposureCausalityRules.RecordAcceptedKnockback(ref _state, OtherPusher, 8, 20);
        Tick(21, knockbackActive: true);
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 21);
        HazardExposureCausalityRules.ReportPresence(ref _state, OtherTrap, 21);

        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, OtherTrap, 21, out int causer), Is.True);
        Assert.That(causer, Is.EqualTo(OtherPusher));
        Assert.That(_state.LinkedKnockbackSequence, Is.EqualTo(8));
        Assert.That(_state.LastBreakReason, Is.EqualTo(HazardCausalityBreakReason.IndependentIntervention));
        Assert.That(_state.LastBrokenHazardId, Is.EqualTo(Trap));
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 21, out _), Is.False);
    }

    [Test]
    public void SamePushReachingAnOverlappingHazard_KeepsTheFirstLink()
    {
        LinkPusherTo(Trap, tick: 11);
        Tick(12, knockbackActive: true);
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 12);
        HazardExposureCausalityRules.ReportPresence(ref _state, OtherTrap, 12);

        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 12, out int causer), Is.True);
        Assert.That(causer, Is.EqualTo(Pusher));
        Assert.That(_state.LastBreakReason, Is.EqualTo(HazardCausalityBreakReason.None));
    }

    [Test]
    public void LaterPushThatKeepsTargetInSameHazard_PreservesOriginalCausality()
    {
        LinkPusherTo(Trap, tick: 11);
        StayIn(Trap, 12, 20);
        HazardExposureCausalityRules.RecordAcceptedKnockback(ref _state, OtherPusher, 8, 20);
        Tick(21, knockbackActive: true);
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 21);

        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 21, out int causer), Is.True);
        Assert.That(causer, Is.EqualTo(Pusher));
    }

    [Test]
    public void EnvironmentalKnockbackReplacingPlayerPush_DiscardsPendingCause()
    {
        HazardExposureCausalityRules.RecordAcceptedKnockback(ref _state, Pusher, 7, 10);
        HazardExposureCausalityRules.RecordAcceptedKnockback(ref _state, 0, 8, 11);
        Tick(12, knockbackActive: true);
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 12);

        Assert.That(_state.PendingPusherEntityId, Is.Zero);
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 12, out _), Is.False);
    }

    [Test]
    public void Reset_ClearsCausalityAndRecordsReason()
    {
        LinkPusherTo(Trap, tick: 11);
        HazardExposureCausalityRules.Reset(ref _state, HazardCausalityBreakReason.TargetLeftActiveState, 12);

        Assert.That(_state.LinkedHazardId, Is.Zero);
        Assert.That(_state.PendingPusherEntityId, Is.Zero);
        Assert.That(_state.LastPresenceHazardId, Is.Zero);
        Assert.That(_state.LastBreakReason, Is.EqualTo(HazardCausalityBreakReason.TargetLeftActiveState));
        Assert.That(_state.LastBrokenHazardId, Is.EqualTo(Trap));
        HazardExposureCausalityRules.ReportPresence(ref _state, Trap, 12);
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 12, out _), Is.False);
    }

    [Test]
    public void InvalidHazardOrTick_NeverResolves()
    {
        LinkPusherTo(Trap, tick: 11);

        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, 0, 11, out _), Is.False);
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 10, out _), Is.False);
        Assert.That(HazardExposureCausalityRules.TryResolveCauser(in _state, Trap, 13, out _), Is.False);
    }

    private void LinkPusherTo(int hazard, int tick)
    {
        HazardExposureCausalityRules.RecordAcceptedKnockback(ref _state, Pusher, 7, tick - 1);
        Tick(tick, knockbackActive: true);
        HazardExposureCausalityRules.ReportPresence(ref _state, hazard, tick);
        Assert.That(_state.LinkedHazardId, Is.EqualTo(hazard));
    }

    private void StayIn(int hazard, int fromTick, int toTick)
    {
        for (int tick = fromTick; tick <= toTick; tick++)
        {
            Tick(tick, knockbackActive: false);
            HazardExposureCausalityRules.ReportPresence(ref _state, hazard, tick);
        }
    }

    private void Tick(int tick, bool knockbackActive) =>
        HazardExposureCausalityRules.Tick(ref _state, knockbackActive, tick);
}
#endif
