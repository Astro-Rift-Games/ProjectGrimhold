using NUnit.Framework;

namespace Tests.EditMode.Scenario
{
    [TestFixture]
    public sealed class DungeonDoorStateRulesTests
    {
        [Test]
        public void TryBeginToggle_FromClosed_StartsOpening()
        {
            bool accepted = DungeonDoorStateRules.TryBeginToggle(DungeonDoorState.Closed, isDoorwayOccupied: false, out DungeonDoorState next);

            Assert.That(accepted, Is.True);
            Assert.That(next, Is.EqualTo(DungeonDoorState.Opening));
        }

        [Test]
        public void TryBeginToggle_FromClosedWhileOccupied_StillStartsOpening()
        {
            bool accepted = DungeonDoorStateRules.TryBeginToggle(DungeonDoorState.Closed, isDoorwayOccupied: true, out DungeonDoorState next);

            Assert.That(accepted, Is.True);
            Assert.That(next, Is.EqualTo(DungeonDoorState.Opening));
        }

        [Test]
        public void TryBeginToggle_FromOpen_StartsClosing()
        {
            bool accepted = DungeonDoorStateRules.TryBeginToggle(DungeonDoorState.Open, isDoorwayOccupied: false, out DungeonDoorState next);

            Assert.That(accepted, Is.True);
            Assert.That(next, Is.EqualTo(DungeonDoorState.Closing));
        }

        [Test]
        public void TryBeginToggle_FromOpenWhileDoorwayOccupied_IsRejectedAndKeepsState()
        {
            bool accepted = DungeonDoorStateRules.TryBeginToggle(DungeonDoorState.Open, isDoorwayOccupied: true, out DungeonDoorState next);

            Assert.That(accepted, Is.False);
            Assert.That(next, Is.EqualTo(DungeonDoorState.Open));
        }

        [TestCase(DungeonDoorState.Opening)]
        [TestCase(DungeonDoorState.Closing)]
        public void TryBeginToggle_DuringTransition_IsIgnoredAndKeepsState(DungeonDoorState transition)
        {
            bool accepted = DungeonDoorStateRules.TryBeginToggle(transition, isDoorwayOccupied: false, out DungeonDoorState next);

            Assert.That(accepted, Is.False);
            Assert.That(next, Is.EqualTo(transition));
        }

        [Test]
        public void TryBeginToggle_RepeatedPressesDuringOpening_NeverLeaveOpening()
        {
            DungeonDoorStateRules.TryBeginToggle(DungeonDoorState.Closed, false, out DungeonDoorState state);

            for (int press = 0; press < 5; press++)
            {
                Assert.That(DungeonDoorStateRules.TryBeginToggle(state, false, out DungeonDoorState next), Is.False);
                state = next;
            }

            Assert.That(state, Is.EqualTo(DungeonDoorState.Opening));
        }

        [TestCase(DungeonDoorState.Opening, DungeonDoorState.Open)]
        [TestCase(DungeonDoorState.Closing, DungeonDoorState.Closed)]
        [TestCase(DungeonDoorState.Open, DungeonDoorState.Open)]
        [TestCase(DungeonDoorState.Closed, DungeonDoorState.Closed)]
        public void CompleteTransition_SettlesOnlyTransitionStates(DungeonDoorState current, DungeonDoorState expected)
        {
            Assert.That(DungeonDoorStateRules.CompleteTransition(current), Is.EqualTo(expected));
        }

        [TestCase(DungeonDoorState.Closed, true)]
        [TestCase(DungeonDoorState.Opening, true)]
        [TestCase(DungeonDoorState.Open, false)]
        [TestCase(DungeonDoorState.Closing, true)]
        public void BlocksPassage_OnlyOpenDoorIsPassable(DungeonDoorState state, bool expected)
        {
            Assert.That(DungeonDoorStateRules.BlocksPassage(state), Is.EqualTo(expected));
        }

        [TestCase(DungeonDoorState.Closed, true)]
        [TestCase(DungeonDoorState.Open, true)]
        [TestCase(DungeonDoorState.Opening, false)]
        [TestCase(DungeonDoorState.Closing, false)]
        public void IsStable_OnlyClosedAndOpenAcceptInteraction(DungeonDoorState state, bool expected)
        {
            Assert.That(DungeonDoorStateRules.IsStable(state), Is.EqualTo(expected));
        }

        [Test]
        public void ResolvePrompt_ClosedDoor_OffersOpen()
        {
            Assert.That(DungeonDoorStateRules.ResolvePrompt(DungeonDoorState.Closed), Is.EqualTo("Abrir puerta"));
        }

        [Test]
        public void ResolvePrompt_OpenDoor_OffersClose()
        {
            Assert.That(DungeonDoorStateRules.ResolvePrompt(DungeonDoorState.Open), Is.EqualTo("Cerrar puerta"));
        }

        [TestCase(DungeonDoorState.Opening)]
        [TestCase(DungeonDoorState.Closing)]
        public void ResolvePrompt_Transition_HasNoPrompt(DungeonDoorState state)
        {
            Assert.That(DungeonDoorStateRules.ResolvePrompt(state), Is.Null);
        }
    }
}
