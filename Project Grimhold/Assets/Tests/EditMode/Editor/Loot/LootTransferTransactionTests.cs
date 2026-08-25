using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;

namespace Tests.EditMode.Loot
{
    public sealed class LootTransferTransactionTests
    {
        [Test]
        public void Execute_UsesRequiredValidationAndCommitOrder()
        {
            var calls = new List<string>();
            var source = new Source(calls, LootTransferFailureReason.None);
            var destination = new Destination(calls, LootTransferFailureReason.None);
            LootTransferRequest request = ValidRequest();

            LootTransferResult result = LootTransferTransaction.Execute(source, destination, request);

            Assert.That(result.Success, Is.True);
            Assert.That(calls, Is.EqualTo(new[]
            {
                "ValidateExtraction",
                "ValidateReceive",
                "CommitExtraction",
                "CommitReceive"
            }));
        }

        [Test]
        public void Execute_SourceRejection_DoesNotValidateOrCommitDestination()
        {
            var calls = new List<string>();
            var source = new Source(calls, LootTransferFailureReason.InsufficientAmount);
            var destination = new Destination(calls, LootTransferFailureReason.None);

            LootTransferResult result = LootTransferTransaction.Execute(source, destination, ValidRequest());

            Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.InsufficientAmount));
            Assert.That(calls, Is.EqualTo(new[] { "ValidateExtraction" }));
        }

        [Test]
        public void Execute_DestinationRejection_DoesNotCommitEitherEndpoint()
        {
            var calls = new List<string>();
            var source = new Source(calls, LootTransferFailureReason.None);
            var destination = new Destination(calls, LootTransferFailureReason.InventoryFull);

            LootTransferResult result = LootTransferTransaction.Execute(source, destination, ValidRequest());

            Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.InventoryFull));
            Assert.That(calls, Is.EqualTo(new[] { "ValidateExtraction", "ValidateReceive" }));
        }

        [Test]
        public void Execute_ResolvesProvenanceBetweenValidationsAndReturnsItAfterCommits()
        {
            var calls = new List<string>();
            var source = new ProvenanceSource(calls, 3);
            var destination = new Destination(calls, LootTransferFailureReason.None);

            LootTransferResult result = LootTransferTransaction.Execute(
                source, destination, ValidRequest(), out LootFirstAcquisitionResult acquisition);

            Assert.That(result.Success, Is.True);
            Assert.That(acquisition.EligibleAmount, Is.EqualTo(3));
            Assert.That(calls, Is.EqualTo(new[]
            {
                "ValidateExtraction",
                "ResolveFirstAcquisition",
                "ValidateReceive",
                "CommitExtraction",
                "CommitReceive"
            }));
        }

        [Test]
        public void Execute_DestinationRejection_ReturnsZeroProvenanceAndDoesNotCommit()
        {
            var calls = new List<string>();
            var source = new ProvenanceSource(calls, 3);
            var destination = new Destination(calls, LootTransferFailureReason.InventoryFull);

            LootTransferResult result = LootTransferTransaction.Execute(
                source, destination, ValidRequest(), out LootFirstAcquisitionResult acquisition);

            Assert.That(result.Success, Is.False);
            Assert.That(acquisition.EligibleAmount, Is.Zero);
            Assert.That(calls, Does.Not.Contain("CommitExtraction"));
        }

        [Test]
        public void Execute_OutOfRangeProvenance_IsIntegrationViolation()
        {
            var calls = new List<string>();
            var source = new ProvenanceSource(calls, 5);
            var destination = new Destination(calls, LootTransferFailureReason.None);

            Assert.Throws<InvalidOperationException>(() =>
                LootTransferTransaction.Execute(source, destination, ValidRequest(), out _));
            Assert.That(calls, Does.Not.Contain("ValidateReceive"));
        }

        [Test]
        public void ProvenanceContract_ExposesOnlyOnePublicQueryAndNoCommit()
        {
            MethodInfo[] methods = typeof(ILootFirstAcquisitionSource).GetMethods();

            Assert.That(methods, Has.Length.EqualTo(1));
            Assert.That(methods[0].Name, Is.EqualTo(nameof(ILootFirstAcquisitionSource.ResolveFirstAcquisition)));
        }

        [Test]
        public void Execute_RaidAwareEndpointsUseOnlyJointCommits()
        {
            var calls = new List<string>();
            var source = new RaidSource(calls, true);
            var destination = new RaidDestination(calls, true);

            LootTransferResult result = LootTransferTransaction.Execute(source, destination, ValidRequest());

            Assert.That(result.Success, Is.True);
            Assert.That(calls, Is.EqualTo(new[]
            {
                "ValidateExtraction",
                "ValidateReceive",
                "ResolveRaidOrigin",
                "ValidateRaidOriginReceive",
                "CommitRaidExtraction",
                "CommitRaidReceive"
            }));
        }

        [Test]
        public void Execute_MixedRaidAwarenessRejectsBeforeMutation()
        {
            var calls = new List<string>();
            var source = new RaidSource(calls, true);
            var destination = new RaidDestination(calls, false);

            Assert.Throws<InvalidOperationException>(() =>
                LootTransferTransaction.Execute(source, destination, ValidRequest()));
            Assert.That(calls, Does.Not.Contain("CommitRaidExtraction"));
            Assert.That(calls, Does.Not.Contain("CommitRaidReceive"));
        }

        [Test]
        public void Execute_InactiveRaidCompositionPreservesLegacyTownCommits()
        {
            var calls = new List<string>();
            var source = new RaidSource(calls, false);
            var destination = new RaidDestination(calls, false);

            LootTransferResult result = LootTransferTransaction.Execute(source, destination, ValidRequest());

            Assert.That(result.Success, Is.True);
            Assert.That(calls, Is.EqualTo(new[]
            {
                "ValidateExtraction",
                "ValidateReceive",
                "LegacyCommitExtraction",
                "LegacyCommitReceive"
            }));
        }

        private static LootTransferRequest ValidRequest() => new(
            new EntityId(1),
            new EntityId(2),
            new LootId("coin"),
            4,
            30);

        private sealed class Source : ILootExtractor
        {
            private readonly List<string> _calls;
            private readonly LootTransferFailureReason _failure;

            public Source(List<string> calls, LootTransferFailureReason failure)
            {
                _calls = calls;
                _failure = failure;
            }

            public EntityId Id => new(1);

            public LootTransferFailureReason ValidateExtraction(in LootTransferRequest request)
            {
                _calls.Add("ValidateExtraction");
                return _failure;
            }

            public void CommitExtraction(in LootTransferRequest request) => _calls.Add("CommitExtraction");
        }

        private sealed class Destination : ILootReceiver
        {
            private readonly List<string> _calls;
            private readonly LootTransferFailureReason _failure;

            public Destination(List<string> calls, LootTransferFailureReason failure)
            {
                _calls = calls;
                _failure = failure;
            }

            public EntityId Id => new(2);

            public LootTransferFailureReason ValidateReceive(in LootTransferRequest request)
            {
                _calls.Add("ValidateReceive");
                return _failure;
            }

            public void CommitReceive(in LootTransferRequest request) => _calls.Add("CommitReceive");
        }

        private sealed class ProvenanceSource : ILootExtractor, ILootFirstAcquisitionSource
        {
            private readonly List<string> _calls;
            private readonly int _eligibleAmount;

            public ProvenanceSource(List<string> calls, int eligibleAmount)
            {
                _calls = calls;
                _eligibleAmount = eligibleAmount;
            }

            public EntityId Id => new(1);

            public LootTransferFailureReason ValidateExtraction(in LootTransferRequest request)
            {
                _calls.Add("ValidateExtraction");
                return LootTransferFailureReason.None;
            }

            public LootFirstAcquisitionResult ResolveFirstAcquisition(in LootTransferRequest request)
            {
                _calls.Add("ResolveFirstAcquisition");
                return new LootFirstAcquisitionResult(_eligibleAmount);
            }

            public void CommitExtraction(in LootTransferRequest request) => _calls.Add("CommitExtraction");
        }

        private sealed class RaidSource : ILootExtractor, IRaidLootOriginSource
        {
            private readonly List<string> _calls;

            public RaidSource(List<string> calls, bool isAware)
            {
                _calls = calls;
                IsRaidLootOriginAware = isAware;
            }

            public EntityId Id => new(1);
            public bool IsRaidLootOriginAware { get; }

            public LootTransferFailureReason ValidateExtraction(in LootTransferRequest request)
            {
                _calls.Add("ValidateExtraction");
                return LootTransferFailureReason.None;
            }

            public void CommitExtraction(in LootTransferRequest request) => _calls.Add("LegacyCommitExtraction");

            public bool TryResolveRaidLootOriginTransfer(
                in LootTransferRequest request,
                out RaidLootOriginTransfer transfer)
            {
                _calls.Add("ResolveRaidOrigin");
                transfer = RaidLootOriginTransfer.Dungeon(request.RequestedAmount);
                return true;
            }

            public void CommitRaidLootExtraction(
                in LootTransferRequest request,
                RaidLootOriginTransfer transfer) => _calls.Add("CommitRaidExtraction");
        }

        private sealed class RaidDestination : ILootReceiver, IRaidLootOriginReceiver
        {
            private readonly List<string> _calls;

            public RaidDestination(List<string> calls, bool isAware)
            {
                _calls = calls;
                IsRaidLootOriginAware = isAware;
            }

            public EntityId Id => new(2);
            public bool IsRaidLootOriginAware { get; }

            public LootTransferFailureReason ValidateReceive(in LootTransferRequest request)
            {
                _calls.Add("ValidateReceive");
                return LootTransferFailureReason.None;
            }

            public void CommitReceive(in LootTransferRequest request) => _calls.Add("LegacyCommitReceive");

            public LootTransferFailureReason ValidateRaidLootOriginReceive(
                in LootTransferRequest request,
                RaidLootOriginTransfer transfer)
            {
                _calls.Add("ValidateRaidOriginReceive");
                return LootTransferFailureReason.None;
            }

            public void CommitRaidLootReceive(
                in LootTransferRequest request,
                RaidLootOriginTransfer transfer) => _calls.Add("CommitRaidReceive");
        }
    }
}
