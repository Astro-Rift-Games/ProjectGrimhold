#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using Fusion;
using NUnit.Framework;
using Assert = NUnit.Framework.Assert;

namespace Tests.PlayMode
{
    /// <summary>Checks the avatar's current extraction capability contract after spawning.</summary>
    internal static class ExtractionProgressTestAssertions
    {
        internal static void AssertRegistered(NetworkRunner runner, NetworkObject avatar)
        {
            Assert.That(runner, Is.Not.Null);
            Assert.That(avatar, Is.Not.Null);
            Assert.That(avatar.IsValid, Is.True, "Extraction capabilities require a spawned avatar identity.");
            Assert.That(avatar.Runner, Is.SameAs(runner));

            EntityRegistry registry = runner.GetComponent<EntityRegistry>();
            PlayerExtractionProgressController controller = avatar.GetComponent<PlayerExtractionProgressController>();
            Assert.That(registry, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);

            var avatarId = new EntityId(unchecked((int)avatar.Id.Raw));
            Assert.That(controller.Id, Is.EqualTo(avatarId));
            Assert.That(registry.TryGetExtractionProgressReceiver(avatarId, out IExtractionProgressReceiver receiver),
                Is.True, "The runner must resolve the spawned avatar's contribution receiver.");
            Assert.That(registry.TryGetExtractionProgressReader(avatarId, out IExtractionProgressReader reader),
                Is.True, "The runner must resolve the same avatar's read-only progress capability.");
            Assert.That(receiver, Is.SameAs(controller));
            Assert.That(reader, Is.SameAs(controller));
        }
    }
}
#endif
