using UnityEngine;

namespace Spawning
{
    public sealed class ReinforcementPointRegistry
    {
        private Transform[] _points = new Transform[0];

        public Transform[] Points => _points;

        public bool Initialize(Transform[] reinforcementPoints, Transform[] enemyPoints, out System.Collections.Generic.List<string> diagnostics)
        {
            diagnostics = new System.Collections.Generic.List<string>();
            bool isValid = true;
            
            if (reinforcementPoints == null)
            {
                _points = new Transform[0];
                return true; // Empty is technically not invalid for the registry itself.
            }

            var seenReinforcements = new System.Collections.Generic.HashSet<Transform>();
            var seenEnemies = new System.Collections.Generic.HashSet<Transform>();
            
            if (enemyPoints != null)
            {
                foreach (var ep in enemyPoints)
                {
                    if (ep != null)
                    {
                        seenEnemies.Add(ep);
                    }
                }
            }

            for (int i = 0; i < reinforcementPoints.Length; i++)
            {
                var pt = reinforcementPoints[i];
                if (pt == null)
                {
                    diagnostics.Add($"[Reinforcements] Point at index {i} is null.");
                    isValid = false;
                    continue;
                }
                
                if (seenReinforcements.Contains(pt))
                {
                    diagnostics.Add($"[Reinforcements] Duplicate point at index {i}: {pt.name}.");
                    isValid = false;
                }
                else
                {
                    seenReinforcements.Add(pt);
                }

                if (seenEnemies.Contains(pt))
                {
                    diagnostics.Add($"[Reinforcements] Point at index {i} ({pt.name}) is already present in the Enemies group.");
                    isValid = false;
                }
            }

            if (!isValid)
            {
                _points = new Transform[0];
                return false;
            }

            _points = reinforcementPoints;
            return true;
        }

        public void ResetForRaidClosure()
        {
            _points = new Transform[0];
        }
    }
}
