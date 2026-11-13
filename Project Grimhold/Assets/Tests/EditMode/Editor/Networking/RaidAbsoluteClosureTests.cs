using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public sealed class RaidAbsoluteClosureTests
{
    [TestCase(RaidParticipantState.Raiding, true)]
    [TestCase(RaidParticipantState.Extracted, false)]
    [TestCase(RaidParticipantState.Defeated, false)]
    [TestCase(RaidParticipantState.Aborted, false)]
    public void Selection_OnlyUnresolvedRaiders_RegardlessOfConnectivityOrDowned(
        RaidParticipantState state, bool expected)
    {
        // Connectivity and Downed do not change the functional participant state.
        Assert.That(AbsoluteClosureRules.ShouldDefeat(state), Is.EqualTo(expected));
    }

    [Test]
    public void DefaultAsset_TotalPlusCollapse_Equals17Minutes()
    {
        var config = AssetDatabase.LoadAssetAtPath<DungeonPressureConfig>(
            "Assets/Scriptable Objects/DefaultDungeonPressureConfig.asset");
        Assert.That(config, Is.Not.Null);
        Assert.That(config.Validate(out string error), Is.True, error);
        Assert.That(config.TotalDurationSeconds, Is.EqualTo(900));
        Assert.That(config.CollapseDurationSeconds, Is.EqualTo(120));
        Assert.That(config.TotalDurationSeconds + config.CollapseDurationSeconds, Is.EqualTo(17 * 60));
        Assert.That(config.ReinforcementsThresholdSeconds, Is.EqualTo(240));
        Assert.That(config.CriticalPressureThresholdSeconds, Is.EqualTo(60));
    }

    [Test]
    public void NetworkRaidParticipant_DeclaresTryMarkDefeatedWithoutAvatar()
    {
        MethodInfo method = typeof(NetworkRaidParticipant).GetMethod(
            "TryMarkDefeatedWithoutAvatar",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.That(method, Is.Not.Null,
            "NetworkRaidParticipant must provide TryMarkDefeatedWithoutAvatar() to resolve raiding participants without an avatar during absolute collapse.");
    }

    [Test]
    public void AbsoluteClosure_SpawnManager_ResolvesParticipantWithoutAvatar()
    {
        string source = File.ReadAllText("Assets/Scripts/Networking/NetworkSpawnManager.cs");
        string method = ReadMethod(source, "public void DefeatRemainingRaidingParticipantsForAbsoluteClosure");

        Assert.That(method, Does.Contain("TryMarkDefeatedWithoutAvatar"),
            "DefeatRemainingRaidingParticipantsForAbsoluteClosure must invoke TryMarkDefeatedWithoutAvatar() when avatar is unresolvable or after collapse.");
        Assert.That(method, Does.Not.Contain("participant.TryMarkDefeated(avatar)"),
            "DefeatRemainingRaidingParticipantsForAbsoluteClosure must not invoke TryMarkDefeated(avatar) with a null or unresolvable avatar.");
    }

    [Test]
    public void MatchController_AdvanceClosure_GuardsDeadlineIsRunningAndBounded()
    {
        string source = File.ReadAllText("Assets/Scripts/Networking/NetworkMatchController.cs");
        string method = ReadMethod(source, "private void AdvanceClosure");

        Assert.That(method, Does.Contain("PersistenceDeadline.IsRunning"),
            "AdvanceClosure must ensure PersistenceDeadline.IsRunning before waiting on pending extraction commits.");
        Assert.That(method, Does.Contain("!PersistenceDeadline.Expired(Runner)"),
            "AdvanceClosure must bound persistence wait with PersistenceDeadline.Expired.");
    }

    private static string ReadMethod(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Signature '{signature}' not found.");
        int bodyStart = source.IndexOf('{', start);
        Assert.That(bodyStart, Is.GreaterThanOrEqualTo(0));

        int depth = 0;
        for (int i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(start, i - start + 1);
                }
            }
        }

        return source.Substring(start);
    }
}
