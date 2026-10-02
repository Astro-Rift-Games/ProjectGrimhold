using NUnit.Framework;
using Spawning;

public class PvePopulationTrackerTests
{
    private PvePopulationTracker _tracker;

    [SetUp]
    public void Setup()
    {
        _tracker = new PvePopulationTracker();
    }

    [Test]
    public void Register_NewEnemy_IncreasesCountsAndThreat()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        
        Assert.AreEqual(1, _tracker.TotalActivePopulation);
        Assert.AreEqual(1, _tracker.GetActivePopulationByOrigin(EnemyPopulationOrigin.Bootstrap));
        Assert.AreEqual(1, _tracker.GetActiveThreatByOrigin(EnemyPopulationOrigin.Bootstrap));
        Assert.AreEqual(0, _tracker.ActiveReinforcements);
    }

    [Test]
    public void Register_TwiceSameId_IsIdempotent()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1); // Should be ignored

        Assert.AreEqual(1, _tracker.TotalActivePopulation);
        Assert.AreEqual(1, _tracker.GetActiveThreatByOrigin(EnemyPopulationOrigin.Bootstrap));
    }

    [Test]
    public void Unregister_RegisteredEnemy_DecreasesCountsAndThreat()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Reinforcement, 2);
        Assert.AreEqual(1, _tracker.ActiveReinforcements);
        Assert.AreEqual(1, _tracker.TotalActivePopulation);
        Assert.AreEqual(2, _tracker.GetActiveThreatByOrigin(EnemyPopulationOrigin.Reinforcement));

        _tracker.Unregister(1u);

        Assert.AreEqual(0, _tracker.ActiveReinforcements);
        Assert.AreEqual(0, _tracker.TotalActivePopulation);
        Assert.AreEqual(0, _tracker.GetActiveThreatByOrigin(EnemyPopulationOrigin.Reinforcement));
    }

    [Test]
    public void Unregister_NotRegisteredEnemy_DoesNothing()
    {
        _tracker.Unregister(99u); // Should not throw and not change counts
        
        Assert.AreEqual(0, _tracker.TotalActivePopulation);
    }

    [Test]
    public void GetAvailableCapacity_RespectsBudgetAndNeverNegative()
    {
        // Budget = 5
        _tracker.Register(1u, EnemyPopulationOrigin.Reinforcement, 2);
        _tracker.Register(2u, EnemyPopulationOrigin.Reinforcement, 1);

        int capacity = _tracker.GetAvailableCapacity(5, EnemyPopulationOrigin.Reinforcement);
        Assert.AreEqual(2, capacity); // 5 - 3 = 2

        _tracker.Register(3u, EnemyPopulationOrigin.Reinforcement, 3);
        
        // Threat is now 6
        capacity = _tracker.GetAvailableCapacity(5, EnemyPopulationOrigin.Reinforcement);
        Assert.AreEqual(0, capacity); // Not negative
    }

    [Test]
    public void GetAvailableGlobalCapacity_RespectsMaxActiveAndNeverNegative()
    {
        // Max Active = 3
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        _tracker.Register(2u, EnemyPopulationOrigin.Reinforcement, 1);

        int capacity = _tracker.GetAvailableGlobalCapacity(3);
        Assert.AreEqual(1, capacity); // 3 - 2 = 1

        _tracker.Register(3u, EnemyPopulationOrigin.Bootstrap, 1);
        _tracker.Register(4u, EnemyPopulationOrigin.Bootstrap, 1);

        // Total pop is 4
        capacity = _tracker.GetAvailableGlobalCapacity(3);
        Assert.AreEqual(0, capacity); // Not negative
    }

    [Test]
    public void ResetForRaidClosure_ClearsAllState()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        _tracker.Register(2u, EnemyPopulationOrigin.Reinforcement, 2);

        _tracker.ResetForRaidClosure();

        Assert.AreEqual(0, _tracker.TotalActivePopulation);
        Assert.AreEqual(0, _tracker.ActiveReinforcements);
        Assert.AreEqual(0, _tracker.GetActiveThreatByOrigin(EnemyPopulationOrigin.Bootstrap));
        Assert.AreEqual(0, _tracker.GetActiveThreatByOrigin(EnemyPopulationOrigin.Reinforcement));
    }
}
