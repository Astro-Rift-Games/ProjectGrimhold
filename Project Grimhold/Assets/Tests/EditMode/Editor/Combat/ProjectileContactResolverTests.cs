#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Combat
{
    public sealed class ProjectileContactResolverTests
    {
        private const int EnemyA = 10;
        private const int EnemyB = 11;
        private const int OrbOwner = 20;

        // Doc 18 §8 T15: the ally is excluded before resolution (EnemyOnly classification), so the
        // Arcane contract sees only the enemy and stops on it without piercing further.
        [Test]
        public void T15_ArcaneSkipsExcludedAllyAndStopsOnFirstValidEnemy()
        {
            // The ally at distance 0.5 is classified as no contact and never reaches the buffer.
            ProjectileContact[] contacts =
            {
                Target(EnemyB, 3f),
                Target(EnemyA, 1f),
                Blocker(5f)
            };
            var sink = new RecordingSink();

            ProjectileContactResolution result = ResolveAll(contacts, ProjectileImpactSettings.SingleImpact,
                new ProjectileImpactLedger(), 1, ref sink);

            Assert.That(sink.Impacts, Is.EqualTo(new[] { EnemyA }));
            Assert.That(result.IsTerminated, Is.True);
            Assert.That(contacts[result.TerminalIndex].EntityIdValue, Is.EqualTo(EnemyA));
            Assert.That(result.ImpactsApplied, Is.EqualTo(1));
        }

        // Doc 18 §8 T16: one application per target and pulse, even with several colliders in contact.
        [Test]
        public void T16_PulseHittingSameTargetTwiceInOneEventAppliesOnce()
        {
            ProjectileImpactSettings settings = Create(ProjectileReimpactPolicy.PeriodicPulse, 10, 0.5f);
            ProjectileContact[] contacts =
            {
                Target(EnemyA, 0f, 0),
                Target(EnemyA, 0.2f, 1),
                Target(EnemyB, 0.1f, 2)
            };
            var sink = new RecordingSink();

            ResolveAll(contacts, settings, new ProjectileImpactLedger(), 1, ref sink, pulseDue: true);

            Assert.That(sink.Impacts, Is.EqualTo(new[] { EnemyA, EnemyB }));
            Assert.That(sink.ImpactSources, Is.EqualTo(new[] { 0, 2 }), "The nearest contact of a target is kept.");
        }

        [Test]
        public void T16_PulsePolicyImpactsOnlyOnPulseEventsAndAgainOnEachPulse()
        {
            ProjectileImpactSettings settings = Create(ProjectileReimpactPolicy.PeriodicPulse, 10, 0.5f);
            var ledger = new ProjectileImpactLedger();
            var sink = new RecordingSink();

            int applied = ResolveAll(new[] { Target(EnemyA, 0f) }, settings, ledger, 1, ref sink, true).ImpactsApplied;
            applied = ResolveAll(new[] { Target(EnemyA, 0f) }, settings, ledger, 2, ref sink, false, applied).ImpactsApplied;
            applied = ResolveAll(new[] { Target(EnemyA, 0f) }, settings, ledger, 3, ref sink, true, applied).ImpactsApplied;

            Assert.That(sink.Impacts, Is.EqualTo(new[] { EnemyA, EnemyA }));
            Assert.That(applied, Is.EqualTo(2));
        }

        // Doc 18 §8 T17: the consumed orb terminates the projectile and later contacts are discarded.
        [Test]
        public void T17_InterceptingOrbIsConsumedAndTerminatesProjectile()
        {
            ProjectileContact[] contacts = { Target(EnemyA, 2f), Interceptor(OrbOwner, 3, 1f), Blocker(4f) };
            var sink = new RecordingSink();

            ProjectileContactResolution result = ResolveAll(contacts, Create(ProjectileReimpactPolicy.OncePerTarget, 3),
                new ProjectileImpactLedger(), 1, ref sink);

            Assert.That(sink.Intercepted, Is.EqualTo(new[] { 3 }));
            Assert.That(sink.Impacts, Is.Empty, "Contacts after the interception are discarded.");
            Assert.That(result.IsTerminated, Is.True);
            Assert.That(contacts[result.TerminalIndex].Kind, Is.EqualTo(ProjectileContactKind.Interceptor));
            Assert.That(result.ImpactsApplied, Is.Zero);
        }

        [Test]
        public void T17_AlreadyConsumedOrbIsPassedThroughAndNextContactResolves()
        {
            ProjectileContact[] contacts = { Interceptor(OrbOwner, 0, 1f), Blocker(2f), Target(EnemyA, 3f) };
            var sink = new RecordingSink { InterceptResult = false };

            ProjectileContactResolution result = ResolveAll(contacts, ProjectileImpactSettings.SingleImpact,
                new ProjectileImpactLedger(), 1, ref sink);

            Assert.That(result.IsTerminated, Is.True);
            Assert.That(contacts[result.TerminalIndex].Kind, Is.EqualTo(ProjectileContactKind.Blocker),
                "An inactive orb must not hide the wall behind it.");
            Assert.That(sink.Impacts, Is.Empty);
        }

        [Test]
        public void ImpactCountTermination_PiercesUntilMaximumThenDiscardsLaterContacts()
        {
            ProjectileContact[] contacts = { Target(EnemyA, 1f), Target(EnemyB, 2f), Target(30, 3f) };
            var sink = new RecordingSink();

            ProjectileContactResolution result = ResolveAll(contacts, Create(ProjectileReimpactPolicy.OncePerTarget, 2),
                new ProjectileImpactLedger(), 1, ref sink);

            Assert.That(sink.Impacts, Is.EqualTo(new[] { EnemyA, EnemyB }));
            Assert.That(result.IsTerminated, Is.True);
            Assert.That(contacts[result.TerminalIndex].EntityIdValue, Is.EqualTo(EnemyB));
        }

        [Test]
        public void SolidBlockerTerminatesAfterEarlierTargetsOnly()
        {
            ProjectileContact[] contacts = { Target(EnemyB, 3f), Blocker(2f), Target(EnemyA, 1f) };
            var sink = new RecordingSink();

            ProjectileContactResolution result = ResolveAll(contacts, Create(ProjectileReimpactPolicy.OncePerTarget, 5),
                new ProjectileImpactLedger(), 1, ref sink);

            Assert.That(sink.Impacts, Is.EqualTo(new[] { EnemyA }));
            Assert.That(contacts[result.TerminalIndex].Kind, Is.EqualTo(ProjectileContactKind.Blocker));
        }

        [Test]
        public void OncePerTarget_NeverReimpactsTheSameTargetDuringLife()
        {
            ProjectileImpactSettings settings = Create(ProjectileReimpactPolicy.OncePerTarget, 5);
            var ledger = new ProjectileImpactLedger();
            var sink = new RecordingSink();

            int applied = ResolveAll(new[] { Target(EnemyA, 0f) }, settings, ledger, 1, ref sink).ImpactsApplied;
            applied = ResolveAll(new ProjectileContact[0], settings, ledger, 2, ref sink, false, applied).ImpactsApplied;
            applied = ResolveAll(new[] { Target(EnemyA, 0f) }, settings, ledger, 3, ref sink, false, applied).ImpactsApplied;

            Assert.That(sink.Impacts, Is.EqualTo(new[] { EnemyA }));
            Assert.That(applied, Is.EqualTo(1));
        }

        [Test]
        public void OncePerContact_ContinuousContactImpactsOnceAndReentryImpactsAgain()
        {
            ProjectileImpactSettings settings = Create(ProjectileReimpactPolicy.OncePerContact, 5);
            var ledger = new ProjectileImpactLedger();
            var sink = new RecordingSink();

            int applied = 0;
            applied = ResolveAll(new[] { Target(EnemyA, 0f) }, settings, ledger, 1, ref sink, false, applied).ImpactsApplied;
            applied = ResolveAll(new[] { Target(EnemyA, 0f) }, settings, ledger, 2, ref sink, false, applied).ImpactsApplied;
            applied = ResolveAll(new ProjectileContact[0], settings, ledger, 3, ref sink, false, applied).ImpactsApplied;
            applied = ResolveAll(new[] { Target(EnemyA, 0f) }, settings, ledger, 4, ref sink, false, applied).ImpactsApplied;

            Assert.That(sink.Impacts, Is.EqualTo(new[] { EnemyA, EnemyA }));
            Assert.That(applied, Is.EqualTo(2));
        }

        [Test]
        public void OncePerContact_FullLedgerOfContinuousContactsFailsClosed()
        {
            ProjectileImpactSettings settings = Create(ProjectileReimpactPolicy.OncePerContact, 50);
            var ledger = new ProjectileImpactLedger();
            var sink = new RecordingSink();
            var contacts = new ProjectileContact[ProjectileImpactLedger.Capacity + 1];
            for (int i = 0; i < contacts.Length; i++) contacts[i] = Target(100 + i, i);

            ResolveAll(contacts, settings, ledger, 1, ref sink);

            Assert.That(sink.Impacts.Count, Is.EqualTo(ProjectileImpactLedger.Capacity));
            Assert.That(sink.Impacts, Has.No.Member(100 + ProjectileImpactLedger.Capacity));
        }

        [Test]
        public void EqualDistanceTieBreak_IsDeterministicRegardlessOfBufferOrder()
        {
            ProjectileContact[] first = { Blocker(1f, 0), Target(EnemyB, 1f, 1), Target(EnemyA, 1f, 2), Interceptor(OrbOwner, 1, 1f, 3) };
            ProjectileContact[] second = { Interceptor(OrbOwner, 1, 1f, 3), Target(EnemyA, 1f, 2), Target(EnemyB, 1f, 1), Blocker(1f, 0) };

            int firstCount = ProjectileContactResolver.SortAndDeduplicate(first, first.Length);
            int secondCount = ProjectileContactResolver.SortAndDeduplicate(second, second.Length);

            Assert.That(firstCount, Is.EqualTo(secondCount));
            for (int i = 0; i < firstCount; i++)
                Assert.That(first[i].SourceIndex, Is.EqualTo(second[i].SourceIndex));
            Assert.That(first[0].Kind, Is.EqualTo(ProjectileContactKind.Interceptor));
            Assert.That(first[1].EntityIdValue, Is.EqualTo(EnemyA));
            Assert.That(first[2].EntityIdValue, Is.EqualTo(EnemyB));
            Assert.That(first[3].Kind, Is.EqualTo(ProjectileContactKind.Blocker));
        }

        [Test]
        public void ZeroDamageMitigationDoesNotTerminateAPiercingProjectile()
        {
            // The resolver terminates on impact count, never on the damage outcome of an impact.
            ProjectileContact[] contacts = { Target(EnemyA, 1f), Target(EnemyB, 2f) };
            var sink = new RecordingSink();

            ProjectileContactResolution result = ResolveAll(contacts, Create(ProjectileReimpactPolicy.OncePerTarget, 3),
                new ProjectileImpactLedger(), 1, ref sink);

            Assert.That(sink.Impacts, Is.EqualTo(new[] { EnemyA, EnemyB }));
            Assert.That(result.IsTerminated, Is.False);
        }

        [TestCase(ProjectileReimpactPolicy.OncePerTarget, 0, 0f)]
        [TestCase(ProjectileReimpactPolicy.OncePerTarget, ProjectileImpactLedger.Capacity + 1, 0f)]
        [TestCase(ProjectileReimpactPolicy.OncePerContact, ProjectileImpactSettings.MaxImpactCountLimit + 1, 0f)]
        [TestCase(ProjectileReimpactPolicy.PeriodicPulse, 1, 0f)]
        [TestCase(ProjectileReimpactPolicy.PeriodicPulse, 1, float.NaN)]
        [TestCase((ProjectileReimpactPolicy)99, 1, 0f)]
        public void Settings_RejectContractsWithoutAValidEndOrCadence(ProjectileReimpactPolicy policy, int max, float pulse)
        {
            Assert.That(ProjectileImpactSettings.TryCreate(policy, max, pulse, 1f / 60f, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void Settings_ConvertPulseIntervalToAtLeastOneTick()
        {
            Assert.That(ProjectileImpactSettings.TryCreate(ProjectileReimpactPolicy.PeriodicPulse, 3, 0.5f, 1f / 60f,
                out ProjectileImpactSettings settings, out _), Is.True);
            Assert.That(settings.PulseIntervalTicks, Is.EqualTo(30));
            Assert.That(settings.TracksContinuousContact, Is.True);

            Assert.That(ProjectileImpactSettings.TryCreate(ProjectileReimpactPolicy.PeriodicPulse, 3, 0.001f, 1f / 60f,
                out settings, out _), Is.True);
            Assert.That(settings.PulseIntervalTicks, Is.EqualTo(1));
        }

        [Test]
        public void SingleImpactSettings_PreserveLegacyFirstContactTermination()
        {
            ProjectileImpactSettings settings = ProjectileImpactSettings.SingleImpact;

            Assert.That(settings.Policy, Is.EqualTo(ProjectileReimpactPolicy.OncePerTarget));
            Assert.That(settings.MaxImpactCount, Is.EqualTo(1));
            Assert.That(settings.TracksContinuousContact, Is.False);
        }

        private static ProjectileContactResolution ResolveAll(ProjectileContact[] contacts,
            ProjectileImpactSettings settings, ProjectileImpactLedger ledger, int tick, ref RecordingSink sink,
            bool pulseDue = false, int impactsApplied = 0)
        {
            int count = ProjectileContactResolver.SortAndDeduplicate(contacts, contacts.Length);
            return ProjectileContactResolver.Resolve(contacts, count, settings, ledger, tick, pulseDue,
                impactsApplied, ref sink);
        }

        private static ProjectileImpactSettings Create(ProjectileReimpactPolicy policy, int max, float pulse = 0f)
        {
            Assert.That(ProjectileImpactSettings.TryCreate(policy, max, pulse, 1f / 60f,
                out ProjectileImpactSettings settings, out string error), Is.True, error);
            return settings;
        }

        private static ProjectileContact Target(int id, float distance, int source = 0) =>
            new ProjectileContact(ProjectileContactKind.Target, id, 0, distance, Vector2.right * distance, source);

        private static ProjectileContact Blocker(float distance, int source = 0) =>
            new ProjectileContact(ProjectileContactKind.Blocker, 0, 0, distance, Vector2.right * distance, source);

        private static ProjectileContact Interceptor(int owner, int slot, float distance, int source = 0) =>
            new ProjectileContact(ProjectileContactKind.Interceptor, owner, slot, distance, Vector2.right * distance, source);

        private sealed class RecordingSink : IProjectileContactSink
        {
            public bool InterceptResult = true;
            public List<int> Impacts { get; } = new List<int>();
            public List<int> ImpactSources { get; } = new List<int>();
            public List<int> Intercepted { get; } = new List<int>();

            public bool TryIntercept(in ProjectileContact contact)
            {
                bool result = InterceptResult;
                if (result) Intercepted.Add(contact.Slot);
                return result;
            }

            public void ApplyImpact(in ProjectileContact contact)
            {
                Impacts.Add(contact.EntityIdValue);
                ImpactSources.Add(contact.SourceIndex);
            }
        }
    }
}
#endif
