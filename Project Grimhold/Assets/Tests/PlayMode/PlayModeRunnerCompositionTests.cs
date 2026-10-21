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
    }
}
#endif
