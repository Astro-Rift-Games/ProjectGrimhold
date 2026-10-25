#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewOcclusionTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private AttackPreviewChecks _checks;
        private AttackPreviewFrame _frame;
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }

        // Hand has eight opaque texels (4x2). Each weapon column hides exactly 25%.
        // Transparent padding must not contribute to numerator or denominator.
        private Sprite Blob(int opaqueColumns, bool unreadable = false, Vector2? pivot = null)
        {
            var texture = Own(new Texture2D(8, 8, TextureFormat.RGBA32, false));
            var pixels = new Color[64];
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < opaqueColumns; x++) pixels[y * 8 + x] = Color.white;
            texture.SetPixels(pixels);
            texture.Apply(false, unreadable);
            return Own(Sprite.Create(texture, new Rect(0, 0, 8, 8), pivot ?? Vector2.zero, 8));
        }
        private SpriteRenderer Renderer(string name, int columns)
        {
            var renderer = Own(new GameObject(name)).AddComponent<SpriteRenderer>();
            renderer.sprite = Blob(columns);
            return renderer;
        }
        [SetUp]
        public void SetUp()
        {
            _checks = new AttackPreviewChecks();
            var hand = Renderer("Hand", 4);
            var weapon = Renderer("Weapon", 4);
            hand.sortingOrder = 4;
            weapon.sortingOrder = 20;
            var grip = Own(new GameObject("Grip")).transform;
            grip.SetParent(hand.transform, false);
            _frame = new AttackPreviewFrame
            {
                Hand = AttackPreviewHand.Off, HandRenderer = hand, WeaponRenderer = weapon,
                Grip = grip, ExpectedHandSprites = new[] { hand.sprite }
            };
        }
        [TearDown]
        public void TearDown()
        {
            _checks?.Dispose();
            for (int i = _owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }
        private AttackPreviewCheck[] Occlusion() => _checks.Evaluate(_frame)
            .Where(c => c.Id == AttackPreviewCheckId.HandOccludedByWeapon).ToArray();

        [TestCase(LootCategory.Shield, false)]
        [TestCase(LootCategory.Weapon, true)]
        public void FullCoverage_OnlyWeaponHeldHandsAreChecked(LootCategory heldCategory, bool shouldFail)
        {
            _frame.HeldItemCategory = heldCategory;
            var results = Occlusion();
            Assert.That(results.Length, Is.EqualTo(shouldFail ? 1 : 0));
            if (shouldFail)
            {
                Assert.That(results[0].Severity, Is.EqualTo(AttackPreviewSeverity.Error));
                Assert.That(results[0].MeasuredValue, Is.EqualTo(1f).Within(0.0001f));
            }
        }

        [Test]
        public void ShieldDefendFrame_IdentifiesShieldHeldHandForOcclusionExclusion()
        {
            var cell = AttackPreviewCatalog.Enumerate().First(c => c.Action == AttackPreviewAction.Defend &&
                c.OffHand != null && c.OffHand.Category == LootCategory.Shield);
            using var rig = new AttackPreviewRig();
            rig.Configure(cell);
            using var source = new AttackPreviewFrameSource(rig, cell);
            var frame = source.Capture(1, 0, false);
            Assert.That(frame.HasHeldItem, Is.True);
            Assert.That(frame.HeldItemCategory, Is.EqualTo(LootCategory.Shield));
        }

        [Test]
        public void OpaqueWeaponAboveHand_ReportsFullOcclusionAsError()
        {
            var result = Occlusion().Single();
            Assert.That(result.Severity, Is.EqualTo(AttackPreviewSeverity.Error));
            Assert.That(result.MeasuredValue, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(result.Threshold, Is.EqualTo(0.5f));
            Assert.That(result.Hand, Is.EqualTo(AttackPreviewHand.Off));
        }
        [Test]
        public void OpaqueWeaponBelowHand_DoesNotOcclude()
        {
            _frame.HandRenderer.sortingOrder = 30;
            Assert.That(Occlusion(), Is.Empty);
        }
        [TestCase(1, false)]
        [TestCase(2, true)]
        public void PartialCoverage_UsesOpaqueHandPixelFractionAndStrictThresholds(int columns, bool warns)
        {
            _frame.WeaponRenderer.sprite = Blob(columns);
            var results = Occlusion();
            Assert.That(results.Length, Is.EqualTo(warns ? 1 : 0));
            if (!warns) return; // Exactly 25% is not greater than the warning threshold.
            Assert.That(results[0].MeasuredValue, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(results[0].Threshold, Is.EqualTo(0.25f));
            Assert.That(results[0].Severity, Is.EqualTo(AttackPreviewSeverity.Warning));
        }
        [Test]
        public void SameSortingOrderWithOverlap_ReportsUnknownOrderingAsWarning()
        {
            _frame.WeaponRenderer.sortingOrder = _frame.HandRenderer.sortingOrder;
            var result = Occlusion().Single();
            Assert.That(result.Severity, Is.EqualTo(AttackPreviewSeverity.Warning));
            Assert.That(result.Message, Does.Contain("order").IgnoreCase);
        }
        [Test]
        public void SameSortingOrderWithoutOverlap_DoesNotWarn()
        {
            _frame.WeaponRenderer.sortingOrder = _frame.HandRenderer.sortingOrder;
            _frame.WeaponRenderer.transform.position = Vector3.right * 3;
            Assert.That(Occlusion(), Is.Empty);
        }
        [Test]
        public void WorldSpaceOverlap_RespectsTransformPivotAndFlips()
        {
            foreach (var renderer in new[] { _frame.HandRenderer, _frame.WeaponRenderer })
            {
                renderer.flipX = true;
                renderer.flipY = true;
                renderer.transform.SetPositionAndRotation(new Vector3(3, -2, 0), Quaternion.Euler(0, 0, 37));
                renderer.transform.localScale = new Vector3(2, 0.5f, 1);
            }
            Assert.That(Occlusion().Single().MeasuredValue, Is.EqualTo(1f).Within(0.0001f));
            _frame.WeaponRenderer.transform.position += Vector3.right * 10;
            Assert.That(Occlusion(), Is.Empty);
        }
        [Test]
        public void DifferentSpritePivots_AreMappedIntoWorldSpace()
        {
            _frame.HandRenderer.sprite = Blob(4, false, new Vector2(0.5f, 0.5f));
            _frame.WeaponRenderer.transform.position = new Vector3(-0.5f, -0.5f, 0);
            Assert.That(Occlusion().Single().MeasuredValue, Is.EqualTo(1f).Within(0.0001f));
        }
        [Test]
        public void FlippingOnlyHand_ChangesWhichWorldPixelsAreCovered()
        {
            _frame.HandRenderer.flipX = true;
            Assert.That(Occlusion(), Is.Empty);
            _frame.WeaponRenderer.transform.position = new Vector3(-0.5f, 0, 0);
            Assert.That(Occlusion().Single().MeasuredValue, Is.EqualTo(1f).Within(0.0001f));
        }
        [Test]
        public void DisabledWeapon_DoesNotOcclude()
        {
            _frame.WeaponRenderer.enabled = false;
            Assert.That(Occlusion(), Is.Empty);
        }
        [Test]
        public void NonReadableSprites_StillMeasureCoverage()
        {
            _frame.HandRenderer.sprite = Blob(4, true);
            _frame.WeaponRenderer.sprite = Blob(3, true);
            Assert.That(Occlusion().Single().MeasuredValue, Is.EqualTo(0.75f).Within(0.0001f));
        }
        [Test]
        public void ConfigurableFractions_CanEscalatePartialCoverage()
        {
            _checks.Dispose();
            _checks = new AttackPreviewChecks(new AttackPreviewCheckSettings
                { OcclusionWarningFraction = 0.1f, OcclusionErrorFraction = 0.2f });
            _frame.WeaponRenderer.sprite = Blob(1);
            Assert.That(Occlusion().Single().Severity, Is.EqualTo(AttackPreviewSeverity.Error));
        }
    }
}
#endif
