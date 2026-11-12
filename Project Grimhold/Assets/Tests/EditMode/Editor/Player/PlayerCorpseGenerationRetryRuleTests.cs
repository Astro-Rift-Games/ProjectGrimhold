using NUnit.Framework;

public sealed class PlayerCorpseGenerationRetryRuleTests
{
    [Test]
    public void FullyCompensatedFailure_AllowsARetry()
    {
        Assert.That(
            PlayerCorpseGenerationRetryRule.CanRetryAfterFailure(
                playerInventoryIntact: true,
                corpseContainerClean: true),
            Is.True);
    }

    [Test]
    public void ChangedPlayerInventory_StaysTerminalSoNothingIsLostOnRetry()
    {
        Assert.That(
            PlayerCorpseGenerationRetryRule.CanRetryAfterFailure(
                playerInventoryIntact: false,
                corpseContainerClean: true),
            Is.False);
    }

    [Test]
    public void ContainerWithLeftoverContent_StaysTerminalSoNothingIsDuplicatedOnRetry()
    {
        Assert.That(
            PlayerCorpseGenerationRetryRule.CanRetryAfterFailure(
                playerInventoryIntact: true,
                corpseContainerClean: false),
            Is.False);
    }

    [Test]
    public void NeitherIntactNorClean_StaysTerminal()
    {
        Assert.That(
            PlayerCorpseGenerationRetryRule.CanRetryAfterFailure(
                playerInventoryIntact: false,
                corpseContainerClean: false),
            Is.False);
    }
}
