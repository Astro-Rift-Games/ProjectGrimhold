#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Presentation
{
    public sealed class TownAbilitiesSelectionTests
    {
        private static TownAbilityEntry Entry(string id) =>
            new(
                new AbilityId(id),
                id,
                string.Empty,
                null,
                AbilityResourceType.Stamina,
                1,
                1f,
                null,
                TownAbilityEntryState.Available,
                false,
                UniversalAbilitySlot.Slot1);

        [Test]
        public void Resolve_KeepsTheCurrentSelectionWhileItIsStillVisible()
        {
            var visible = new List<TownAbilityEntry> { Entry("charge"), Entry("trap") };

            AbilityId resolved = TownAbilitiesSelection.Resolve(visible, new AbilityId("trap"));

            Assert.That(resolved, Is.EqualTo(new AbilityId("trap")));
        }

        [Test]
        public void Resolve_FallsBackToTheFirstVisibleEntryWhenTheSelectionDisappeared()
        {
            var visible = new List<TownAbilityEntry> { Entry("charge"), Entry("trap") };

            AbilityId resolved = TownAbilitiesSelection.Resolve(visible, new AbilityId("empower"));

            Assert.That(resolved, Is.EqualTo(new AbilityId("charge")));
        }

        [Test]
        public void Resolve_DefaultsToTheFirstVisibleEntryWhenNothingWasSelected()
        {
            var visible = new List<TownAbilityEntry> { Entry("charge"), Entry("trap") };

            Assert.That(TownAbilitiesSelection.Resolve(visible, default), Is.EqualTo(new AbilityId("charge")));
        }

        [Test]
        public void Resolve_WithNothingVisible_ReturnsAnInvalidId()
        {
            Assert.That(
                TownAbilitiesSelection.Resolve(new List<TownAbilityEntry>(), new AbilityId("charge")).IsValid,
                Is.False);
            Assert.That(TownAbilitiesSelection.Resolve(null, default).IsValid, Is.False);
        }
    }
}
#endif
