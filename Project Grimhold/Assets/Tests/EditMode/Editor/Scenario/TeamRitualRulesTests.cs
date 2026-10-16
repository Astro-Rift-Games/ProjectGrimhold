using NUnit.Framework;

namespace Tests.EditMode.Scenario
{
    [TestFixture]
    public sealed class TeamRitualRulesTests
    {
        [Test]
        public void CanInitiateRitual_ActiveConnectedLivingMemberOfAssignedTeam_CanInitiate()
        {
            bool canInitiate = TeamRitualRules.CanInitiateRitual(
                isSanctuaryAssignedToTeam: true,
                isMemberAlive: true,
                isMemberDowned: false,
                isMemberConnected: true);

            Assert.That(canInitiate, Is.True);
        }

        [Test]
        public void CanInitiateRitual_UnassignedTeam_CannotInitiate()
        {
            bool canInitiate = TeamRitualRules.CanInitiateRitual(
                isSanctuaryAssignedToTeam: false,
                isMemberAlive: true,
                isMemberDowned: false,
                isMemberConnected: true);

            Assert.That(canInitiate, Is.False);
        }

        [Test]
        public void CanInitiateRitual_DownedMember_CannotInitiate()
        {
            // Per doc 06 §11, initiation requires an Active member; Downed cannot initiate
            bool canInitiate = TeamRitualRules.CanInitiateRitual(
                isSanctuaryAssignedToTeam: true,
                isMemberAlive: true,
                isMemberDowned: true,
                isMemberConnected: true);

            Assert.That(canInitiate, Is.False);
        }

        [Test]
        public void CanInitiateRitual_DisconnectedMember_CannotInitiate()
        {
            bool canInitiate = TeamRitualRules.CanInitiateRitual(
                isSanctuaryAssignedToTeam: true,
                isMemberAlive: true,
                isMemberDowned: false,
                isMemberConnected: false);

            Assert.That(canInitiate, Is.False);
        }

        [Test]
        public void CanInitiateRitual_DeadMember_CannotInitiate()
        {
            bool canInitiate = TeamRitualRules.CanInitiateRitual(
                isSanctuaryAssignedToTeam: true,
                isMemberAlive: false,
                isMemberDowned: false,
                isMemberConnected: true);

            Assert.That(canInitiate, Is.False);
        }

        [TestCase(2, false)]
        [TestCase(1, false)]
        [TestCase(0, true)]
        [TestCase(-1, true)]
        public void ShouldCancelRitual_CancelsOnlyWhenNoRequiredMembersRemain_EnforcesRuleA4(
            int remainingRequiredMembers,
            bool expectedCancel)
        {
            // Per D-5 and A-4: movement, damage, downed state or disconnect of initiator do NOT cancel.
            // Only when remaining required members reaches 0 does the ritual cancel.
            bool shouldCancel = TeamRitualRules.ShouldCancelRitual(remainingRequiredMembers);
            Assert.That(shouldCancel, Is.EqualTo(expectedCancel));
        }
    }
}
