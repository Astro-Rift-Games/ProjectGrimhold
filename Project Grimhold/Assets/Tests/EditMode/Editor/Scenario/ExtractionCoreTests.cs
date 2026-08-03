using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Editor.Scenario
{
    [TestFixture]
    public sealed class ExtractionCoreTests
    {
        [Test]
        public void ExtractionCountdownSnapshot_ConservesActiveZoneIdAndRemainingSeconds()
        {
            EntityId zoneId = new EntityId(1027);
            ExtractionCountdownSnapshot snapshot = new ExtractionCountdownSnapshot(
                ExtractionState.InProgress,
                zoneId,
                remainingSeconds: 3.5f,
                totalSeconds: 5.0f,
                progress: 0.3f
            );

            Assert.AreEqual(ExtractionState.InProgress, snapshot.State);
            Assert.AreEqual(zoneId, snapshot.ActiveZoneId);
            Assert.AreEqual(3.5f, snapshot.RemainingSeconds);
            Assert.AreEqual(5.0f, snapshot.TotalSeconds);
            Assert.AreEqual(0.3f, snapshot.Progress);
            Assert.AreEqual(1.5f, snapshot.ElapsedSeconds);
        }

        [Test]
        public void ExtractionCountdownSnapshot_ClampsNegativeValuesDefensively()
        {
            ExtractionCountdownSnapshot snapshot = new ExtractionCountdownSnapshot(
                ExtractionState.InProgress,
                new EntityId(5),
                remainingSeconds: -2.0f,
                totalSeconds: -10.0f,
                progress: 1.5f
            );

            Assert.AreEqual(0f, snapshot.RemainingSeconds);
            Assert.AreEqual(0f, snapshot.TotalSeconds);
            Assert.AreEqual(1.0f, snapshot.Progress);
        }

        [Test]
        public void ExtractionCountdownSnapshot_NoneAndExtractedFactories_ReturnDefaultValues()
        {
            ExtractionCountdownSnapshot none = ExtractionCountdownSnapshot.None();
            Assert.AreEqual(ExtractionState.None, none.State);
            Assert.AreEqual(0, none.ActiveZoneId.Value);

            EntityId completedZoneId = new EntityId(72);
            ExtractionCountdownSnapshot extracted = ExtractionCountdownSnapshot.Extracted(completedZoneId);
            Assert.AreEqual(ExtractionState.Extracted, extracted.State);
            Assert.AreEqual(completedZoneId, extracted.ActiveZoneId);
            Assert.AreEqual(1.0f, extracted.Progress);
        }

        [Test]
        public void EntityRegistry_ExtractionCapabilitiesComposeAndUnregisterExpectedInstances()
        {
            GameObject registryObject = new GameObject("Registry");
            GameObject zoneObject = new GameObject("Zone");
            GameObject participantObject = new GameObject("Participant");
            GameObject obsoleteObject = new GameObject("Obsolete");

            try
            {
                EntityRegistry registry = registryObject.AddComponent<EntityRegistry>();
                EntityId id = new EntityId(27);
                TestZone zone = zoneObject.AddComponent<TestZone>();
                zone.IdValue = id.Value;
                TestParticipant participant = participantObject.AddComponent<TestParticipant>();
                participant.IdValue = id.Value;
                TestParticipant obsolete = obsoleteObject.AddComponent<TestParticipant>();
                obsolete.IdValue = id.Value;

                Assert.IsTrue(registry.TryRegisterExtractionZone(id, zone));
                Assert.IsTrue(registry.TryRegisterExtractionParticipant(id, participant));
                Assert.IsTrue(registry.TryGetExtractionZone(id, out IExtractionZone resolvedZone));
                Assert.AreSame(zone, resolvedZone);
                Assert.IsTrue(registry.TryGetExtractionParticipant(id, out IExtractionParticipant resolvedParticipant));
                Assert.AreSame(participant, resolvedParticipant);

                Assert.IsFalse(registry.TryRegisterExtractionParticipant(id, obsolete));
                Assert.IsFalse(registry.TryUnregisterExtractionParticipant(id, obsolete));
                Assert.IsTrue(registry.TryGetExtractionZone(id, out _));
                Assert.IsTrue(registry.TryUnregisterExtractionParticipant(id, participant));
                Assert.IsTrue(registry.TryGetExtractionZone(id, out _));
                Assert.IsTrue(registry.TryUnregisterExtractionZone(id, zone));
            }
            finally
            {
                Object.DestroyImmediate(obsoleteObject);
                Object.DestroyImmediate(participantObject);
                Object.DestroyImmediate(zoneObject);
                Object.DestroyImmediate(registryObject);
            }
        }

        [Test]
        public void EntityRegistry_CharacterAndDamageableResolveIndependentlyForSameIdentity()
        {
            GameObject registryObject = new GameObject("Registry");
            GameObject characterObject = new GameObject("Character");

            try
            {
                EntityRegistry registry = registryObject.AddComponent<EntityRegistry>();
                EntityId id = new EntityId(28);
                TestCharacter character = characterObject.AddComponent<TestCharacter>();
                character.IdValue = id.Value;

                Assert.IsTrue(registry.TryRegisterEntity(id, character, null));
                Assert.IsTrue(registry.TryGetCharacter(id, out ICharacter resolvedCharacter));
                Assert.AreSame(character, resolvedCharacter);
                Assert.IsTrue(registry.TryGetDamageable(id, out IDamageable resolvedDamageable));
                Assert.AreSame(character, resolvedDamageable);

                Assert.IsTrue(registry.TryUnregisterEntity(id, character));
                Assert.IsFalse(registry.TryGetCharacter(id, out _));
                Assert.IsFalse(registry.TryGetDamageable(id, out _));
            }
            finally
            {
                Object.DestroyImmediate(characterObject);
                Object.DestroyImmediate(registryObject);
            }
        }

        [Test]
        public void PlayerExtractionController_ImplementsIExtractionParticipantContract()
        {
            GameObject obj = new GameObject("Player");
            PlayerExtractionController controller = obj.AddComponent<PlayerExtractionController>();

            Assert.IsTrue(controller is IExtractionParticipant);
            Assert.AreEqual((Vector2)obj.transform.position, controller.ValidationPoint);

            Object.DestroyImmediate(obj);
        }

        [Test]
        public void PlayerExtractionController_WithoutStateAuthority_RejectsTryBeginExtraction()
        {
            GameObject obj = new GameObject("Player");
            PlayerExtractionController controller = obj.AddComponent<PlayerExtractionController>();

            bool started = controller.TryBeginExtraction(new EntityId(42));
            Assert.IsFalse(started);

            Object.DestroyImmediate(obj);
        }

        [Test]
        public void ExtractionZone_TrySetAvailability_WithoutStateAuthority_ReturnsFalse()
        {
            GameObject zoneObj = new GameObject("Zone");
            zoneObj.AddComponent<BoxCollider2D>();
            ExtractionZone zone = zoneObj.AddComponent<ExtractionZone>();

            bool result = zone.TrySetAvailability(false);
            Assert.IsFalse(result);

            Object.DestroyImmediate(zoneObj);
        }

        private sealed class TestZone : MonoBehaviour, IExtractionZone
        {
            public int IdValue { get; set; }
            public EntityId Id => new EntityId(IdValue);
            public bool IsAvailable => true;
            public bool ContainsExact(Vector2 point) => true;
            public bool ContainsWithTolerance(Vector2 point, float tolerance) => true;
            public bool TrySetAvailability(bool available) => false;
        }

        private sealed class TestParticipant : MonoBehaviour, IExtractionParticipant
        {
            public int IdValue { get; set; }
            public EntityId Id => new EntityId(IdValue);
            public ExtractionState State => ExtractionState.None;
            public EntityId ActiveZoneId => default;
            public Vector2 ValidationPoint => transform.position;
            public bool TryBeginExtraction(EntityId zoneId) => false;
            public void NotifyExtractionZoneExit(EntityId zoneId) { }
        }

        private sealed class TestCharacter : MonoBehaviour, ICharacter, IDamageable
        {
            public int IdValue { get; set; }
            public EntityId Id => new EntityId(IdValue);
            public bool IsAlive => true;
            public bool CanReceiveDamage => true;
            public DamageResult ApplyDamage(in DamageRequest request) => default;
        }
    }
}
