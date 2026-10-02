using System.Collections.Generic;
using Spawning;

public sealed class PvePopulationTracker
{
    private struct EnemyEntry
    {
        public EnemyPopulationOrigin Origin;
        public int Cost;
    }

    private readonly Dictionary<uint, EnemyEntry> _activeEnemies = new Dictionary<uint, EnemyEntry>();

    private int _totalPopulation = 0;
    
    private readonly Dictionary<EnemyPopulationOrigin, int> _populationByOrigin = new Dictionary<EnemyPopulationOrigin, int>();
    private readonly Dictionary<EnemyPopulationOrigin, int> _threatByOrigin = new Dictionary<EnemyPopulationOrigin, int>();

    public int TotalActivePopulation => _totalPopulation;
    
    public int ActiveReinforcements => GetActivePopulationByOrigin(EnemyPopulationOrigin.Reinforcement);

    public void Register(uint id, EnemyPopulationOrigin origin, int cost)
    {
        if (_activeEnemies.ContainsKey(id)) return;
        
        _activeEnemies[id] = new EnemyEntry { Origin = origin, Cost = cost };
        
        _totalPopulation++;

        if (!_populationByOrigin.ContainsKey(origin)) _populationByOrigin[origin] = 0;
        if (!_threatByOrigin.ContainsKey(origin)) _threatByOrigin[origin] = 0;

        _populationByOrigin[origin]++;
        _threatByOrigin[origin] += cost;
    }

    public void Unregister(uint id)
    {
        if (!_activeEnemies.TryGetValue(id, out var entry)) return;

        _activeEnemies.Remove(id);
        
        _totalPopulation--;
        
        if (_populationByOrigin.ContainsKey(entry.Origin))
            _populationByOrigin[entry.Origin]--;
        
        if (_threatByOrigin.ContainsKey(entry.Origin))
            _threatByOrigin[entry.Origin] -= entry.Cost;
    }

    public int GetActivePopulationByOrigin(EnemyPopulationOrigin origin)
    {
        return _populationByOrigin.TryGetValue(origin, out int count) ? count : 0;
    }

    public int GetActiveThreatByOrigin(EnemyPopulationOrigin origin)
    {
        return _threatByOrigin.TryGetValue(origin, out int threat) ? threat : 0;
    }

    public int GetAvailableCapacity(int budget, EnemyPopulationOrigin origin)
    {
        int activeThreat = GetActiveThreatByOrigin(origin);
        int available = budget - activeThreat;
        return available < 0 ? 0 : available;
    }

    public int GetAvailableGlobalCapacity(int maxActive)
    {
        int available = maxActive - _totalPopulation;
        return available < 0 ? 0 : available;
    }

    public void GetActiveEnemyIds(List<uint> buffer)
    {
        buffer.Clear();
        foreach (var id in _activeEnemies.Keys)
        {
            buffer.Add(id);
        }
    }

    public void ResetForRaidClosure()
    {
        _activeEnemies.Clear();
        _totalPopulation = 0;
        _populationByOrigin.Clear();
        _threatByOrigin.Clear();
    }

    public static bool TryReconcile(int restoredAliveCount, int trackerCount, out string discrepancyLog)
    {
        if (restoredAliveCount != trackerCount)
        {
            discrepancyLog = $"Host Migration population reconciliation mismatch: Restored alive enemies = {restoredAliveCount}, Tracker population = {trackerCount}.";
            return false;
        }
        
        discrepancyLog = $"Host Migration population reconciliation successful. Active population: {restoredAliveCount}.";
        return true;
    }
}
