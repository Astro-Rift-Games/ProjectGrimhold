using NUnit.Framework;

public class DungeonPhaseResolverTests
{
    [Test]
    public void Resolve_RemainingSecondsAboveReinforcements_ReturnsNormal()
    {
        var phase = DungeonPhaseResolver.Resolve(301, 300, 120);
        Assert.AreEqual(DungeonPressurePhase.Normal, phase);
    }

    [Test]
    public void Resolve_RemainingSecondsAtReinforcements_ReturnsReinforcements()
    {
        var phase = DungeonPhaseResolver.Resolve(300, 300, 120);
        Assert.AreEqual(DungeonPressurePhase.Reinforcements, phase);
    }

    [Test]
    public void Resolve_RemainingSecondsBelowReinforcements_ReturnsReinforcements()
    {
        var phase = DungeonPhaseResolver.Resolve(299, 300, 120);
        Assert.AreEqual(DungeonPressurePhase.Reinforcements, phase);
    }

    [Test]
    public void Resolve_RemainingSecondsAtCritical_ReturnsCriticalPressure()
    {
        var phase = DungeonPhaseResolver.Resolve(120, 300, 120);
        Assert.AreEqual(DungeonPressurePhase.CriticalPressure, phase);
    }

    [Test]
    public void Resolve_RemainingSecondsBelowCritical_ReturnsCriticalPressure()
    {
        var phase = DungeonPhaseResolver.Resolve(119, 300, 120);
        Assert.AreEqual(DungeonPressurePhase.CriticalPressure, phase);
    }

    [Test]
    public void Resolve_RemainingSecondsAtZero_ReturnsCollapse()
    {
        var phase = DungeonPhaseResolver.Resolve(0, 300, 120);
        Assert.AreEqual(DungeonPressurePhase.Collapse, phase);
    }

    [Test]
    public void Resolve_RemainingSecondsNegative_ReturnsCollapse()
    {
        var phase = DungeonPhaseResolver.Resolve(-10, 300, 120);
        Assert.AreEqual(DungeonPressurePhase.Collapse, phase);
    }
    
    [Test]
    public void Resolve_Monotonicity_AlwaysAdvances()
    {
        var p1 = DungeonPhaseResolver.Resolve(400, 300, 120);
        var p2 = DungeonPhaseResolver.Resolve(200, 300, 120);
        var p3 = DungeonPhaseResolver.Resolve(50, 300, 120);
        var p4 = DungeonPhaseResolver.Resolve(0, 300, 120);
        
        Assert.IsTrue(p1 < p2);
        Assert.IsTrue(p2 < p3);
        Assert.IsTrue(p3 < p4);
    }
    
    [Test]
    public void GeneratesWithoutLoot_NormalPhase_ReturnsFalse()
    {
        Assert.IsFalse(DungeonPhaseResolver.GeneratesWithoutLoot(DungeonPressurePhase.Normal));
    }

    [Test]
    public void GeneratesWithoutLoot_ReinforcementsPhase_ReturnsFalse()
    {
        Assert.IsFalse(DungeonPhaseResolver.GeneratesWithoutLoot(DungeonPressurePhase.Reinforcements));
    }

    [Test]
    public void GeneratesWithoutLoot_CriticalPressurePhase_ReturnsFalse()
    {
        Assert.IsFalse(DungeonPhaseResolver.GeneratesWithoutLoot(DungeonPressurePhase.CriticalPressure));
    }

    [Test]
    public void GeneratesWithoutLoot_CollapsePhase_ReturnsTrue()
    {
        Assert.IsTrue(DungeonPhaseResolver.GeneratesWithoutLoot(DungeonPressurePhase.Collapse));
    }
}
