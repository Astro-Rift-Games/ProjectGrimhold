using NUnit.Framework;

namespace Tests.EditMode.Scenario
{
    [TestFixture]
    public sealed class DungeonDoorOpenDirectionRulesTests
    {
        [TestCase(21f, 20f, DungeonDoorOpenDirection.South)]
        [TestCase(20.000002f, 20f, DungeonDoorOpenDirection.South)]
        [TestCase(19f, 20f, DungeonDoorOpenDirection.North)]
        [TestCase(19.999998f, 20f, DungeonDoorOpenDirection.North)]
        [TestCase(20f, 20f, DungeonDoorOpenDirection.North)]
        [TestCase(-9f, -10f, DungeonDoorOpenDirection.South)]
        [TestCase(-11f, -10f, DungeonDoorOpenDirection.North)]
        [TestCase(0f, 0f, DungeonDoorOpenDirection.North)]
        public void ResolveOpenDirection_OpensAwayFromInteractor(
            float interactorY, float pivotY, DungeonDoorOpenDirection expected)
        {
            Assert.That(DungeonDoorStateRules.ResolveOpenDirection(interactorY, pivotY), Is.EqualTo(expected));
        }
    }
}
