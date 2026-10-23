#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.PlayMode
{
    public sealed class PlayModeRunnerCompositionTests
    {
        [Test]
        public void MissingRunner_RejectsComposition()
        {
            Assert.Throws<AssertionException>(() => PlayModeRunnerComposition.AddLocalInputContext(null));
        }

        [Test]
        public void DuplicateComposition_RejectsWithoutReplacingTheContext()
        {
            var owner = new GameObject("DuplicateInputContextFixture");
            try
            {
                NetworkRunner runner = owner.AddComponent<NetworkRunner>();
                LocalInputContext context = PlayModeRunnerComposition.AddLocalInputContext(runner);

                Assert.Throws<AssertionException>(() => PlayModeRunnerComposition.AddLocalInputContext(runner));
                Assert.That(owner.GetComponents<LocalInputContext>(), Has.Length.EqualTo(1));
                Assert.That(owner.GetComponent<LocalInputContext>(), Is.SameAs(context));
                Assert.That(context.Reader, Is.Null, "Composition must not create or register a local reader.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SeparateRunners_OwnDistinctContextsDestroyedWithTheirOwners()
        {
            var firstOwner = new GameObject("FirstInputContextFixture");
            var secondOwner = new GameObject("SecondInputContextFixture");
            try
            {
                NetworkRunner firstRunner = firstOwner.AddComponent<NetworkRunner>();
                NetworkRunner secondRunner = secondOwner.AddComponent<NetworkRunner>();
                LocalInputContext first = PlayModeRunnerComposition.AddLocalInputContext(firstRunner);
                LocalInputContext second = PlayModeRunnerComposition.AddLocalInputContext(secondRunner);

                Assert.That(first, Is.Not.SameAs(second));
                Assert.That(first.gameObject, Is.SameAs(firstRunner.gameObject));
                Assert.That(second.gameObject, Is.SameAs(secondRunner.gameObject));
                Assert.That(first.Reader, Is.Null);
                Assert.That(second.Reader, Is.Null);

                Object.DestroyImmediate(firstOwner);
                Assert.That(first == null, Is.True);
                Assert.That(second != null, Is.True, "Destroying one fixture must not affect another runner.");
                Object.DestroyImmediate(secondOwner);
                Assert.That(second == null, Is.True);
            }
            finally
            {
                if (firstOwner != null) Object.DestroyImmediate(firstOwner);
                if (secondOwner != null) Object.DestroyImmediate(secondOwner);
            }
        }

        [Test]
        public void TeamProgressWithoutRegistry_RejectsComposition()
        {
            var owner = new GameObject("TeamProgressWithoutRegistryFixture");
            try
            {
                NetworkRunner runner = owner.AddComponent<NetworkRunner>();

                Assert.Throws<AssertionException>(() => PlayModeRunnerComposition.AddTeamExtractionProgress(runner));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void TeamProgress_IsTheRunnerSingletonAndAccumulatesPerTeam()
        {
            var owner = new GameObject("TeamProgressFixture");
            try
            {
                NetworkRunner runner = owner.AddComponent<NetworkRunner>();
                EntityRegistry registry = owner.AddComponent<EntityRegistry>();
                PlayModeTeamExtractionProgress progress = PlayModeRunnerComposition.AddTeamExtractionProgress(runner);

                Assert.That(registry.TryGetTeamExtractionProgress(out ITeamExtractionProgress resolved), Is.True);
                Assert.That(resolved, Is.SameAs(progress));
                Assert.Throws<AssertionException>(() => PlayModeRunnerComposition.AddTeamExtractionProgress(runner));

                Assert.That(RaidTeamId.TryCreate(1, out RaidTeamId firstTeam), Is.True);
                Assert.That(RaidTeamId.TryCreate(2, out RaidTeamId secondTeam), Is.True);
                Assert.That(progress.TryApplyContribution(firstTeam, 3, out int firstProgress, out bool completed), Is.True);
                Assert.That(firstProgress, Is.EqualTo(3));
                Assert.That(completed, Is.False);
                Assert.That(progress.TryApplyContribution(firstTeam, 0, out _, out _), Is.False);
                Assert.That(progress.TryApplyContribution(default, 1, out _, out _), Is.False);

                Assert.That(progress.TryGetSnapshot(firstTeam, out ExtractionProgressSnapshot snapshot), Is.True);
                Assert.That(snapshot.CurrentProgress, Is.EqualTo(3));
                Assert.That(progress.TryGetProgress(secondTeam, out int secondProgress, out _), Is.True);
                Assert.That(secondProgress, Is.Zero);
                Assert.That(progress.AppliedContributionCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
#endif
