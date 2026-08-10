#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class PlayerExtractionLootSaverTests
{
    [Test]
    public void PayloadShape_AllowsEmptySnapshot()
    {
        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new int[0],
                new int[0],
                PlayerLootReceiver.MaxLootTypes,
                _ => true),
            Is.True);
    }

    [Test]
    public void PayloadShape_RejectsMismatchedArraysAndDuplicates()
    {
        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new[] { 1 },
                new int[0],
                PlayerLootReceiver.MaxLootTypes,
                _ => true),
            Is.False);

        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new[] { 1, 1 },
                new[] { 2, 3 },
                PlayerLootReceiver.MaxLootTypes,
                _ => true),
            Is.False);
    }

    [Test]
    public void PayloadShape_RejectsUnknownIndexesAndInvalidAmounts()
    {
        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new[] { 4 },
                new[] { 1 },
                PlayerLootReceiver.MaxLootTypes,
                index => index == 2),
            Is.False);

        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new[] { 2 },
                new[] { 0 },
                PlayerLootReceiver.MaxLootTypes,
                _ => true),
            Is.False);
    }
}
#endif
