using NUnit.Framework;

public sealed class ControlledReturnRegistryTests
{
    [Test]
    public void Key_UsesOrdinalProfileAndGenerationIdentity()
    {
        var exact = new ControlledReturnKey("client-a", "raid-1");

        Assert.That(exact, Is.EqualTo(new ControlledReturnKey("client-a", "raid-1")));
        Assert.That(exact, Is.Not.EqualTo(new ControlledReturnKey("CLIENT-A", "raid-1")));
        Assert.That(exact, Is.Not.EqualTo(new ControlledReturnKey("client-a", "raid-2")));
        Assert.That(new ControlledReturnKey(string.Empty, "raid-1").IsValid, Is.False);
        Assert.That(new ControlledReturnKey("client-a", null).IsValid, Is.False);
    }

    [Test]
    public void Authorization_IsOneShotAndCannotCrossRaidGeneration()
    {
        var registry = new ControlledReturnRegistry();
        var authorized = new ControlledReturnKey("client-a", "raid-1");
        var otherGeneration = new ControlledReturnKey("client-a", "raid-2");

        Assert.That(registry.TryRegister(in authorized), Is.True);
        Assert.That(registry.TryRegister(in authorized), Is.False);
        Assert.That(registry.TryConsume(in otherGeneration), Is.False);
        Assert.That(registry.TryConsume(in authorized), Is.True);
        Assert.That(registry.TryConsume(in authorized), Is.False);
    }

    [Test]
    public void OnlyConsumedAuthorizationBecomesTerminalForThatGeneration()
    {
        var registry = new ControlledReturnRegistry();
        var controlled = new ControlledReturnKey("client-a", "raid-1");
        var unexpected = new ControlledReturnKey("client-b", "raid-1");
        var laterRaid = new ControlledReturnKey("client-a", "raid-2");

        Assert.That(registry.TryRegister(in controlled), Is.True);
        Assert.That(registry.TryConsume(in controlled), Is.True);
        Assert.That(registry.MarkTerminal(in controlled), Is.True);

        Assert.That(registry.IsTerminal(in controlled), Is.True);
        Assert.That(registry.IsTerminal(in unexpected), Is.False);
        Assert.That(registry.IsTerminal(in laterRaid), Is.False);
    }
}
