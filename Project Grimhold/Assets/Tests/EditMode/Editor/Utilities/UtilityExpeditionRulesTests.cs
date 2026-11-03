#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Fusion;
using NUnit.Framework;
using UnityEngine;
using Assert = NUnit.Framework.Assert;

namespace Tests.EditMode.Utilities
{
    [TestFixture]
    public sealed class UtilityExpeditionRulesTests
    {
        private sealed class MockCooldownEndpoint :
            ILootExtractor,
            ILootReceiver,
            ILootQuantityReader,
            IUtilityCooldownProvider,
            IUtilityCooldownReceiver
        {
            private readonly Dictionary<LootId, int> _quantities = new();
            private readonly Dictionary<LootId, TickTimer> _cooldowns = new();

            public EntityId Id { get; }

            public MockCooldownEndpoint(int id)
            {
                Id = new EntityId(id);
            }

            public void Seed(LootId lootId, int amount, TickTimer cooldown)
            {
                _quantities[lootId] = amount;
                if (cooldown.IsRunning)
                {
                    _cooldowns[lootId] = cooldown;
                }
            }

            public int GetLootAmount(LootId lootId) =>
                _quantities.TryGetValue(lootId, out int amount) ? amount : 0;

            public LootTransferFailureReason ValidateExtraction(in LootTransferRequest request) =>
                request.SourceId == Id && GetLootAmount(request.LootId) >= request.RequestedAmount && request.RequestedAmount > 0
                    ? LootTransferFailureReason.None
                    : LootTransferFailureReason.InvalidAmount;

            public void CommitExtraction(in LootTransferRequest request)
            {
                int remaining = GetLootAmount(request.LootId) - request.RequestedAmount;
                if (remaining <= 0) _quantities.Remove(request.LootId);
                else _quantities[request.LootId] = remaining;
            }

            public LootTransferFailureReason ValidateReceive(in LootTransferRequest request) =>
                request.DestinationId == Id && request.RequestedAmount > 0
                    ? LootTransferFailureReason.None
                    : LootTransferFailureReason.DestinationNotFound;

            public void CommitReceive(in LootTransferRequest request) =>
                _quantities[request.LootId] = GetLootAmount(request.LootId) + request.RequestedAmount;

            public TickTimer GetUtilityCooldown(LootId lootId) =>
                _cooldowns.TryGetValue(lootId, out TickTimer timer) ? timer : TickTimer.None;

            public void ClearUtilityCooldown(LootId lootId) =>
                _cooldowns.Remove(lootId);

            public void SetUtilityCooldown(LootId lootId, TickTimer timer) =>
                _cooldowns[lootId] = timer;
        }

        [Test]
        public void CardinalDirectionHelper_CalculatesAllEightDirectionsCorrectly()
        {
            Vector2 origin = Vector2.zero;

            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(0f, 10f)), Is.EqualTo(CardinalDirection.North));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(10f, 0f)), Is.EqualTo(CardinalDirection.East));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(0f, -10f)), Is.EqualTo(CardinalDirection.South));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(-10f, 0f)), Is.EqualTo(CardinalDirection.West));

            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(10f, 10f)), Is.EqualTo(CardinalDirection.NorthEast));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(10f, -10f)), Is.EqualTo(CardinalDirection.SouthEast));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(-10f, -10f)), Is.EqualTo(CardinalDirection.SouthWest));
            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, new Vector2(-10f, 10f)), Is.EqualTo(CardinalDirection.NorthWest));

            Assert.That(CardinalDirectionHelper.GetCardinalDirection(origin, origin), Is.EqualTo(CardinalDirection.None));
        }

        [Test]
        public void CardinalDirectionHelper_LocalizedNamesAndAbbreviations_AreAccurate()
        {
            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.North), Is.EqualTo("Norte"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.North), Is.EqualTo("N"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.NorthEast), Is.EqualTo("Noreste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.NorthEast), Is.EqualTo("NE"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.East), Is.EqualTo("Este"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.East), Is.EqualTo("E"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.SouthEast), Is.EqualTo("Sureste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.SouthEast), Is.EqualTo("SE"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.South), Is.EqualTo("Sur"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.South), Is.EqualTo("S"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.SouthWest), Is.EqualTo("Suroeste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.SouthWest), Is.EqualTo("SW"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.West), Is.EqualTo("Oeste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.West), Is.EqualTo("W"));

            Assert.That(CardinalDirectionHelper.GetLocalizedName(CardinalDirection.NorthWest), Is.EqualTo("Noroeste"));
            Assert.That(CardinalDirectionHelper.GetAbbreviation(CardinalDirection.NorthWest), Is.EqualTo("NW"));
        }

        [Test]
        public void ArcaneElementDetectionRules_IdentifiesArcaneDefinitionsCorrectly()
        {
            Assert.That(ArcaneElementDetectionRules.IsArcaneDefinition(null), Is.False);

            var arcaneDef = ScriptableObject.CreateInstance<LootDefinition>();
            try
            {
                typeof(LootDefinition).GetField("_family", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(arcaneDef, "ArcaneComponents");
                Assert.That(ArcaneElementDetectionRules.IsArcaneDefinition(arcaneDef), Is.True);

                typeof(LootDefinition).GetField("_family", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(arcaneDef, "Material");
                typeof(LootDefinition).GetField("_tags", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(arcaneDef, new string[] { "crafting", "arcane" });
                Assert.That(ArcaneElementDetectionRules.IsArcaneDefinition(arcaneDef), Is.True);

                typeof(LootDefinition).GetField("_tags", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(arcaneDef, new string[] { "crafting", "iron" });
                Assert.That(ArcaneElementDetectionRules.IsArcaneDefinition(arcaneDef), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(arcaneDef);
            }
        }

        [Test]
        public void LootTransferResult_MaxTransportableExceeded_IsRecognizedAsRejectionReason()
        {
            var result = LootTransferResult.Rejected(LootTransferFailureReason.MaxTransportableExceeded);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(LootTransferFailureReason.MaxTransportableExceeded));
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void LootTransferTransaction_TransfersActiveUtilityCooldown_ToContainer_AndRestoresToAnyWithdrawingPlayer()
        {
            LootId compassId = PlayerConsumableNetworkController.RuneCompassId;
            TickTimer activeCooldown = CreateSyntheticRunningTimer(targetTick: 1850);

            var playerA = new MockCooldownEndpoint(1);
            var container = new MockCooldownEndpoint(100);
            var playerB = new MockCooldownEndpoint(2);

            playerA.Seed(compassId, 1, activeCooldown);

            // Deposit from Player A into Container
            var depositReq = new LootTransferRequest(playerA.Id, container.Id, compassId, 1, 1000);
            LootTransferResult depositResult = LootTransferTransaction.Execute(playerA, container, depositReq);

            Assert.That(depositResult.Success, Is.True);
            Assert.That(playerA.GetLootAmount(compassId), Is.EqualTo(0));
            Assert.That(playerA.GetUtilityCooldown(compassId).IsRunning, Is.False,
                "Depositing the utility must clear the active cooldown from the depositing player.");
            Assert.That(container.GetUtilityCooldown(compassId).TargetTick, Is.EqualTo(1850),
                "Container must preserve the exact cooldown target tick.");

            // Withdraw from Container into Player B (party member / enemy)
            var withdrawReq = new LootTransferRequest(container.Id, playerB.Id, compassId, 1, 1120);
            LootTransferResult withdrawResult = LootTransferTransaction.Execute(container, playerB, withdrawReq);

            Assert.That(withdrawResult.Success, Is.True);
            Assert.That(container.GetLootAmount(compassId), Is.EqualTo(0));
            Assert.That(container.GetUtilityCooldown(compassId).IsRunning, Is.False,
                "Withdrawing the utility must clear the stored cooldown from the empty container.");
            Assert.That(playerB.GetUtilityCooldown(compassId).TargetTick, Is.EqualTo(1850),
                "Withdrawing player must inherit the remaining cooldown target tick.");
        }

        private static TickTimer CreateSyntheticRunningTimer(int targetTick)
        {
            object boxed = default(TickTimer);
            FieldInfo[] fields = typeof(TickTimer).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            FieldInfo tickField = null;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType == typeof(int))
                {
                    tickField = fields[i];
                    break;
                }
            }

            Assert.That(tickField, Is.Not.Null, "Fusion.TickTimer must contain an integer target tick field.");
            tickField.SetValue(boxed, targetTick);
            var timer = (TickTimer)boxed;
            Assert.That(timer.IsRunning, Is.True);
            Assert.That(timer.TargetTick, Is.EqualTo(targetTick));
            return timer;
        }
    }
}
#endif
