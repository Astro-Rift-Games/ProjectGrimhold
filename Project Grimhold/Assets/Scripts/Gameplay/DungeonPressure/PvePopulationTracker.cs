using System.Collections.Generic;
using Spawning;

public sealed class PvePopulationTracker
{
    private readonly HashSet<EnemyCharacter> _activeEnemies = new HashSet<EnemyCharacter>();
    private int _reinforcementsCount = 0;

    public int TotalActivePopulation => _activeEnemies.Count;
    public int ActiveReinforcements => _reinforcementsCount;

    public void Register(EnemyCharacter enemy)
    {
        if (enemy == null || !enemy.IsAlive) return;

        if (_activeEnemies.Add(enemy))
        {
            if (enemy.PopulationOrigin == EnemyPopulationOrigin.Reinforcement)
            {
                _reinforcementsCount++;
            }
            UnityEngine.Debug.Log($"[PveTracker] Enemigo registrado ({enemy.PopulationOrigin}). Total vivos: {_activeEnemies.Count} | Refuerzos vivos: {_reinforcementsCount}");
        }
    }

    public void Unregister(EnemyCharacter enemy)
    {
        if (enemy == null) return;

        if (_activeEnemies.Remove(enemy))
        {
            if (enemy.PopulationOrigin == EnemyPopulationOrigin.Reinforcement)
            {
                _reinforcementsCount--;
                if (_reinforcementsCount < 0) _reinforcementsCount = 0;
            }
            UnityEngine.Debug.Log($"[PveTracker] Enemigo desregistrado ({enemy.PopulationOrigin}). Total vivos: {_activeEnemies.Count} | Refuerzos vivos: {_reinforcementsCount}");
        }
    }

    public void ResetForRaidClosure()
    {
        _activeEnemies.Clear();
        _reinforcementsCount = 0;
    }
}
