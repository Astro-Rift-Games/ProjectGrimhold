#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class TownMenuTabIdsTests
    {
        [Test]
        public void All_ListsTheTabsInDisplayOrder()
        {
            Assert.That(
                TownMenuTabIds.All,
                Is.EqualTo(new[] { "inventory", "attributes", "abilities", "options" }));
        }

        [Test]
        public void Abilities_HasAStableId()
        {
            Assert.That(TownMenuTabIds.Abilities, Is.EqualTo("abilities"));
        }
    }
}
#endif
