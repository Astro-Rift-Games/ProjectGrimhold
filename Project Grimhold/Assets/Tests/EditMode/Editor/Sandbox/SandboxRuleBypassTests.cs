using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Sandbox
{
    public sealed class SandboxRuleBypassTests
    {
        [Test]
        public void Requirements_IgnoredWhenBypassIsOn()
        {
            Assert.That(SandboxRuleBypass.EvaluateRequirements(true, false), Is.True);
            Assert.That(SandboxRuleBypass.EvaluateRequirements(true, true), Is.True);
        }

        [Test]
        public void Requirements_UnchangedWhenBypassIsOff()
        {
            Assert.That(SandboxRuleBypass.EvaluateRequirements(false, false), Is.False);
            Assert.That(SandboxRuleBypass.EvaluateRequirements(false, true), Is.True);
        }

        [TestCase(1, new[] { 1 })]
        [TestCase(5, new[] { 5 })]
        [TestCase(7, new[] { 5, 1, 1 })]
        [TestCase(10, new[] { 5, 5 })]
        [TestCase(-3, new[] { -1, -1, -1 })]
        [TestCase(-10, new[] { -5, -5 })]
        public void Decompose_SplitsIntoSupportedSteps(int amount, int[] expected)
        {
            var steps = new List<int>();

            bool ok = SandboxRuleBypass.TryDecomposeAdjustment(amount, steps);

            Assert.That(ok, Is.True);
            Assert.That(steps, Is.EqualTo(expected));
        }

        [TestCase(0)]
        [TestCase(SandboxRuleBypass.MaxAdjustment + 1)]
        [TestCase(-SandboxRuleBypass.MaxAdjustment - 1)]
        public void Decompose_RejectsZeroAndOutOfRange(int amount)
        {
            var steps = new List<int>();

            Assert.That(SandboxRuleBypass.TryDecomposeAdjustment(amount, steps), Is.False);
            Assert.That(steps, Is.Empty);
        }

        [Test]
        public void Decompose_RejectsNullOutput()
        {
            Assert.That(SandboxRuleBypass.TryDecomposeAdjustment(1, null), Is.False);
        }

        [Test]
        public void RequirementText_ShowsMinimumAndCurrentValue()
        {
            Assert.That(SandboxRuleBypass.DescribeRequirement("Strength", 10, 12), Is.EqualTo("needs Strength 10 (current 12)"));
            Assert.That(SandboxRuleBypass.DescribeRequirement("Strength", 15, null), Is.EqualTo("needs Strength 15 (current ?)"));
        }

        [Test]
        public void AbilityLabel_MarksAbilitiesWithoutBehaviour()
        {
            Assert.That(SandboxRuleBypass.DescribeAbility("Charge", true), Is.EqualTo("Charge"));
            Assert.That(SandboxRuleBypass.DescribeAbility("Heal", false), Is.EqualTo("Heal (no behaviour - cannot cast)"));
        }
    }
}
