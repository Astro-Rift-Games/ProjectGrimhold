using UnityEngine;

namespace Spawning
{
    public sealed class ReinforcementPointRegistry
    {
        private Transform[] _points = new Transform[0];

        public Transform[] Points => _points;

        public void Initialize(Transform[] spawnPoints)
        {
            _points = spawnPoints ?? new Transform[0];
        }

        public void ResetForRaidClosure()
        {
            _points = new Transform[0];
        }
    }
}
