using System;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Scenario
{
    public sealed class TeamExtractionCoordinatorTests
    {
        private GameObject _root;
        private TeamExtractionCoordinator _coordinator;
        private ExtractionConfig _config;
        private EntityRegistry _registry;
        private MockSanctuary _sanctuary;
        private MockZone _zone;
        private MockAssignmentService _assignmentService;

        private GameObject _player1Obj;
        private PlayerExtractionController _player1Controller;
        private MockCharacter _char1;

        private GameObject _player2Obj;
        private PlayerExtractionController _player2Controller;
        private MockCharacter _char2;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestCoordinatorRoot");
            _coordinator = _root.AddComponent<TeamExtractionCoordinator>();

            _config = ScriptableObject.CreateInstance<ExtractionConfig>();
            SetField(_config, "_countdownDurationSeconds", 3f);
            SetField(_config, "_cancelWhenLeavingArea", true);
            SetField(_config, "_boundaryTolerance", 0.5f);
            SetField(_config, "_soloProgressQuota", 100);
            SetField(_config, "_duoProgressQuota", 150);
            SetField(_config, "_maxTeamsPerRaid", 4);

            _registry = _root.AddComponent<EntityRegistry>();
            _sanctuary = _root.AddComponent<MockSanctuary>();
            _zone = _root.AddComponent<MockZone>();
            _assignmentService = _root.AddComponent<MockAssignmentService>();

            var sanctuaryId = new EntityId(50);
            _sanctuary.SanctuaryId = sanctuaryId;
            _zone.ZoneId = sanctuaryId;

            _registry.TryRegisterExtractionSanctuary(sanctuaryId, _sanctuary);
            _registry.TryRegisterExtractionZone(sanctuaryId, _zone);

            _coordinator.SetDependenciesForTest(_registry, null, _assignmentService, null, _config);

            // Set up player 1
            _player1Obj = new GameObject("Player1");
            _player1Obj.AddComponent<NetworkObject>();
            _player1Obj.AddComponent<PlayerMovementNetworkController>();
            _player1Obj.AddComponent<PlayerCombatNetworkController>();
            _char1 = _player1Obj.AddComponent<MockCharacter>();
            _player1Controller = _player1Obj.AddComponent<PlayerExtractionController>();
            SetField(_player1Controller, "_config", _config);
            SetField(_player1Controller, "_characterSource", _char1);

            // Set up player 2
            _player2Obj = new GameObject("Player2");
            _player2Obj.AddComponent<NetworkObject>();
            _player2Obj.AddComponent<PlayerMovementNetworkController>();
            _player2Obj.AddComponent<PlayerCombatNetworkController>();
            _char2 = _player2Obj.AddComponent<MockCharacter>();
            _player2Controller = _player2Obj.AddComponent<PlayerExtractionController>();
            SetField(_player2Controller, "_config", _config);
            SetField(_player2Controller, "_characterSource", _char2);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_player1Obj);
            UnityEngine.Object.DestroyImmediate(_player2Obj);
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void Solo_AllConditionsMet_StartsCountdown()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);

            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                if (tid != team1) return false;
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                ctrls.Add(_player1Controller);
                return true;
            };

            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.InProgress));
            Assert.That(_player1Controller.ActiveZoneId, Is.EqualTo(_sanctuary.SanctuaryId));
        }

        [Test]
        public void Solo_LeavesArea_CancelsCountdown()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            bool isInside = true;
            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: isInside, isInsideTolerant: isInside));
                ctrls.Add(_player1Controller);
                return true;
            };

            // Start countdown
            _coordinator.FixedUpdateNetwork();
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.InProgress));

            // Step outside
            isInside = false;
            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.False);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.None));
        }

        [Test]
        public void Solo_Disconnects_CancelsCountdown()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            bool isConnected = true;
            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: isConnected, isInsideExact: true, isInsideTolerant: true));
                ctrls.Add(_player1Controller);
                return true;
            };

            _coordinator.FixedUpdateNetwork();
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);

            // Disconnects (fail-closed)
            isConnected = false;
            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.False);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.None));
        }

        [Test]
        public void Solo_CountdownExpires_CompletesExtraction()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                ctrls.Add(_player1Controller);
                return true;
            };

            _coordinator.FixedUpdateNetwork();
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);

            // Simulate timer expiration
            _coordinator.TimerExpiredOverrideForTest = tid => true;

            bool completionEventFired = false;
            _player1Controller.ExtractionCompleted += _ => completionEventFired = true;

            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.False);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.Extracted));
            Assert.That(completionEventFired, Is.True);
        }

        [Test]
        public void Duo_BothInsideAndConnected_StartsCountdown()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            var part2 = MakeParticipantId(2);

            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                snaps.Add(new TeamMemberExtractionSnapshot(part2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                ctrls.Add(_player1Controller);
                ctrls.Add(_player2Controller);
                return true;
            };

            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.InProgress));
            Assert.That(_player2Controller.State, Is.EqualTo(ExtractionState.InProgress));
        }

        [Test]
        public void Duo_TeammateOutside_DoesNotStartCountdown()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            var part2 = MakeParticipantId(2);

            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                // Teammate is outside area!
                snaps.Add(new TeamMemberExtractionSnapshot(part2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: false, isInsideTolerant: false));
                ctrls.Add(_player1Controller);
                ctrls.Add(_player2Controller);
                return true;
            };

            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.False);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.None));
            Assert.That(_player2Controller.State, Is.EqualTo(ExtractionState.None));
        }

        [Test]
        public void Duo_TeammateLeavesDuringCountdown_CancelsCountdownForBoth()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            var part2 = MakeParticipantId(2);
            bool teammateInside = true;

            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                snaps.Add(new TeamMemberExtractionSnapshot(part2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: teammateInside, isInsideTolerant: teammateInside));
                ctrls.Add(_player1Controller);
                ctrls.Add(_player2Controller);
                return true;
            };

            // Start countdown
            _coordinator.FixedUpdateNetwork();
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);

            // Teammate leaves
            teammateInside = false;
            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.False);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.None));
            Assert.That(_player2Controller.State, Is.EqualTo(ExtractionState.None));
        }

        [Test]
        public void Duo_TeammateDisconnectsDuringCountdown_CancelsCountdownForBoth()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            var part2 = MakeParticipantId(2);
            bool teammateConnected = true;

            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                snaps.Add(new TeamMemberExtractionSnapshot(part2, RaidParticipantState.Raiding, isConnected: teammateConnected, isInsideExact: true, isInsideTolerant: true));
                ctrls.Add(_player1Controller);
                ctrls.Add(_player2Controller);
                return true;
            };

            _coordinator.FixedUpdateNetwork();
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);

            // Teammate disconnects
            teammateConnected = false;
            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.False);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.None));
            Assert.That(_player2Controller.State, Is.EqualTo(ExtractionState.None));
        }

        [Test]
        public void Duo_DownedTeammateInsideAndConnected_ExtractsBothTogether()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            var part2 = MakeParticipantId(2);

            _char2.IsDownedState = true; // Downed player

            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                // Downed player is still in Raiding state, connected, and inside zone
                snaps.Add(new TeamMemberExtractionSnapshot(part2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                ctrls.Add(_player1Controller);
                ctrls.Add(_player2Controller);
                return true;
            };

            _coordinator.FixedUpdateNetwork();
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);

            // Complete countdown
            _coordinator.TimerExpiredOverrideForTest = tid => true;

            _coordinator.FixedUpdateNetwork();

            Assert.That(_coordinator.IsTeamExtracting(team1), Is.False);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.Extracted));
            Assert.That(_player2Controller.State, Is.EqualTo(ExtractionState.Extracted));
        }

        [Test]
        public void Duo_TeammateDefeated_CompositionChanges_RestartsCountdownImmediatelyForSurvivor()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            var part2 = MakeParticipantId(2);
            var teammateState = RaidParticipantState.Raiding;

            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                snaps.Add(new TeamMemberExtractionSnapshot(part2, teammateState, isConnected: teammateState == RaidParticipantState.Raiding, isInsideExact: true, isInsideTolerant: true));
                ctrls.Add(_player1Controller);
                if (teammateState == RaidParticipantState.Raiding)
                {
                    ctrls.Add(_player2Controller);
                }
                return true;
            };

            // Start countdown with duo [1, 2]
            _coordinator.FixedUpdateNetwork();
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);
            TeamCompositionSignature originalSignature = _coordinator.GetSlotForTest(0).ActiveSignature;

            // Teammate confirmed absent / defeated -> transitions to Defeated (out of required composition)
            teammateState = RaidParticipantState.Defeated;
            _coordinator.FixedUpdateNetwork();

            // Coordinator should have immediately restarted with new single-member signature!
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);
            TeamCompositionSignature newSignature = _coordinator.GetSlotForTest(0).ActiveSignature;
            Assert.That(newSignature, Is.Not.EqualTo(originalSignature));
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.InProgress));
        }

        [Test]
        public void TwoIndependentTeams_CanExtractIndependentlyInSameSanctuary()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out RaidTeamId team2), Is.True);
            _coordinator.TryInitializeTeam(team1);
            _coordinator.TryInitializeTeam(team2);

            // Both assigned to same sanctuary (D-3)
            _assignmentService.Assignments[team1] = _sanctuary.SanctuaryId;
            _assignmentService.Assignments[team2] = _sanctuary.SanctuaryId;

            // Only team 1 completed ritual
            _sanctuary.CompletedTeams.Add(team1);

            var part1 = MakeParticipantId(1);
            var part2 = MakeParticipantId(2);

            _coordinator.MemberSnapshotsProviderForTest = (tid, zone, snaps, ctrls) =>
            {
                if (tid == team1)
                {
                    snaps.Add(new TeamMemberExtractionSnapshot(part1, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                    ctrls.Add(_player1Controller);
                    return true;
                }
                if (tid == team2)
                {
                    snaps.Add(new TeamMemberExtractionSnapshot(part2, RaidParticipantState.Raiding, isConnected: true, isInsideExact: true, isInsideTolerant: true));
                    ctrls.Add(_player2Controller);
                    return true;
                }
                return false;
            };

            _coordinator.FixedUpdateNetwork();

            // Team 1 starts countdown; Team 2 is blocked (ritual not complete)
            Assert.That(_coordinator.IsTeamExtracting(team1), Is.True);
            Assert.That(_coordinator.IsTeamExtracting(team2), Is.False);
            Assert.That(_player1Controller.State, Is.EqualTo(ExtractionState.InProgress));
            Assert.That(_player2Controller.State, Is.EqualTo(ExtractionState.None));
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private static RaidParticipantId MakeParticipantId(int value)
        {
            Assert.That(RaidParticipantId.TryCreate(value, out RaidParticipantId id), Is.True);
            return id;
        }

        private sealed class MockSanctuary : MonoBehaviour, IExtractionSanctuary
        {
            public EntityId SanctuaryId { get; set; }
            public HashSet<RaidTeamId> CompletedTeams = new();

            public EntityId Id => SanctuaryId;
            public bool IsAnyRitualCompleted => CompletedTeams.Count > 0;
            public bool IsReserved => CompletedTeams.Count > 0;
            public EntityId OwnerId => default;
            public ExtractionRitualState RitualState => ExtractionRitualState.NotStarted;

            public bool IsAssignedToTeam(RaidTeamId teamId) => true;
            public bool TryAssignTeam(RaidTeamId teamId) => true;
            public bool CanUseExtraction(RaidTeamId teamId) => CompletedTeams.Contains(teamId);
            public bool CanUseExtraction(EntityId participantId) => true;
            public bool IsOwnedBy(EntityId playerId) => true;
            public bool TryReserve(EntityId playerId) => true;
            public bool TryGetRitualProgress(RaidTeamId teamId, out ExtractionRitualSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }
            public bool TryGetRitualProgress(out ExtractionRitualSnapshot snapshot)
            {
                snapshot = default;
                return false;
            }
        }

        private sealed class MockZone : MonoBehaviour, IExtractionZone
        {
            public EntityId ZoneId { get; set; }
            public EntityId Id => ZoneId;
            public bool IsAvailable => true;
            public bool ContainsExact(Vector2 point) => true;
            public bool ContainsWithTolerance(Vector2 point, float tolerance) => true;
            public bool TrySetAvailability(bool available) => true;
            public bool IsOccupant(EntityId participantId) => true;
        }

        private sealed class MockAssignmentService : ExtractionSanctuaryAssignmentService
        {
            public Dictionary<RaidTeamId, EntityId> Assignments = new();

            public override SanctuaryAssignmentResult TryGetAssignment(RaidTeamId teamId)
            {
                if (Assignments.TryGetValue(teamId, out EntityId id))
                {
                    return SanctuaryAssignmentResult.Assigned(teamId, id, true);
                }
                return SanctuaryAssignmentResult.Rejected(teamId, SanctuaryAssignmentFailureReason.NoSanctuariesConfigured);
            }
        }

        private sealed class MockCharacter : MonoBehaviour, ICharacter
        {
            public EntityId Id => new EntityId(1);
            public bool IsAliveState = true;
            public bool IsDownedState = false;

            public bool IsAlive => IsAliveState;
            public bool IsDowned => IsDownedState;
        }
    }
}
