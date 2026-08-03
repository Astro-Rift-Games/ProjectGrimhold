using System;
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Scenario
{
    public sealed class SanctuaryAssignmentTests
    {
        private GameObject _runnerObject;
        private NetworkRunner _runner;
        private EntityRegistry _registry;
        private ExtractionSanctuaryAssignmentService _service;

        [SetUp]
        public void SetUp()
        {
            _runnerObject = new GameObject("Sanctuary assignment tests");
            _runner = _runnerObject.AddComponent<NetworkRunner>();
            _registry = _runnerObject.AddComponent<EntityRegistry>();
            _service = _runnerObject.AddComponent<ExtractionSanctuaryAssignmentService>();
            Assert.That(_service.Initialize(_runner, GameMode.Client), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_runnerObject);
        }

        [TestCase(0UL, int.MinValue, int.MinValue, 1)]
        [TestCase(0UL, int.MaxValue, int.MaxValue, 4)]
        [TestCase(ulong.MaxValue, -1, 1, 17)]
        public void SelectionPolicy_IsReproducibleAndAlwaysInRange(
            ulong seed,
            int tick,
            int playerValue,
            int count)
        {
            var playerId = new EntityId(playerValue);
            int first = SanctuarySelectionPolicy.SelectIndex(seed, tick, playerId, count);
            int second = SanctuarySelectionPolicy.SelectIndex(seed, tick, playerId, count);

            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.GreaterThanOrEqualTo(0).And.LessThan(count));
        }

        [Test]
        public void SelectionPolicy_RejectsInvalidInputs()
        {
            Assert.Throws<ArgumentException>(() =>
                SanctuarySelectionPolicy.SelectIndex(1UL, 1, default, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SanctuarySelectionPolicy.SelectIndex(1UL, 1, new EntityId(1), 0));
        }

        [Test]
        public void Result_IsImmutableAndDistinguishesExistingAssignment()
        {
            var playerId = new EntityId(11);
            var sanctuaryId = new EntityId(22);
            SanctuaryAssignmentResult result = SanctuaryAssignmentResult.Assigned(playerId, sanctuaryId, true);

            Assert.That(result.Success, Is.True);
            Assert.That(result.PlayerId, Is.EqualTo(playerId));
            Assert.That(result.SanctuaryId, Is.EqualTo(sanctuaryId));
            Assert.That(result.IsExistingAssignment, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(SanctuaryAssignmentFailureReason.None));
            Assert.That(typeof(SanctuaryAssignmentResult).IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), false), Is.True);
            foreach (PropertyInfo property in typeof(SanctuaryAssignmentResult).GetProperties())
            {
                Assert.That(property.CanWrite, Is.False, property.Name);
            }
        }

        [Test]
        public void Registry_ReaderAndSanctuaryCapabilitiesAreIsolated()
        {
            var id = new EntityId(31);
            var reader = new Reader(id, new ExtractionProgressSnapshot(100, 100, true));
            var conflictingReader = new Reader(id, default);
            var sanctuary = new Sanctuary(id, default);
            var conflictingSanctuary = new Sanctuary(id, default);

            Assert.That(_registry.TryRegisterExtractionProgressReader(id, reader), Is.True);
            Assert.That(_registry.TryRegisterExtractionProgressReader(id, reader), Is.True);
            Assert.That(_registry.TryRegisterExtractionProgressReader(id, conflictingReader), Is.False);
            Assert.That(_registry.TryRegisterExtractionSanctuary(id, sanctuary), Is.True);
            Assert.That(_registry.TryRegisterExtractionSanctuary(id, sanctuary), Is.True);
            Assert.That(_registry.TryRegisterExtractionSanctuary(id, conflictingSanctuary), Is.False);
            Assert.That(_registry.TryUnregisterExtractionProgressReader(id, conflictingReader), Is.False);
            Assert.That(_registry.TryUnregisterExtractionProgressReader(id, reader), Is.True);
            Assert.That(_registry.TryGetExtractionSanctuary(id, out IExtractionSanctuary resolved), Is.True);
            Assert.That(resolved, Is.SameAs(sanctuary));
            Assert.That(_registry.TryUnregisterExtractionSanctuary(id, conflictingSanctuary), Is.False);
            Assert.That(_registry.TryUnregisterExtractionSanctuary(id, sanctuary), Is.True);
        }

        [Test]
        public void TryGetAssignment_ReportsAbsenceDuplicateAndRegistryInconsistency()
        {
            var playerId = new EntityId(41);
            var first = new Sanctuary(new EntityId(101), playerId);
            var second = new Sanctuary(new EntityId(202), playerId);
            Register(first);

            SanctuaryAssignmentResult one = _service.TryGetAssignment(playerId);
            Assert.That(one.Success, Is.True);
            Assert.That(one.SanctuaryId, Is.EqualTo(first.Id));

            Register(second);
            LogAssert.Expect(UnityEngine.LogType.Error,
                "ExtractionSanctuaryAssignmentService configuration failure: DuplicateExistingAssignment.");
            SanctuaryAssignmentResult duplicate = _service.TryGetAssignment(playerId);
            Assert.That(duplicate.Success, Is.False);
            Assert.That(duplicate.FailureReason, Is.EqualTo(SanctuaryAssignmentFailureReason.DuplicateExistingAssignment));

            Assert.That(_registry.TryUnregisterExtractionSanctuary(first.Id, first), Is.True);
            LogAssert.Expect(UnityEngine.LogType.Error,
                "ExtractionSanctuaryAssignmentService configuration failure: SanctuaryRegistryInconsistent.");
            SanctuaryAssignmentResult inconsistent = _service.TryGetAssignment(new EntityId(99));
            Assert.That(inconsistent.FailureReason, Is.EqualTo(SanctuaryAssignmentFailureReason.SanctuaryRegistryInconsistent));
        }

        [Test]
        public void TryGetAssignment_AssignmentNotFoundIsNormalResult()
        {
            SanctuaryAssignmentResult result = _service.TryGetAssignment(new EntityId(51));
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SanctuaryAssignmentFailureReason.AssignmentNotFound));
            Assert.That(_service.HasSessionSeed, Is.False);
            Assert.That(_service.TryAssign(new EntityId(51)).FailureReason,
                Is.EqualTo(SanctuaryAssignmentFailureReason.NoAuthority));
        }

        [Test]
        public void SanctuaryRegistration_MaintainsStableAscendingIdentityOrder()
        {
            Register(new Sanctuary(new EntityId(303), default));
            Register(new Sanctuary(new EntityId(101), default));
            Register(new Sanctuary(new EntityId(202), default));

            FieldInfo field = typeof(ExtractionSanctuaryAssignmentService).GetField(
                "_sanctuaryIds",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var registeredIds = (List<EntityId>)field.GetValue(_service);

            Assert.That(registeredIds.Count, Is.EqualTo(3));
            Assert.That(registeredIds[0], Is.EqualTo(new EntityId(101)));
            Assert.That(registeredIds[1], Is.EqualTo(new EntityId(202)));
            Assert.That(registeredIds[2], Is.EqualTo(new EntityId(303)));
        }

        [Test]
        public void FailureReasons_ContainTheCompleteTask54Contract()
        {
            string[] expected =
            {
                "None", "ServiceNotInitialized", "InvalidPlayer", "NoAuthority", "OutsideSimulation",
                "AssignmentNotFound", "ProgressReaderUnavailable", "InvalidProgressSnapshot",
                "QuotaIncomplete", "AssignmentNotRequested", "PlayerUnavailable",
                "NoSanctuariesConfigured", "NoFreeSanctuary", "SanctuaryRegistryInconsistent",
                "DuplicateExistingAssignment", "ReservationConflict"
            };

            Assert.That(Enum.GetNames(typeof(SanctuaryAssignmentFailureReason)), Is.EqualTo(expected));
        }

        [Test]
        public void ConcreteSanctuary_RejectsInvalidAndAuthoritylessReservations()
        {
            var root = new GameObject("Authorityless sanctuary");
            root.AddComponent<NetworkObject>();
            root.AddComponent<BoxCollider2D>();
            root.AddComponent<ExtractionZone>();
            ExtractionSanctuary sanctuary = root.AddComponent<ExtractionSanctuary>();
            try
            {
                Assert.That(sanctuary.TryReserve(default), Is.False);
                Assert.That(sanctuary.TryReserve(new EntityId(1)), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private void Register(Sanctuary sanctuary)
        {
            Assert.That(_registry.TryRegisterExtractionSanctuary(sanctuary.Id, sanctuary), Is.True);
            Assert.That(_service.TryRegisterSanctuary(sanctuary.Id, sanctuary), Is.True);
        }

        private sealed class Reader : IExtractionProgressReader
        {
            private readonly ExtractionProgressSnapshot _snapshot;

            public Reader(EntityId id, ExtractionProgressSnapshot snapshot)
            {
                Id = id;
                _snapshot = snapshot;
            }

            public EntityId Id { get; }
            public bool TryGetSnapshot(out ExtractionProgressSnapshot snapshot)
            {
                snapshot = _snapshot;
                return true;
            }
        }

        private sealed class Sanctuary : IExtractionSanctuary
        {
            public Sanctuary(EntityId id, EntityId ownerId)
            {
                Id = id;
                OwnerId = ownerId;
            }

            public EntityId Id { get; }
            public EntityId OwnerId { get; private set; }
            public bool IsReserved => OwnerId.Value != 0;
            public ExtractionRitualState RitualState => ExtractionRitualState.NotStarted;
            public bool IsOwnedBy(EntityId playerId) => playerId.Value != 0 && OwnerId == playerId;
            public bool CanUseExtraction(EntityId playerId) => false;
            public bool TryGetRitualProgress(out ExtractionRitualSnapshot snapshot)
            {
                snapshot = new ExtractionRitualSnapshot(RitualState, 10f, 10f, 0f);
                return true;
            }
            public bool TryReserve(EntityId playerId)
            {
                if (playerId.Value == 0 || (IsReserved && OwnerId != playerId))
                {
                    return false;
                }

                OwnerId = playerId;
                return true;
            }
        }
    }
}
