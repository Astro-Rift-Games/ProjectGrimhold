#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections.Generic;

namespace Tests.PlayMode
{
    /// <summary>
    /// Runner-scoped team pool for fixtures that exercise extraction contribution sources without
    /// composing <c>NetworkMatchController</c>, whose Dungeon Pressure and reinforcement directors
    /// would add unrelated simulation to the fixture.
    /// </summary>
    internal sealed class PlayModeTeamExtractionProgress : ITeamExtractionProgress
    {
        internal const int FixtureEntityId = int.MaxValue - 1;

        private readonly Dictionary<RaidTeamId, int> _progressByTeam = new();
        private readonly int _quota;

        internal PlayModeTeamExtractionProgress(int quota = int.MaxValue)
        {
            _quota = quota;
        }

        public EntityId Id => new(FixtureEntityId);

        internal int AppliedContributionCount { get; private set; }

        public bool TryGetProgress(RaidTeamId teamId, out int currentProgress, out int quota)
        {
            quota = _quota;
            if (!teamId.IsValid)
            {
                currentProgress = 0;
                return false;
            }

            _progressByTeam.TryGetValue(teamId, out currentProgress);
            return true;
        }

        public bool TryGetSnapshot(RaidTeamId teamId, out ExtractionProgressSnapshot snapshot)
        {
            if (!TryGetProgress(teamId, out int currentProgress, out int quota))
            {
                snapshot = default;
                return false;
            }

            snapshot = new ExtractionProgressSnapshot(currentProgress, quota, false);
            return true;
        }

        public bool TryApplyContribution(RaidTeamId teamId, long amount, out int nextProgress, out bool completedQuota)
        {
            nextProgress = 0;
            completedQuota = false;
            if (!teamId.IsValid || amount <= 0)
            {
                return false;
            }

            _progressByTeam.TryGetValue(teamId, out int currentProgress);
            nextProgress = (int)Math.Min(_quota, currentProgress + amount);
            completedQuota = currentProgress < _quota && nextProgress >= _quota;
            _progressByTeam[teamId] = nextProgress;
            AppliedContributionCount++;
            return true;
        }

        public bool TryInitializeFromRoster(RaidTeamRoster roster) => true;

        public bool IsAssignedToTeam(RaidTeamId teamId) => teamId.IsValid;
    }
}
#endif
