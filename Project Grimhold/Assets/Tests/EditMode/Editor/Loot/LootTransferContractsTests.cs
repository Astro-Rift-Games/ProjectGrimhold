using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootTransferContractsTests
    {
        [Test]
        public void LootEntry_ReportsValidityAndValueEquality()
        {
            var lootId = new LootId("coin");
            var left = new LootEntry(lootId, 4);
            var equal = new LootEntry(lootId, 4);
            var differentLoot = new LootEntry(new LootId("gem"), 4);
            var differentAmount = new LootEntry(lootId, 5);

            Assert.That(left.IsValid, Is.True);
            Assert.That(new LootEntry(default, 4).IsValid, Is.False);
            Assert.That(new LootEntry(lootId, 0).IsValid, Is.False);
            Assert.That(new LootEntry(lootId, -1).IsValid, Is.False);
            Assert.That(left, Is.EqualTo(equal));
            Assert.That(left == equal, Is.True);
            Assert.That(left != differentLoot, Is.True);
            Assert.That(left, Is.Not.EqualTo(differentAmount));
            Assert.That(left.GetHashCode(), Is.EqualTo(equal.GetHashCode()));

            var dictionary = new Dictionary<LootEntry, string> { [left] = "value" };
            Assert.That(dictionary[equal], Is.EqualTo("value"));
        }

        [Test]
        public void LootTransferRequest_ConservesDataAndReportsValidity()
        {
            var source = new EntityId(10);
            var destination = new EntityId(20);
            var loot = new LootId("coin");
            var request = new LootTransferRequest(source, destination, loot, 5, 100);

            Assert.That(request.SourceId, Is.EqualTo(source));
            Assert.That(request.DestinationId, Is.EqualTo(destination));
            Assert.That(request.LootId, Is.EqualTo(loot));
            Assert.That(request.RequestedAmount, Is.EqualTo(5));
            Assert.That(request.SimulationTick, Is.EqualTo(100));
            Assert.That(request.IsValid, Is.True);
            Assert.That(new LootTransferRequest(default, destination, loot, 5, 100).IsValid, Is.False);
            Assert.That(new LootTransferRequest(source, default, loot, 5, 100).IsValid, Is.False);
            Assert.That(new LootTransferRequest(source, destination, default, 5, 100).IsValid, Is.False);
            Assert.That(new LootTransferRequest(source, destination, loot, 0, 100).IsValid, Is.False);
            Assert.That(new LootTransferRequest(source, destination, loot, -1, 100).IsValid, Is.False);
        }

        [TestCase(typeof(LootTransferRequest))]
        [TestCase(typeof(LootTransferResult))]
        [TestCase(typeof(LootEntry))]
        public void ContractValueObjects_HaveOnlyReadonlyInstanceFields(Type type)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.That(type.IsValueType, Is.True);
            Assert.That(fields.All(field => field.IsInitOnly), Is.True);
        }

        [Test]
        public void LootTransferResult_SuccessRepresentsCompleteRequest()
        {
            var request = ValidRequest(7);
            LootTransferResult result = LootTransferResult.Succeeded(request);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Success, Is.True);
            Assert.That(result.TransferredAmount, Is.EqualTo(request.RequestedAmount));
            Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.None));
        }

        [Test]
        public void LootTransferResult_RejectionTransfersNothing()
        {
            LootTransferResult result = LootTransferResult.Rejected(LootTransferFailureReason.InventoryFull);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Success, Is.False);
            Assert.That(result.TransferredAmount, Is.Zero);
            Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.InventoryFull));
        }

        [Test]
        public void LootTransferResult_DefaultIsUninitialized()
        {
            LootTransferResult result = default;

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Success, Is.False);
            Assert.That(result.TransferredAmount, Is.Zero);
            Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.Uninitialized));
        }

        [Test]
        public void LootTransferResult_FactoriesRejectContradictoryInputs()
        {
            var invalidRequest = new LootTransferRequest(default, new EntityId(2), new LootId("coin"), 1, 10);

            Assert.Throws<ArgumentException>(() => LootTransferResult.Succeeded(invalidRequest));
            Assert.Throws<ArgumentOutOfRangeException>(() => LootTransferResult.Rejected(LootTransferFailureReason.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => LootTransferResult.Rejected(LootTransferFailureReason.Uninitialized));
            Assert.Throws<ArgumentOutOfRangeException>(() => LootTransferResult.Rejected((LootTransferFailureReason)999));
        }

        [TestCase(LootTransferFailureReason.InvalidLoot)]
        [TestCase(LootTransferFailureReason.InvalidAmount)]
        [TestCase(LootTransferFailureReason.SourceNotFound)]
        [TestCase(LootTransferFailureReason.DestinationNotFound)]
        [TestCase(LootTransferFailureReason.InsufficientAmount)]
        [TestCase(LootTransferFailureReason.InventoryFull)]
        [TestCase(LootTransferFailureReason.OutOfRange)]
        [TestCase(LootTransferFailureReason.MissingAuthority)]
        [TestCase(LootTransferFailureReason.ContainerUnavailable)]
        [TestCase(LootTransferFailureReason.Overflow)]
        public void LootTransferResult_RepresentsEveryRejection(LootTransferFailureReason reason)
        {
            LootTransferResult result = LootTransferResult.Rejected(reason);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.FailureReason, Is.EqualTo(reason));
        }

        [Test]
        public void LootCapabilities_AreSegregatedAndCommitsDoNotReturnResults()
        {
            var contentReader = new StubContentReader();
            var quantityReader = new StubQuantityReader();
            var slotReader = new StubSlotCapacityReader();
            var receiver = new StubReceiver();
            var extractor = new StubExtractor();

            Assert.That(contentReader, Is.Not.InstanceOf<ILootReceiver>());
            Assert.That(quantityReader, Is.Not.InstanceOf<ILootContentReader>());
            Assert.That(slotReader, Is.Not.InstanceOf<ILootExtractor>());
            Assert.That(receiver, Is.Not.InstanceOf<ILootExtractor>());
            Assert.That(extractor, Is.Not.InstanceOf<ILootReceiver>());
            Assert.That(typeof(ILootReceiver).GetMethod(nameof(ILootReceiver.CommitReceive)).ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(typeof(ILootExtractor).GetMethod(nameof(ILootExtractor.CommitExtraction)).ReturnType, Is.EqualTo(typeof(void)));
        }

        [Test]
        public void PlayerLootReceiver_ImplementsTemporaryInventoryCapabilities()
        {
            Type receiverType = typeof(PlayerLootReceiver);

            Assert.That(typeof(ILootReceiver).IsAssignableFrom(receiverType), Is.True);
            Assert.That(typeof(ILootContentReader).IsAssignableFrom(receiverType), Is.True);
            Assert.That(typeof(ILootQuantityReader).IsAssignableFrom(receiverType), Is.True);
            Assert.That(typeof(ILootExtractor).IsAssignableFrom(receiverType), Is.True);
            Assert.That(typeof(ILootSlotCapacityReader).IsAssignableFrom(receiverType), Is.True);
        }

        [Test]
        public void NetworkLootContainer_ImplementsBidirectionalEndpointCapabilities()
        {
            Type containerType = typeof(NetworkLootContainer);

            Assert.That(typeof(ILootReceiver).IsAssignableFrom(containerType), Is.True);
            Assert.That(typeof(ILootExtractor).IsAssignableFrom(containerType), Is.True);
            Assert.That(typeof(ILootContentReader).IsAssignableFrom(containerType), Is.True);
            Assert.That(typeof(ILootQuantityReader).IsAssignableFrom(containerType), Is.True);
            Assert.That(typeof(ILootSlotCapacityReader).IsAssignableFrom(containerType), Is.True);
        }

        [Test]
        public void LootContentReader_ReturnsReadOnlySnapshot()
        {
            var reader = new StubContentReader();

            Assert.That(reader.TryGetLootContent(out IReadOnlyList<LootEntry> content), Is.True);
            Assert.That(content, Has.Count.EqualTo(1));
            Assert.That(((IList<LootEntry>)content).IsReadOnly, Is.True);
            Assert.Throws<NotSupportedException>(() => ((IList<LootEntry>)content).Add(new LootEntry(new LootId("gem"), 1)));
        }

        [Test]
        public void EndpointValidationDoesNotMutateAndCommitAppliesExactAmount()
        {
            var receiver = new StubReceiver();
            var extractor = new StubExtractor();
            LootTransferRequest request = ValidRequest(6);

            Assert.That(receiver.ValidateReceive(request), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(extractor.ValidateExtraction(request), Is.EqualTo(LootTransferFailureReason.None));
            Assert.That(receiver.Amount, Is.Zero);
            Assert.That(extractor.Amount, Is.EqualTo(20));

            receiver.CommitReceive(request);
            extractor.CommitExtraction(request);

            Assert.That(receiver.Amount, Is.EqualTo(6));
            Assert.That(extractor.Amount, Is.EqualTo(14));
        }

        private static LootTransferRequest ValidRequest(int amount) => new(
            new EntityId(1),
            new EntityId(2),
            new LootId("coin"),
            amount,
            50);

        private sealed class StubContentReader : ILootContentReader
        {
            public EntityId Id => new(1);

            public bool TryGetLootContent(out IReadOnlyList<LootEntry> content)
            {
                content = Array.AsReadOnly(new[] { new LootEntry(new LootId("coin"), 2) });
                return true;
            }
        }

        private sealed class StubQuantityReader : ILootQuantityReader
        {
            public EntityId Id => new(1);
            public int GetLootAmount(LootId lootId) => lootId == new LootId("coin") ? 2 : 0;
        }

        private sealed class StubSlotCapacityReader : ILootSlotCapacityReader
        {
            public EntityId Id => new(1);
            public int SlotCapacity => 4;
            public int OccupiedSlotCount => 1;
        }

        private sealed class StubReceiver : ILootReceiver
        {
            public EntityId Id => new(2);
            public int Amount { get; private set; }

            public LootTransferFailureReason ValidateReceive(in LootTransferRequest request) =>
                request.DestinationId == Id
                    ? LootTransferFailureReason.None
                    : LootTransferFailureReason.DestinationNotFound;

            public void CommitReceive(in LootTransferRequest request) => Amount += request.RequestedAmount;
        }

        private sealed class StubExtractor : ILootExtractor
        {
            public EntityId Id => new(1);
            public int Amount { get; private set; } = 20;

            public LootTransferFailureReason ValidateExtraction(in LootTransferRequest request) =>
                request.SourceId != Id
                    ? LootTransferFailureReason.SourceNotFound
                    : request.RequestedAmount > Amount
                        ? LootTransferFailureReason.InsufficientAmount
                        : LootTransferFailureReason.None;

            public void CommitExtraction(in LootTransferRequest request) => Amount -= request.RequestedAmount;
        }
    }
}
