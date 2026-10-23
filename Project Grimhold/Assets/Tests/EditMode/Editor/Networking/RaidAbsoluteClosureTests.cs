using System;
using System.IO;
using NUnit.Framework;

public sealed class RaidAbsoluteClosureTests
{
    private const string SpawnManagerPath =
        "Assets/Scripts/Networking/NetworkSpawnManager.cs";
    private const string PressureControllerPath =
        "Assets/Scripts/Gameplay/DungeonPressure/DungeonPressureController.cs";
    private const string PlayerCharacterPath =
        "Assets/Scripts/Player/PlayerCharacter.cs";

    [Test]
    public void AbsoluteClosure_DefeatsRemainingRaidingParticipantsWithoutAborting()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        string method = ReadBlock(
            source,
            "public void DefeatRemainingRaidingParticipantsForAbsoluteClosure()",
            "public bool TryCleanupRaidWorldForResults");

        // Must resolve raiding participants using definitive defeat
        Assert.That(method, Does.Contain("character.ForceDefinitiveDefeatFromCollapse()"));
        Assert.That(method, Does.Contain("participant.TryMarkDefeated"));

        // Must NOT call TryAbortForClosure
        Assert.That(method, Does.Not.Contain("TryAbortForClosure"));

        // Must resolve disconnected retained participants as well
        Assert.That(method, Does.Contain("_disconnectedRegistry.RetainedParticipants"));
        Assert.That(method, Does.Contain("_controlledReturns.MarkTerminal"));
        Assert.That(method, Does.Contain("_disconnectedRegistry.Remove"));
    }

    [Test]
    public void AbsoluteClosure_OnlyTargetsRaidingParticipants_SkippingExtractedAndDefeated()
    {
        string source = File.ReadAllText(SpawnManagerPath);
        string method = ReadBlock(
            source,
            "public void DefeatRemainingRaidingParticipantsForAbsoluteClosure()",
            "public bool TryCleanupRaidWorldForResults");

        Assert.That(method, Does.Contain("participant.State != RaidParticipantState.Raiding"));
    }

    [Test]
    public void PressureController_TriggersAbsoluteClosureUponCollapseExpiry()
    {
        string source = File.ReadAllText(PressureControllerPath);

        Assert.That(source, Does.Contain("DefeatRemainingRaidingParticipantsForAbsoluteClosure"));
        Assert.That(source, Does.Contain("CollapseRemainingTicks"));
        Assert.That(source, Does.Contain("Phase == DungeonPressurePhase.Collapse"));
    }

    [Test]
    public void PlayerCharacter_ForceDefinitiveDefeatFromCollapse_ClearsDownedAndCallsHandleDeath()
    {
        string source = File.ReadAllText(PlayerCharacterPath);
        string method = ReadBlock(
            source,
            "internal void ForceDefinitiveDefeatFromCollapse()",
            "internal bool TryRestoreFromDowned");

        Assert.That(method, Does.Contain("_downedStateController?.ClearDownedState()"));
        Assert.That(method, Does.Contain("Health = 0f;"));
        Assert.That(method, Does.Contain("HandleDeath();"));
        Assert.That(method, Does.Contain("participant.TryMarkDefeated(Object);"));
    }

    private static string ReadBlock(string source, string startHeader, string endHeader)
    {
        int start = source.IndexOf(startHeader, StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0), $"Header '{startHeader}' not found in source.");
        int end = source.IndexOf(endHeader, start, StringComparison.Ordinal);
        Assert.That(end, Is.GreaterThan(start), $"Header '{endHeader}' not found after '{startHeader}'.");
        return source.Substring(start, end - start);
    }
}
