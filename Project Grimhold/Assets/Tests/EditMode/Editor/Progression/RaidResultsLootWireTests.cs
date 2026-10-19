using System.Collections.Generic;
using NUnit.Framework;

namespace Tests.EditMode.Progression
{
    public sealed class RaidResultsLootWireTests
    {
        private static readonly LootId Coin = new("coin");
        private static readonly LootId Sword = new("sword");
        private static readonly string[] Catalog = { "coin", "sword" };

        [Test]
        public void EncodeThenDecode_RoundTripsTheSummaryInCatalogOrder()
        {
            RaidResultsLootSummary summary = CreateSummary(
                RaidResultsLootKind.Lost, 123, new RaidResultsLootEntry(Sword, 1), new RaidResultsLootEntry(Coin, 7));

            Assert.That(
                RaidResultsLootWire.TryEncode(summary, TryGetIndex, 10, 64, out int[] indices, out int[] amounts, out string error),
                Is.True,
                error);
            Assert.That(indices, Is.EqualTo(new[] { 0, 1 }));
            Assert.That(amounts, Is.EqualTo(new[] { 7, 1 }));

            Assert.That(
                RaidResultsLootWire.TryDecode(
                    summary.Kind, summary.TotalValue, indices, amounts, TryGetLootId, out RaidResultsLootSummary decoded),
                Is.True);
            Assert.That(decoded.Kind, Is.EqualTo(RaidResultsLootKind.Lost));
            Assert.That(decoded.TotalValue, Is.EqualTo(123));
            Assert.That(decoded.Entries[0], Is.EqualTo(new RaidResultsLootEntry(Coin, 7)));
            Assert.That(decoded.Entries[1], Is.EqualTo(new RaidResultsLootEntry(Sword, 1)));
        }

        [Test]
        public void Encode_RejectsMoreEntriesThanTheNetworkCapacity()
        {
            RaidResultsLootSummary summary = CreateSummary(
                RaidResultsLootKind.Extracted, 1, new RaidResultsLootEntry(Coin, 1), new RaidResultsLootEntry(Sword, 1));

            Assert.That(
                RaidResultsLootWire.TryEncode(summary, TryGetIndex, 1, 64, out _, out _, out _),
                Is.False);
        }

        [Test]
        public void Encode_RejectsLootOutsideTheCatalog()
        {
            RaidResultsLootSummary summary = CreateSummary(
                RaidResultsLootKind.Extracted, 1, new RaidResultsLootEntry(new LootId("unknown"), 1));

            Assert.That(
                RaidResultsLootWire.TryEncode(summary, TryGetIndex, 10, 64, out _, out _, out _),
                Is.False);
        }

        [Test]
        public void Encode_RejectsCatalogIndexBeyondTheWireRange()
        {
            RaidResultsLootSummary summary = CreateSummary(
                RaidResultsLootKind.Extracted, 1, new RaidResultsLootEntry(Sword, 1));

            Assert.That(
                RaidResultsLootWire.TryEncode(summary, TryGetIndex, 10, 1, out _, out _, out _),
                Is.False);
        }

        [Test]
        public void Decode_RejectsMismatchedOrInvalidPayloads()
        {
            Assert.That(
                RaidResultsLootWire.TryDecode(
                    RaidResultsLootKind.Lost, 0, new[] { 0 }, new[] { 1, 2 }, TryGetLootId, out _),
                Is.False);
            Assert.That(
                RaidResultsLootWire.TryDecode(
                    RaidResultsLootKind.Lost, 0, new[] { 0 }, new[] { 0 }, TryGetLootId, out _),
                Is.False);
            Assert.That(
                RaidResultsLootWire.TryDecode(
                    RaidResultsLootKind.Lost, 0, new[] { 5 }, new[] { 1 }, TryGetLootId, out _),
                Is.False);
            Assert.That(
                RaidResultsLootWire.TryDecode(
                    RaidResultsLootKind.None, 0, new int[0], new int[0], TryGetLootId, out _),
                Is.False);
            Assert.That(
                RaidResultsLootWire.TryDecode(
                    RaidResultsLootKind.Lost, -1, new int[0], new int[0], TryGetLootId, out _),
                Is.False);
        }

        [Test]
        public void Decode_AcceptsAnEmptySummary()
        {
            Assert.That(
                RaidResultsLootWire.TryDecode(
                    RaidResultsLootKind.Extracted, 0, new int[0], new int[0], TryGetLootId,
                    out RaidResultsLootSummary summary),
                Is.True);
            Assert.That(summary.Entries, Is.Empty);
        }

        private static RaidResultsLootSummary CreateSummary(
            RaidResultsLootKind kind,
            long totalValue,
            params RaidResultsLootEntry[] entries) => new(kind, entries, totalValue);

        private static bool TryGetIndex(LootId lootId, out int index)
        {
            index = System.Array.IndexOf(Catalog, lootId.Value);
            return index >= 0;
        }

        private static bool TryGetLootId(int index, out LootId lootId)
        {
            lootId = default;
            if (index < 0 || index >= Catalog.Length)
            {
                return false;
            }

            lootId = new LootId(Catalog[index]);
            return true;
        }
    }
}
