#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class PreviewPlayerPresentationStateTests
    {
        private static LootDefinition Sword => AttackPreviewCatalogTests.Sword;
        private static AttackPreviewCombination Cell(LootDefinition main, LootDefinition off,
            AttackPreviewAction action = AttackPreviewAction.MainAttack) => new AttackPreviewCombination(main, off, action, 3);
        private static LootDefinition Shield => AttackPreviewCatalog.Enumerate()
            .Select(c => c.MainHand).First(d => d != null && d.Category == LootCategory.Shield);
        private static LootDefinition TwoHanded => AttackPreviewCatalog.Enumerate()
            .Select(c => c.MainHand).First(d => d != null && d.WeaponDefinition != null &&
                d.WeaponDefinition.Handedness == WeaponHandedness.TwoHanded);

        [Test]
        public void EquippedDefinition_OffOnlyDoesNotFallBackFromEmptyMain()
        {
            var state = new PreviewPlayerPresentationState();
            state.Configure(Cell(null, Sword, AttackPreviewAction.OffAttack));
            Assert.That(state.TryGetEquippedDefinition(out var equipped), Is.False);
            Assert.That(equipped, Is.Null);
            Assert.That(state.TryGetSlotDefinition(state.GetActiveHandSlot(true), out var off), Is.True);
            Assert.That(off, Is.SameAs(Sword));
        }
        [Test]
        public void EquippedDefinition_MainShieldIsReturnedWithoutWeaponCategoryFilter()
        {
            var state = new PreviewPlayerPresentationState();
            var shield = Shield;
            state.Configure(Cell(shield, Sword, AttackPreviewAction.OffAttack));
            Assert.That(state.TryGetEquippedDefinition(out var equipped), Is.True);
            Assert.That(equipped, Is.SameAs(shield));
        }
        [Test]
        public void SlotDefinition_IsRawEquipmentEvenWhenOffHandIsBlocked()
        {
            var state = new PreviewPlayerPresentationState();
            var shield = Shield;
            state.Configure(Cell(TwoHanded, shield, AttackPreviewAction.Defend));
            Assert.That(state.TryGetSlotDefinition(state.GetActiveHandSlot(true), out var off), Is.True);
            Assert.That(off, Is.SameAs(shield));
            Assert.That(state.IsDefending, Is.False, "Raw slot visibility does not authorize blocked shield defense.");
            Assert.That(state.TryGetSlotDefinition(EquipmentSlot.None, out var missing), Is.False);
            Assert.That(missing, Is.Null);
        }
        [Test]
        public void BlockedOffHand_DoesNotPublishAttack()
        {
            var state = new PreviewPlayerPresentationState();
            int attacks = 0;
            state.AttackPerformed += attack => attacks++;
            state.Configure(Cell(TwoHanded, Sword, AttackPreviewAction.OffAttack));
            state.Evaluate(0);
            Assert.That(attacks, Is.Zero);
        }
        [Test]
        public void Defending_RequiresAnEligibleShield()
        {
            var state = new PreviewPlayerPresentationState();
            state.Configure(Cell(Sword, null, AttackPreviewAction.Defend));
            Assert.That(state.IsDefending, Is.False);
            state.Configure(Cell(Sword, Shield, AttackPreviewAction.Defend));
            Assert.That(state.IsDefending, Is.True);
            state.Configure(Cell(Shield, Sword, AttackPreviewAction.Defend));
            Assert.That(state.IsDefending, Is.True);
        }
        [Test]
        public void CatalogIdentity_SameWeaponInEitherHandUsesTheSameIndex()
        {
            var state = new PreviewPlayerPresentationState();
            AttackPerformedEvent last = default;
            state.AttackPerformed += attack => last = attack;
            state.Configure(Cell(Sword, Sword));
            state.Evaluate(0);
            int mainIndex = last.WeaponCatalogIndexPlusOne;
            state.Configure(Cell(Sword, Sword, AttackPreviewAction.OffAttack));
            state.Evaluate(0);
            Assert.That(mainIndex, Is.GreaterThan(0));
            Assert.That(last.WeaponCatalogIndexPlusOne, Is.EqualTo(mainIndex));
        }
        [Test]
        public void CatalogLookup_IsIndependentOfCurrentEquipment()
        {
            var state = new PreviewPlayerPresentationState();
            AttackPerformedEvent attack = default;
            state.AttackPerformed += value => attack = value;
            state.Configure(Cell(Sword, null));
            state.Evaluate(0);
            int oldIndex = attack.WeaponCatalogIndexPlusOne;
            var other = AttackPreviewCatalog.Enumerate().Select(c => c.MainHand)
                .First(d => d != null && d != Sword && d.WeaponDefinition != null);
            state.Configure(Cell(other, null));
            Assert.That(state.TryGetWeaponByCatalogIndexPlusOne(oldIndex, out var original), Is.True);
            Assert.That(original, Is.SameAs(Sword));
            Assert.That(state.TryGetWeaponByCatalogIndexPlusOne(0, out _), Is.False);
            Assert.That(state.TryGetWeaponByCatalogIndexPlusOne(-1, out _), Is.False);
        }
        [Test]
        public void UnconfiguredState_DoesNotClaimReadableAimOrInputAuthority()
        {
            var state = new PreviewPlayerPresentationState();
            Assert.That(state.IsEquipmentReadable, Is.False);
            Assert.That(state.IsAimReadable, Is.False);
            Assert.That(state.AimDirection, Is.EqualTo(Vector2.zero));
            Assert.That(state.FacingDirection, Is.EqualTo(Vector2.down));
            Assert.That(state.HasInputAuthority, Is.False);
            Assert.That(state.ActiveWeaponSetSlot, Is.EqualTo(WeaponSetSlot.None));
        }
        [Test]
        public void ConfirmedAttack_UsesAbsoluteClockAndSourceHand()
        {
            var state = new PreviewPlayerPresentationState();
            AttackPerformedEvent attack = default;
            state.AttackPerformed += value => attack = value;
            state.Configure(Cell(null, Sword, AttackPreviewAction.OffAttack));
            state.Evaluate(0.25f);
            Assert.That(attack.SourceSlot, Is.EqualTo(state.GetActiveHandSlot(true)));
            Assert.That(state.TryGetAttackPresentationSeconds(attack, out float seconds), Is.True);
            Assert.That(seconds, Is.EqualTo(0.25f));
            state.Evaluate(0.1f);
            Assert.That(state.TryGetAttackPresentationSeconds(attack, out seconds), Is.True);
            Assert.That(seconds, Is.EqualTo(0.1f));
        }
    }
}
#endif
