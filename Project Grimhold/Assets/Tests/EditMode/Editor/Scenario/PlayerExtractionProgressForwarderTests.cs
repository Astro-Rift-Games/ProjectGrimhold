using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Scenario
{
    public sealed class PlayerExtractionProgressForwarderTests
    {
        private GameObject _runnerObject;
        private GameObject _playerObject;
        private PlayerExtractionProgressController _progressController;
        private ExtractionConfig _config;
        private MockTeamProgress _mockTeamProgress;
        private MockCharacter _mockCharacter;
        private EntityRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _runnerObject = new GameObject("RunnerHolder");
            _registry = _runnerObject.AddComponent<EntityRegistry>();
            ExtractionSanctuaryAssignmentService assignmentService =
                _runnerObject.AddComponent<ExtractionSanctuaryAssignmentService>();

            _config = ScriptableObject.CreateInstance<ExtractionConfig>();

            _playerObject = new GameObject("PlayerObject");
            _progressController = _playerObject.AddComponent<PlayerExtractionProgressController>();
            _mockCharacter = _playerObject.AddComponent<MockCharacter>();
            PlayerExtractionController extractionController = _playerObject.AddComponent<PlayerExtractionController>();

            // Set private fields via reflection
            SetField(_progressController, "_config", _config);
            SetField(_progressController, "_character", _mockCharacter);
            SetField(_progressController, "_extractionController", extractionController);
            SetField(_progressController, "_registry", _registry);
            SetField(_progressController, "_assignmentService", assignmentService);
            SetField(_progressController, "_dependenciesValid", true);

            _mockTeamProgress = new MockTeamProgress(new EntityId(100));
            _progressController.SetTeamProgressForTesting(_mockTeamProgress);

            RaidTeamId.TryCreate(1, out RaidTeamId teamId);
            _progressController.SetTeamIdOverrideForTesting(teamId);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_playerObject);
            Object.DestroyImmediate(_runnerObject);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void TryGetSnapshot_ForwardsDirectlyToTeamProgress()
        {
            _mockTeamProgress.Current = 60;
            _mockTeamProgress.Quota = 150;
            _mockTeamProgress.AssignmentRequested = false;

            Assert.That(_progressController.TryGetSnapshot(out ExtractionProgressSnapshot snapshot), Is.True);
            Assert.That(snapshot.CurrentProgress, Is.EqualTo(60));
            Assert.That(snapshot.Quota, Is.EqualTo(150));
            Assert.That(snapshot.AssignmentRequested, Is.False);

            Assert.That(_progressController.CurrentProgress, Is.EqualTo(60));
            Assert.That(_progressController.Quota, Is.EqualTo(150));
            Assert.That((bool)_progressController.AssignmentRequested, Is.False);
        }

        [Test]
        public void TryGetSnapshot_ReflectsCompletedQuotaAndAssignmentRequested()
        {
            _mockTeamProgress.Current = 150;
            _mockTeamProgress.Quota = 150;
            _mockTeamProgress.AssignmentRequested = true;

            Assert.That(_progressController.TryGetSnapshot(out ExtractionProgressSnapshot snapshot), Is.True);
            Assert.That(snapshot.CurrentProgress, Is.EqualTo(150));
            Assert.That(snapshot.IsQuotaComplete, Is.True);
            Assert.That(snapshot.AssignmentRequested, Is.True);

            Assert.That(_progressController.CurrentProgress, Is.EqualTo(150));
            Assert.That((bool)_progressController.AssignmentRequested, Is.True);
        }

        [Test]
        public void Quota_FallsBackToConfigSoloQuotaWhenTeamProgressMissing()
        {
            _progressController.SetTeamProgressForTesting(null);
            Assert.That(_progressController.Quota, Is.EqualTo(_config.SoloProgressQuota));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private sealed class MockTeamProgress : ITeamExtractionProgress
        {
            public int Current;
            public int Quota = 100;
            public bool AssignmentRequested;

            public MockTeamProgress(EntityId id) => Id = id;
            public EntityId Id { get; }

            public bool TryGetProgress(RaidTeamId teamId, out int currentProgress, out int quota)
            {
                currentProgress = Current;
                quota = Quota;
                return true;
            }

            public bool TryGetSnapshot(RaidTeamId teamId, out ExtractionProgressSnapshot snapshot)
            {
                snapshot = new ExtractionProgressSnapshot(Current, Quota, AssignmentRequested);
                return true;
            }

            public bool TryApplyContribution(RaidTeamId teamId, long amount, out int nextProgress, out bool completedQuota)
            {
                Current += (int)amount;
                nextProgress = Current;
                completedQuota = Current >= Quota;
                if (completedQuota)
                {
                    AssignmentRequested = true;
                }
                return true;
            }

            public bool TryInitializeFromRoster(RaidTeamRoster roster) => true;
            public bool IsAssignedToTeam(RaidTeamId teamId) => true;
        }

        private sealed class MockCharacter : MonoBehaviour, ICharacter
        {
            public bool IsAliveValue = true;
            public EntityId Id => new(1);
            public bool IsAlive => IsAliveValue;
        }
    }
}
