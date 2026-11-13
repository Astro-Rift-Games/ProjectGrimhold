#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

public sealed class RaidAdmissionWindowsTests
{
    [Test]
    public void HostWindow_CoversClientAvailabilityPlusAdmissionPlusMargin()
    {
        float required = RaidAdmissionWindows.ClientSessionAvailabilitySeconds +
                         RaidAdmissionWindows.ClientAdmissionSeconds +
                         RaidAdmissionWindows.HostAdmissionMarginSeconds;

        Assert.That(RaidAdmissionWindows.HostAdmissionSeconds, Is.GreaterThanOrEqualTo(required));
        Assert.That(RaidAdmissionWindows.HostAdmissionSeconds, Is.EqualTo(75f));
    }

    [Test]
    public void HostWindow_NeverClosesBeforeALateClientCanBeAdmitted()
    {
        // A client released at the latest moment still joins within its availability budget and
        // then waits for admission; the Host must still be waiting for it at that point.
        float latestClientAdmission = RaidAdmissionWindows.ClientSessionAvailabilitySeconds +
                                      RaidAdmissionWindows.ClientAdmissionSeconds;

        Assert.That(RaidAdmissionWindows.HostAdmissionSeconds, Is.GreaterThan(latestClientAdmission));
    }

    [Test]
    public void Defaults_MatchTheSerializedClientAvailabilityAndExistingSceneWait()
    {
        Assert.That(RaidAdmissionWindows.ClientSessionAvailabilitySeconds, Is.EqualTo(40f));
        Assert.That(RaidAdmissionWindows.ClientAdmissionSeconds, Is.EqualTo(30f));
        Assert.That(RaidAdmissionWindows.InitialSceneWaitSeconds, Is.EqualTo(30f));
    }

    [Test]
    public void ClientAdmissionClosedGrace_KeepsTheExitUnderOneSecondAfterObservingTheClosure()
    {
        // FIX-03 S9: grace plus one 50 ms poll of FusionSessionLauncher's admission wait.
        const float PollSeconds = 0.05f;

        Assert.That(RaidAdmissionWindows.ClientAdmissionClosedGraceSeconds, Is.GreaterThan(0f));
        Assert.That(RaidAdmissionWindows.ClientAdmissionClosedGraceSeconds + PollSeconds, Is.LessThan(1f));
    }

    // The coordinator's serialized availability budget is the value that applies at runtime; an
    // Inspector change above the shared budget would let a client outlive the Host window.
    [Test]
    public void SerializedClientAvailability_StaysWithinTheSharedBudget()
    {
        GameObject systems = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Systems.prefab");
        Assert.That(systems, Is.Not.Null);
        SessionConnectionCoordinator coordinator = systems.GetComponentInChildren<SessionConnectionCoordinator>(true);
        Assert.That(coordinator, Is.Not.Null);

        SerializedProperty availability = new SerializedObject(coordinator)
            .FindProperty("_clientSessionAvailabilityTimeoutSeconds");

        Assert.That(availability, Is.Not.Null);
        Assert.That(
            availability.floatValue,
            Is.LessThanOrEqualTo(RaidAdmissionWindows.ClientSessionAvailabilitySeconds));
    }
}
#endif
