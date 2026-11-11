using NUnit.Framework;

namespace Tests.EditMode.Testing
{
    public sealed class PrePushTestSelectionTests
    {
        [TestCase("Tests.EditMode.AttackAnimationValidator.AttackPreviewCatalogTests.Catalog_ResolvesEveryWeapon")]
        [TestCase("SessionConnectionStateMachineTests.HubStartup_PreCancelledToken_DoesNotCreateRunner")]
        [TestCase("DirectionalAnimationGeneratorTests.Generate_WritesEveryFacing(\"N\")")]
        public void SlowEditorToolingSuites_AreExcluded(string fullName)
        {
            Assert.That(PrePushTestSelection.IsExcluded(fullName), Is.True);
        }

        [TestCase("Tests.EditMode.Loot.MvpItemCatalogTests.All39MvpItems_MatchDoc14MasterTableSpecifications")]
        [TestCase("Tests.EditMode.Networking.SessionConnectionCoordinatorTests.Start_Succeeds")]
        [TestCase("RaidSceneMinimapTests.Layout_IsCurrent")]
        [TestCase("Tests.EditMode.AttackAnimationValidatorLikeNameTests.Other")]
        public void OtherTests_AreIncluded(string fullName)
        {
            Assert.That(PrePushTestSelection.IsExcluded(fullName), Is.False);
        }
    }
}
