using NUnit.Framework;
using UnityEditor;

namespace Tests.EditMode.Loot
{
    public sealed class LootDropExactAmountTests
    {
        [TestCase(1, 13)]
        [TestCase(5, 13)]
        [TestCase(12, 13)]
        [TestCase(1, 2)]
        public void ExactAmount_ResolvesTheChosenPartialAmount(int exactAmount, int availableAmount)
        {
            LootTransferFailureReason failure = LootTransferQuantityResolver.Resolve(
                LootTransferQuantityMode.ExactAmount, availableAmount, exactAmount, out int requestedAmount);

            Assert.That(failure, Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(requestedAmount, Is.EqualTo(exactAmount));
        }

        [TestCase(0, 13, LootTransferFailureReason.InvalidAmount)]
        [TestCase(-2, 13, LootTransferFailureReason.InvalidAmount)]
        [TestCase(13, 13, LootTransferFailureReason.InvalidAmount)]
        [TestCase(14, 13, LootTransferFailureReason.InsufficientAmount)]
        [TestCase(1, 1, LootTransferFailureReason.InvalidAmount)]
        [TestCase(1, 0, LootTransferFailureReason.InsufficientAmount)]
        public void ExactAmount_RejectsAmountsOutsideThePartialRange(
            int exactAmount, int availableAmount, LootTransferFailureReason expected)
        {
            LootTransferFailureReason failure = LootTransferQuantityResolver.Resolve(
                LootTransferQuantityMode.ExactAmount, availableAmount, exactAmount, out int requestedAmount);

            Assert.That(failure, Is.EqualTo(expected));
            Assert.That(requestedAmount, Is.Zero);
        }

        [Test]
        public void ModeOnlyResolution_StillRejectsExactAmountWithoutAChosenAmount()
        {
            LootTransferFailureReason failure = LootTransferQuantityResolver.Resolve(
                LootTransferQuantityMode.ExactAmount, 13, out int requestedAmount);

            Assert.That(failure, Is.EqualTo(LootTransferFailureReason.InvalidAmount));
            Assert.That(requestedAmount, Is.Zero);
        }

        [TestCase(LootTransferQuantityMode.SingleUnit, 1)]
        [TestCase(LootTransferQuantityMode.FullStack, 8)]
        public void ExistingModes_ResolveUnchangedWithoutAnExactAmount(
            LootTransferQuantityMode mode, int expected)
        {
            Assert.That(LootTransferQuantityResolver.Resolve(mode, 8, 0, out int requestedAmount),
                Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(requestedAmount, Is.EqualTo(expected));
        }

        [TestCase(LootTransferQuantityMode.SingleUnit)]
        [TestCase(LootTransferQuantityMode.FullStack)]
        public void ExistingModes_RejectAConflictingExactAmount(LootTransferQuantityMode mode)
        {
            Assert.That(LootTransferQuantityResolver.Resolve(mode, 8, 3, out int requestedAmount),
                Is.EqualTo(LootTransferFailureReason.InvalidAmount));
            Assert.That(requestedAmount, Is.Zero);
        }

        [Test]
        public void RequestedAmount_AffectsDropEqualityHashAndReplayConflicts()
        {
            var original = new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.ExactAmount, 7, 19, 4);
            var duplicate = new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.ExactAmount, 7, 19, 4);
            var conflict = new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.ExactAmount, 7, 19, 5);

            Assert.That(original.RequestedAmount, Is.EqualTo(4));
            Assert.That(duplicate, Is.EqualTo(original));
            Assert.That(duplicate.GetHashCode(), Is.EqualTo(original.GetHashCode()));
            Assert.That(conflict, Is.Not.EqualTo(original));
            Assert.That(conflict.GetHashCode(), Is.Not.EqualTo(original.GetHashCode()));
            var state = new LootDropRequestState();
            state.TryEnqueue(original, out _);
            Assert.That(state.TryEnqueue(duplicate, out _), Is.EqualTo(LootDropRequestState.Disposition.PendingDuplicate));
            Assert.That(state.TryEnqueue(conflict, out _), Is.EqualTo(LootDropRequestState.Disposition.PendingPayloadConflict));
        }

        [Test]
        public void ModeOnlyIdentities_CarryNoRequestedAmount()
        {
            Assert.That(new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.FullStack).RequestedAmount, Is.Zero);
            Assert.That(new LootDropRequestIdentity(5, 2, LootTransferQuantityMode.FullStack, 7, 19).RequestedAmount, Is.Zero);
        }

        [Test]
        public void ExactAmountClient_RetainsAddressAndAmount()
        {
            var state = new LootDropClientRequestState();

            Assert.That(state.TryCreateExactAmountCandidate(2, 4, 7, 19, out LootDropRequestIdentity identity), Is.True);
            Assert.That(identity.QuantityMode, Is.EqualTo(LootTransferQuantityMode.ExactAmount));
            Assert.That(identity.RequestedAmount, Is.EqualTo(4));
            Assert.That(identity.SourceInventorySlotIndex, Is.EqualTo(7));
            Assert.That(identity.ExpectedInventoryRevision, Is.EqualTo(19));
            Assert.That(identity.RequestSequence, Is.EqualTo(1));
        }

        [TestCase(0, 7)]
        [TestCase(-1, 7)]
        [TestCase(4, -1)]
        [TestCase(4, 30)]
        public void ExactAmountClient_RejectsInvalidAmountOrAddressWithoutConsumingSequence(int amount, int slotIndex)
        {
            var state = new LootDropClientRequestState();

            Assert.That(state.TryCreateExactAmountCandidate(2, amount, slotIndex, 19, out _), Is.False);
            Assert.That(state.TryCreateExactAmountCandidate(2, 4, 7, 19, out LootDropRequestIdentity valid), Is.True);
            Assert.That(valid.RequestSequence, Is.EqualTo(1));
        }

        [Test]
        public void ModeOnlyClient_RejectsExactAmountMode()
        {
            var state = new LootDropClientRequestState();

            Assert.That(state.TryCreateCandidate(2, LootTransferQuantityMode.ExactAmount, out _), Is.False);
            Assert.That(state.TryCreateCandidate(2, LootTransferQuantityMode.ExactAmount, 7, 19, out _), Is.False);
        }

        [TestCase(4, true)]
        [TestCase(5, false)]
        [TestCase(1, false)]
        public void Confirmation_SuccessfulExactDropMustMatchTheRequestedAmount(int droppedAmount, bool accepted)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<LootDefinitionCatalog>(
                "Assets/Scriptable Objects/Loot/Catalogs/LootDefinitionCatalog.asset");
            Assert.That(catalog, Is.Not.Null);
            var expected = new LootDropRequestIdentity(5, 0, LootTransferQuantityMode.ExactAmount, 7, 19, 4);

            bool reconstructed = LootDropConfirmation.TryReconstruct(5, 0, droppedAmount, true,
                (int)LootDropFailureReason.None, 10, expected, catalog,
                out LootDropConfirmation confirmation, out string error);

            Assert.That(reconstructed, Is.EqualTo(accepted), error);
            if (accepted)
            {
                Assert.That(confirmation.Result.DroppedAmount, Is.EqualTo(4));
            }
        }
    }
}
