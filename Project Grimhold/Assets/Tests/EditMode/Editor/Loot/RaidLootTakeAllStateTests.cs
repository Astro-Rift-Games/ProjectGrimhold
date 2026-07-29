using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class RaidLootTakeAllStateTests
    {
        private static readonly LootId Bone = new("bone");
        private static readonly LootId Coins = new("coins");

        [Test]
        public void TryBegin_CapturesValidEntriesInVisibleOrder()
        {
            var state = new RaidLootTakeAllState();

            Assert.That(state.TryBegin(new[]
            {
                new LootEntry(Bone, 2),
                default,
                new LootEntry(Coins, 3)
            }), Is.True);

            Assert.That(state.CurrentLootId, Is.EqualTo(Bone));
            Assert.That(state.TryAdvance(Bone), Is.True);
            Assert.That(state.CurrentLootId, Is.EqualTo(Coins));
            Assert.That(state.TryAdvance(Coins), Is.True);
            Assert.That(state.IsActive, Is.False);
        }

        [Test]
        public void ActiveSnapshot_DoesNotObserveLaterSourceChanges()
        {
            var entries = new[] { new LootEntry(Bone, 2) };
            var state = new RaidLootTakeAllState();
            Assert.That(state.TryBegin(entries), Is.True);

            entries[0] = new LootEntry(Coins, 5);

            Assert.That(state.CurrentLootId, Is.EqualTo(Bone));
        }

        [Test]
        public void RequestLifecycle_WaitsForCompletionBeforeAdvancing()
        {
            var state = new RaidLootTakeAllState();
            Assert.That(state.TryBegin(new[]
            {
                new LootEntry(Bone, 2),
                new LootEntry(Coins, 3)
            }), Is.True);

            Assert.That(state.TryMarkRequestSent(Bone), Is.True);
            Assert.That(state.IsAwaitingCompletion, Is.True);
            Assert.That(state.TryMarkRequestSent(Bone), Is.False);
            Assert.That(state.TryAdvance(Bone), Is.True);
            Assert.That(state.IsAwaitingCompletion, Is.False);
            Assert.That(state.CurrentLootId, Is.EqualTo(Coins));
        }

        [Test]
        public void Advance_AllowsRejectedOrLocallySkippedEntryToContinue()
        {
            var state = new RaidLootTakeAllState();
            Assert.That(state.TryBegin(new[]
            {
                new LootEntry(Bone, 2),
                new LootEntry(Coins, 3)
            }), Is.True);

            Assert.That(state.TryAdvance(Bone), Is.True);
            Assert.That(state.CurrentLootId, Is.EqualTo(Coins));
        }

        [Test]
        public void EmptyOrCancelledState_HasNoCurrentRequest()
        {
            var state = new RaidLootTakeAllState();
            Assert.That(state.TryBegin(System.Array.Empty<LootEntry>()), Is.False);

            Assert.That(state.TryBegin(new[] { new LootEntry(Bone, 2) }), Is.True);
            state.Cancel();

            Assert.That(state.IsActive, Is.False);
            Assert.That(state.IsAwaitingCompletion, Is.False);
            Assert.That(state.CurrentLootId, Is.EqualTo(default(LootId)));
        }
    }
}
