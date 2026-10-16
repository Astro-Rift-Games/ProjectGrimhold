using System;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Scenario
{
    public sealed class TeamSanctuaryTests
    {
        private GameObject _root;
        private ExtractionSanctuary _sanctuary;
        private ExtractionZone _zone;
        private ExtractionConfig _config;
        private EntityRegistry _registry;
        private ExtractionSanctuaryAssignmentService _assignmentService;
        private BoxCollider2D _collider;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("TestSanctuary");
            _root.AddComponent<NetworkObject>();
            _collider = _root.AddComponent<BoxCollider2D>();
            _zone = _root.AddComponent<ExtractionZone>();
            _sanctuary = _root.AddComponent<ExtractionSanctuary>();

            _config = ScriptableObject.CreateInstance<ExtractionConfig>();
            SetField(_config, "_ritualDurationSeconds", 5f);
            SetField(_config, "_soloProgressQuota", 100);
            SetField(_config, "_duoProgressQuota", 150);
            SetField(_config, "_maxTeamsPerRaid", 4);

            _registry = _root.AddComponent<EntityRegistry>();
            _assignmentService = _root.AddComponent<ExtractionSanctuaryAssignmentService>();

            SetField(_sanctuary, "_config", _config);
            SetField(_sanctuary, "_extractionZone", _zone);
            SetField(_sanctuary, "_interactionCollider", _collider);
            SetField(_sanctuary, "_registry", _registry);
            SetField(_sanctuary, "_assignmentService", _assignmentService);
            SetField(_sanctuary, "_compositionValid", true);
            _sanctuary.SetAllowEditModeSimulation(true);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void TryAssignTeam_AssignsSlotAndIsIdempotent()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);

            Assert.That(_sanctuary.IsAssignedToTeam(team1), Is.False);
            Assert.That(_sanctuary.TryAssignTeam(team1), Is.True);
            Assert.That(_sanctuary.IsAssignedToTeam(team1), Is.True);

            // Re-assigning same team is idempotent
            Assert.That(_sanctuary.TryAssignTeam(team1), Is.True);
            Assert.That(_sanctuary.IsAssignedToTeam(team1), Is.True);
        }

        [Test]
        public void TryAssignTeam_MultipleTeamsCanShareSanctuary()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out RaidTeamId team2), Is.True);

            Assert.That(_sanctuary.TryAssignTeam(team1), Is.True);
            Assert.That(_sanctuary.TryAssignTeam(team2), Is.True);

            Assert.That(_sanctuary.IsAssignedToTeam(team1), Is.True);
            Assert.That(_sanctuary.IsAssignedToTeam(team2), Is.True);
            Assert.That(_sanctuary.IsReserved, Is.True);
        }

        [Test]
        public void CanInteract_RequiresAssignedTeamAndActiveAliveCharacter()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out RaidTeamId unassignedTeam), Is.True);
            _sanctuary.TryAssignTeam(team1);

            var characterObj = new GameObject("TestPlayer");
            MockPlayerCharacter mockChar = characterObj.AddComponent<MockPlayerCharacter>();
            var charId = new EntityId(10);
            mockChar.AssignedId = charId;
            mockChar.IsAliveState = true;
            mockChar.IsDownedState = false;

            Assert.That(_registry.TryRegisterEntity(charId, mockChar, null), Is.True);

            try
            {
                // Unassigned team cannot interact
                _sanctuary.OverrideTeamIdForTest = unassignedTeam;
                var request = new InteractionRequest(charId, _sanctuary.Id, 0);
                Assert.That(_sanctuary.CanInteract(request), Is.False);

                // Assigned team, active character can interact
                _sanctuary.OverrideTeamIdForTest = team1;
                Assert.That(_sanctuary.CanInteract(request), Is.True);

                // Downed character cannot initiate ritual
                mockChar.IsDownedState = true;
                Assert.That(_sanctuary.CanInteract(request), Is.False);

                // Dead character cannot initiate ritual
                mockChar.IsDownedState = false;
                mockChar.IsAliveState = false;
                Assert.That(_sanctuary.CanInteract(request), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(characterObj);
            }
        }

        [Test]
        public void Interact_StartsRitualOnlyForAssignedInteractingTeam()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            Assert.That(RaidTeamId.TryCreate(2, out RaidTeamId team2), Is.True);
            _sanctuary.TryAssignTeam(team1);
            _sanctuary.TryAssignTeam(team2);

            var characterObj = new GameObject("Player1");
            MockPlayerCharacter mockChar = characterObj.AddComponent<MockPlayerCharacter>();
            var charId = new EntityId(15);
            mockChar.AssignedId = charId;
            mockChar.IsAliveState = true;
            mockChar.IsDownedState = false;
            Assert.That(_registry.TryRegisterEntity(charId, mockChar, null), Is.True);

            try
            {
                _sanctuary.OverrideTeamIdForTest = team1;
                var request = new InteractionRequest(charId, _sanctuary.Id, 0);

                InteractionResult result = _sanctuary.Interact(request);
                Assert.That(result.Success, Is.True);

                Assert.That(_sanctuary.TryGetRitualProgress(team1, out ExtractionRitualSnapshot snap1), Is.True);
                Assert.That(snap1.State, Is.EqualTo(ExtractionRitualState.InProgress));

                Assert.That(_sanctuary.TryGetRitualProgress(team2, out ExtractionRitualSnapshot snap2), Is.True);
                Assert.That(snap2.State, Is.EqualTo(ExtractionRitualState.NotStarted));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(characterObj);
            }
        }

        [Test]
        public void RitualProgression_CancelsOnlyWhenRemainingRequiredCountReachesZero()
        {
            Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId team1), Is.True);
            _sanctuary.TryAssignTeam(team1);

            var characterObj = new GameObject("Player");
            MockPlayerCharacter mockChar = characterObj.AddComponent<MockPlayerCharacter>();
            var charId = new EntityId(20);
            mockChar.AssignedId = charId;
            mockChar.IsAliveState = true;
            mockChar.IsDownedState = false;
            Assert.That(_registry.TryRegisterEntity(charId, mockChar, null), Is.True);

            try
            {
                _sanctuary.OverrideTeamIdForTest = team1;
                var request = new InteractionRequest(charId, _sanctuary.Id, 0);
                Assert.That(_sanctuary.Interact(request).Success, Is.True);

                // Provide 1 remaining member: ritual does NOT cancel
                _sanctuary.RequiredMemberCountProviderForTest = _ => 1;
                _sanctuary.FixedUpdateNetwork();

                Assert.That(_sanctuary.TryGetRitualProgress(team1, out ExtractionRitualSnapshot snap), Is.True);
                Assert.That(snap.State, Is.EqualTo(ExtractionRitualState.InProgress));

                // When remaining required count reaches 0, ritual cancels per A-4 / D-5
                _sanctuary.RequiredMemberCountProviderForTest = _ => 0;
                _sanctuary.FixedUpdateNetwork();

                Assert.That(_sanctuary.TryGetRitualProgress(team1, out ExtractionRitualSnapshot cancelledSnap), Is.True);
                Assert.That(cancelledSnap.State, Is.EqualTo(ExtractionRitualState.Cancelled));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(characterObj);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private sealed class MockPlayerCharacter : MonoBehaviour, ICharacter
        {
            public EntityId AssignedId { get; set; }
            public bool IsAliveState = true;
            public bool IsDownedState = false;

            public EntityId Id => AssignedId;
            public bool IsAlive => IsAliveState;
            public bool IsDowned => IsDownedState;
        }
    }
}
