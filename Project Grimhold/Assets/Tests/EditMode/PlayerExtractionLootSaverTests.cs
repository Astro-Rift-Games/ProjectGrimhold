#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;

public sealed class PlayerExtractionLootSaverTests
{
    [Test]
    public void SnapshotCapacity_CoversInventoryAndAllSixEquipmentSlots()
    {
        Assert.That(PlayerExtractionLootSaver.MaximumSnapshotEntries, Is.EqualTo(22));
    }

    [Test]
    public void PayloadShape_AllowsEmptySnapshot()
    {
        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new int[0],
                new int[0],
                PlayerLootReceiver.MaxDistinctLootTypes,
                PlayerLootReceiver.MaxCatalogEntries,
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
                PlayerLootReceiver.MaxDistinctLootTypes,
                PlayerLootReceiver.MaxCatalogEntries,
                _ => true),
            Is.False);

        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new[] { 1, 1 },
                new[] { 2, 3 },
                PlayerLootReceiver.MaxDistinctLootTypes,
                PlayerLootReceiver.MaxCatalogEntries,
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
                PlayerLootReceiver.MaxDistinctLootTypes,
                PlayerLootReceiver.MaxCatalogEntries,
                index => index == 2),
            Is.False);

        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new[] { 2 },
                new[] { 0 },
                PlayerLootReceiver.MaxDistinctLootTypes,
                PlayerLootReceiver.MaxCatalogEntries,
                _ => true),
            Is.False);
    }

    [Test]
    public void PayloadShape_AllowsCatalogIndexBeyondDistinctEntryCapacity()
    {
        Assert.That(
            PlayerExtractionLootSaver.ValidatePayloadShape(
                new[] { PlayerLootReceiver.MaxCatalogEntries - 1 },
                new[] { 1 },
                PlayerLootReceiver.MaxDistinctLootTypes,
                PlayerLootReceiver.MaxCatalogEntries,
                _ => true),
            Is.True);
    }

    [Test]
    public void ExperienceCandidate_MustBelongToAckPendingAndParticipantSequence()
    {
        var candidate = new ExtractedLootExperienceCandidate(
            3,
            new ExtractedLootExperienceCalculation(100, 10));

        Assert.That(
            PlayerExtractionLootSaver.CandidateMatchesPendingTransaction(candidate, 3, 3, 3),
            Is.True);
        Assert.That(
            PlayerExtractionLootSaver.CandidateMatchesPendingTransaction(candidate, 2, 3, 3),
            Is.False);
        Assert.That(
            PlayerExtractionLootSaver.CandidateMatchesPendingTransaction(candidate, 3, 2, 3),
            Is.False);
        Assert.That(
            PlayerExtractionLootSaver.CandidateMatchesPendingTransaction(candidate, 3, 3, 4),
            Is.False);
    }
}
#endif
