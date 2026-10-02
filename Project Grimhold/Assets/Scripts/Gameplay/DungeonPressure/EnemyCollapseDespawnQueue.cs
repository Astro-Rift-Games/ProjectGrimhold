using System.Collections.Generic;

public class EnemyCollapseDespawnQueue
{
    private readonly List<uint> _queue = new List<uint>();
    private readonly List<uint> _batchBuffer = new List<uint>();

    public int Remaining => _queue.Count;
    public IReadOnlyList<uint> CurrentBatch => _batchBuffer;

    public void Populate(PvePopulationTracker tracker)
    {
        tracker.GetActiveEnemyIds(_queue);
    }

    public void Clear()
    {
        _queue.Clear();
        _batchBuffer.Clear();
    }

    public void PrepareNextBatch(int count)
    {
        _batchBuffer.Clear();
        int processed = 0;
        int maxIndex = _queue.Count - 1;
        
        while (processed < count && maxIndex >= 0)
        {
            _batchBuffer.Add(_queue[maxIndex]);
            _queue.RemoveAt(maxIndex);
            processed++;
            maxIndex--;
        }
    }
}
