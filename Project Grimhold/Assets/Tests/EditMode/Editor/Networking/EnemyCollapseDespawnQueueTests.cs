using System.Collections.Generic;
using NUnit.Framework;
using Spawning;

public class EnemyCollapseDespawnQueueTests
{
    private PvePopulationTracker _tracker;
    private EnemyCollapseDespawnQueue _queue;

    [SetUp]
    public void Setup()
    {
        _tracker = new PvePopulationTracker();
        _queue = new EnemyCollapseDespawnQueue();
    }

    [Test]
    public void Populate_FillsQueueWithActiveEnemies()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        _tracker.Register(2u, EnemyPopulationOrigin.Reinforcement, 1);

        _queue.Populate(_tracker);

        Assert.AreEqual(2, _queue.Remaining);
    }

    [Test]
    public void PrepareNextBatch_PullsExactlyRequestedAmountAndLeavesRest()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        _tracker.Register(2u, EnemyPopulationOrigin.Bootstrap, 1);
        _tracker.Register(3u, EnemyPopulationOrigin.Reinforcement, 1);
        
        _queue.Populate(_tracker); // 3 items
        
        _queue.PrepareNextBatch(2);

        Assert.AreEqual(2, _queue.CurrentBatch.Count);
        Assert.AreEqual(1, _queue.Remaining);
    }

    [Test]
    public void PrepareNextBatch_WhenRequestedMoreThanRemaining_PullsOnlyRemaining()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        _queue.Populate(_tracker); // 1 item

        _queue.PrepareNextBatch(5);

        Assert.AreEqual(1, _queue.CurrentBatch.Count);
        Assert.AreEqual(0, _queue.Remaining);
    }

    [Test]
    public void Clear_EmptiesBothQueueAndBatchBuffer()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        _queue.Populate(_tracker);
        _queue.PrepareNextBatch(1);
        
        Assert.AreEqual(1, _queue.CurrentBatch.Count);

        _queue.Clear();

        Assert.AreEqual(0, _queue.Remaining);
        Assert.AreEqual(0, _queue.CurrentBatch.Count);
    }

    [Test]
    public void Populate_WhenCalledMidProcess_OverwritesQueueWithNewState()
    {
        _tracker.Register(1u, EnemyPopulationOrigin.Bootstrap, 1);
        _tracker.Register(2u, EnemyPopulationOrigin.Bootstrap, 1);
        _queue.Populate(_tracker); // 2 items in queue

        _queue.PrepareNextBatch(1); // 1 item in batch, 1 remaining
        
        // Mid-process (e.g. host migration), the tracker gets a different set
        _tracker.ResetForRaidClosure();
        _tracker.Register(3u, EnemyPopulationOrigin.Reinforcement, 1);
        _tracker.Register(4u, EnemyPopulationOrigin.Reinforcement, 1);
        _tracker.Register(5u, EnemyPopulationOrigin.Reinforcement, 1);

        _queue.Populate(_tracker); // Should clear the old 1 remaining and populate 3

        Assert.AreEqual(3, _queue.Remaining);
        
        // Ensure previous batch buffer is not accidentally kept if we just do PrepareNextBatch
        _queue.PrepareNextBatch(5);
        Assert.AreEqual(3, _queue.CurrentBatch.Count); // only the new 3
        Assert.AreEqual(0, _queue.Remaining);
    }
}
