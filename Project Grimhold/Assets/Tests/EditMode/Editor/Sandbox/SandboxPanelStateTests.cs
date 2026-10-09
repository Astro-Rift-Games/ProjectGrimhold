using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.Sandbox
{
    public sealed class SandboxPanelStateTests
    {
        [Test]
        public void NewState_UsesSafeDefaults()
        {
            var state = new SandboxPanelState();

            Assert.That(state.Tab, Is.EqualTo(SandboxPanelTab.Player));
            Assert.That(state.Count, Is.EqualTo(1));
            Assert.That(state.Spacing, Is.GreaterThanOrEqualTo(SandboxSpawnPlanner.MinimumSpacing));
            Assert.That(state.Slot1Index, Is.EqualTo(SandboxPanelState.NoAbility));
            Assert.That(state.Slot2Index, Is.EqualTo(SandboxPanelState.NoAbility));
        }

        [TestCase(1, 5, 6)]
        [TestCase(1, -5, 1)]
        [TestCase(48, 10, 50)]
        public void AdjustCount_ClampsBetweenOneAndMax(int start, int delta, int expected)
        {
            var state = new SandboxPanelState();
            state.AdjustCount(start - state.Count, 50);

            state.AdjustCount(delta, 50);

            Assert.That(state.Count, Is.EqualTo(expected));
        }

        [Test]
        public void AdjustSpacing_NeverDropsBelowMinimum()
        {
            var state = new SandboxPanelState();

            state.AdjustSpacing(-100f);

            Assert.That(state.Spacing, Is.EqualTo(SandboxSpawnPlanner.MinimumSpacing));
        }

        [Test]
        public void AdjustSpacing_IgnoresNonFiniteDelta()
        {
            var state = new SandboxPanelState();
            float before = state.Spacing;

            state.AdjustSpacing(float.NaN);

            Assert.That(state.Spacing, Is.EqualTo(before));
        }

        [TestCase(0, 1, 3, 1)]
        [TestCase(2, 1, 3, 0)]
        [TestCase(0, -1, 3, 2)]
        [TestCase(0, 1, 0, 0)]
        public void WrapIndex_WrapsAndToleratesEmptyLists(int current, int delta, int count, int expected)
        {
            Assert.That(SandboxPanelState.WrapIndex(current, delta, count), Is.EqualTo(expected));
        }

        [Test]
        public void CycleKind_VisitsEveryKindAndWraps()
        {
            var state = new SandboxPanelState();
            SandboxEnemyKind first = state.Kind;

            state.CycleKind(1);
            state.CycleKind(1);
            state.CycleKind(1);

            Assert.That(state.Kind, Is.EqualTo(first));
        }

        [Test]
        public void TrySelectAbility_RejectsEntriesWithoutBehaviour()
        {
            var state = new SandboxPanelState();

            bool selected = state.TrySelectAbility(1, 3, hasBehaviour: false, out string error);

            Assert.That(selected, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(state.Slot1Index, Is.EqualTo(SandboxPanelState.NoAbility));
        }

        [Test]
        public void TrySelectAbility_RejectsSameAbilityInBothSlots()
        {
            var state = new SandboxPanelState();
            state.TrySelectAbility(1, 2, true, out _);

            bool selected = state.TrySelectAbility(2, 2, true, out string error);

            Assert.That(selected, Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(state.Slot2Index, Is.EqualTo(SandboxPanelState.NoAbility));
        }

        [Test]
        public void TrySelectAbility_AcceptsDistinctEquippableAbilities()
        {
            var state = new SandboxPanelState();

            Assert.That(state.TrySelectAbility(1, 0, true, out _), Is.True);
            Assert.That(state.TrySelectAbility(2, 4, true, out _), Is.True);
            Assert.That(state.Slot1Index, Is.EqualTo(0));
            Assert.That(state.Slot2Index, Is.EqualTo(4));
        }

        [TestCase(0)]
        [TestCase(3)]
        public void TrySelectAbility_RejectsUnknownSlot(int slot)
        {
            var state = new SandboxPanelState();

            Assert.That(state.TrySelectAbility(slot, 1, true, out _), Is.False);
        }

        [Test]
        public void TrySelectAbility_RejectsNegativeCatalogIndex()
        {
            var state = new SandboxPanelState();

            Assert.That(state.TrySelectAbility(1, -1, true, out _), Is.False);
        }

        [Test]
        public void ClearSlot_ResetsOnlyThatSlot()
        {
            var state = new SandboxPanelState();
            state.TrySelectAbility(1, 0, true, out _);
            state.TrySelectAbility(2, 1, true, out _);

            state.ClearSlot(1);

            Assert.That(state.Slot1Index, Is.EqualTo(SandboxPanelState.NoAbility));
            Assert.That(state.Slot2Index, Is.EqualTo(1));
        }

        [Test]
        public void AdjustAttributeStep_ClampsToSupportedRange()
        {
            var state = new SandboxPanelState();

            state.AdjustAttributeStep(-50);
            Assert.That(state.AttributeStep, Is.EqualTo(1));

            state.AdjustAttributeStep(500);
            Assert.That(state.AttributeStep, Is.EqualTo(SandboxPanelState.MaxAttributeStep));
        }

        [TestCase("3.5", "-2", true, 3.5f, -2f)]
        [TestCase(" 0 ", "10", true, 0f, 10f)]
        [TestCase("abc", "1", false, 0f, 0f)]
        [TestCase("1", "", false, 0f, 0f)]
        [TestCase("NaN", "1", false, 0f, 0f)]
        [TestCase("Infinity", "1", false, 0f, 0f)]
        public void TryParseVector_ParsesInvariantFiniteNumbers(
            string x, string y, bool expectedOk, float expectedX, float expectedY)
        {
            bool ok = SandboxPanelState.TryParseVector(x, y, out Vector2 result);

            Assert.That(ok, Is.EqualTo(expectedOk));
            Assert.That(result, Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [TestCase("50", true, 50f)]
        [TestCase("0.5", true, 0.5f)]
        [TestCase("-", false, 0f)]
        [TestCase("", false, 0f)]
        public void TryParseNumber_RejectsGarbage(string text, bool expectedOk, float expected)
        {
            bool ok = SandboxPanelState.TryParseNumber(text, out float value);

            Assert.That(ok, Is.EqualTo(expectedOk));
            Assert.That(value, Is.EqualTo(expected));
        }

        [Test]
        public void SetTab_IgnoresUndefinedValues()
        {
            var state = new SandboxPanelState();
            state.SetTab(SandboxPanelTab.Dummy);

            state.SetTab((SandboxPanelTab)99);

            Assert.That(state.Tab, Is.EqualTo(SandboxPanelTab.Dummy));
        }

        [Test]
        public void SyncSlots_ReplacesSelectionWithAppliedIndices()
        {
            var state = new SandboxPanelState();
            state.TrySelectAbility(1, 4, true, out _);

            state.SyncSlots(0, 1);

            Assert.That(state.Slot1Index, Is.EqualTo(0));
            Assert.That(state.Slot2Index, Is.EqualTo(1));
        }

        [Test]
        public void SyncSlots_NegativeIndicesBecomeEmpty()
        {
            var state = new SandboxPanelState();

            state.SyncSlots(-5, 3);

            Assert.That(state.Slot1Index, Is.EqualTo(SandboxPanelState.NoAbility));
            Assert.That(state.Slot2Index, Is.EqualTo(3));
        }
    }
}
