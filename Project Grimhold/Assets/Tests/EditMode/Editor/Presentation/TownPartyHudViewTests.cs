#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Presentation
{
    public sealed class TownPartyHudViewTests
    {
        private GameObject _host;
        private GameObject _root;
        private TownPartyHudView _view;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("TownPartyHudViewTests");
            _root = new GameObject("Root");
            _root.transform.SetParent(_host.transform, false);
            _view = _host.AddComponent<TownPartyHudView>();
            typeof(TownPartyHudView)
                .GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_view, _root);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [Test]
        public void OwnedWithoutPartyPresentation_StaysHidden()
        {
            _view.SetOwned(true);

            Assert.That(_root.activeSelf, Is.False);
        }

        [Test]
        public void SoloParty_StaysHidden()
        {
            _view.SetOwned(true);

            _view.Present(new TownPartyPresentation("Dani", string.Empty, false));

            Assert.That(_root.activeSelf, Is.False);
        }

        [Test]
        public void PartyWithCompanion_IsShownForTheOwner()
        {
            _view.SetOwned(true);

            _view.Present(new TownPartyPresentation("Dani", "Ana", true));

            Assert.That(_root.activeSelf, Is.True);
        }

        [Test]
        public void PartyWithCompanion_IsHiddenForANonOwner()
        {
            _view.SetOwned(false);

            _view.Present(new TownPartyPresentation("Dani", "Ana", true));

            Assert.That(_root.activeSelf, Is.False);
        }

        [Test]
        public void ClearParty_HidesAShownHud()
        {
            _view.SetOwned(true);
            _view.Present(new TownPartyPresentation("Dani", "Ana", true));

            _view.ClearParty();

            Assert.That(_root.activeSelf, Is.False);
        }

        [Test]
        public void PartyDisbanding_BackToSolo_HidesTheHud()
        {
            _view.SetOwned(true);
            _view.Present(new TownPartyPresentation("Dani", "Ana", true));

            _view.Present(new TownPartyPresentation("Dani", string.Empty, false));

            Assert.That(_root.activeSelf, Is.False);
        }

        [Test]
        public void LosingOwnership_HidesAShownHud()
        {
            _view.SetOwned(true);
            _view.Present(new TownPartyPresentation("Dani", "Ana", true));

            _view.SetOwned(false);

            Assert.That(_root.activeSelf, Is.False);
        }
    }
}
#endif
