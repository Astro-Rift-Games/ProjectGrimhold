#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class MainMenuSessionActionsTests
    {
        [Test]
        public void Unauthenticated_ShowsLoginAndDisabledPlay()
        {
            var actions = new MainMenuSessionActions(false, false);

            Assert.That(actions.PlayVisible, Is.True);
            Assert.That(actions.PlayInteractable, Is.False);
            Assert.That(actions.LoginVisible, Is.True);
            Assert.That(actions.LogoutVisible, Is.False);
            Assert.That(actions.SecondaryInteractable, Is.True);
        }

        [Test]
        public void Authenticated_ShowsLogoutAndEnablesPlay()
        {
            var actions = new MainMenuSessionActions(true, false);

            Assert.That(actions.PlayVisible, Is.True);
            Assert.That(actions.PlayInteractable, Is.True);
            Assert.That(actions.LoginVisible, Is.False);
            Assert.That(actions.LogoutVisible, Is.True);
            Assert.That(actions.SecondaryInteractable, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Busy_DisablesAllActionsAndPreservesSessionVisibility(bool isAuthenticated)
        {
            var actions = new MainMenuSessionActions(isAuthenticated, true);

            Assert.That(actions.PlayVisible, Is.True);
            Assert.That(actions.PlayInteractable, Is.False);
            Assert.That(actions.SecondaryInteractable, Is.False);
            Assert.That(actions.LoginVisible, Is.EqualTo(!isAuthenticated));
            Assert.That(actions.LogoutVisible, Is.EqualTo(isAuthenticated));
        }
    }
}
#endif
