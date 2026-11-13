using NUnit.Framework;

namespace Tests.EditMode.Player
{
    public sealed class PlayerCorpseCompensationTests
    {
        [Test]
        public void PostClearFailure_PreservesAndEnablesCorpseContainer_NeverLeavesBothEmpty()
        {
            // When player inventory was already cleared before failure occurred,
            // compensation must preserve and enable the corpse container to prevent total loot destruction.
            PlayerCorpseCompensationAction action = PlayerCorpseCompensationRule.DetermineCompensationAction(playerCleared: true);
            Assert.That(action, Is.EqualTo(PlayerCorpseCompensationAction.PreserveAndEnableCorpse));
        }

        [Test]
        public void PreClearFailure_RollsBackContainer_PreservingPlayerInventory()
        {
            // When player inventory was NOT cleared before failure occurred,
            // container contents are rolled back and disabled because player still retains items.
            PlayerCorpseCompensationAction action = PlayerCorpseCompensationRule.DetermineCompensationAction(playerCleared: false);
            Assert.That(action, Is.EqualTo(PlayerCorpseCompensationAction.RollbackContainer));
        }

        [Test]
        public void ForcedDefeat_WhenCorpseConversionIncomplete_RequiresManualItemLiquidationAndLossSummary()
        {
            // When a forced defeat occurs and corpse generation did not complete,
            // unowned items must be liquidated from avatar and loss summary published to participant.
            bool requiresLiquidation = PlayerCorpseCompensationRule.RequiresForcedDefeatItemLiquidation(corpseGenerationCompleted: false);
            Assert.That(requiresLiquidation, Is.True);
        }

        [Test]
        public void ForcedDefeat_WhenCorpseConversionCompleted_DoesNotRequireManualLiquidation()
        {
            // When corpse generation already completed, items are safely in the corpse and summary is already published.
            bool requiresLiquidation = PlayerCorpseCompensationRule.RequiresForcedDefeatItemLiquidation(corpseGenerationCompleted: true);
            Assert.That(requiresLiquidation, Is.False);
        }
    }
}
