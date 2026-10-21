#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode
{
    /// <summary>Composes only the runner-local input bridge required by gameplay fixtures.</summary>
    internal static class PlayModeRunnerComposition
    {
        internal static LocalInputContext AddLocalInputContext(NetworkRunner runner)
        {
            Assert.That(runner, Is.Not.Null, "Compose input only after creating the fixture runner.");
            Assert.That(runner.GetComponent<LocalInputContext>(), Is.Null,
                "The fixture must have exactly one owner of its runner-local input context.");

            return runner.gameObject.AddComponent<LocalInputContext>();
        }
    }
}
#endif
