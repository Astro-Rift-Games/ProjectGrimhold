#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewChecksTests
    {
        private readonly List<Object> _owned = new List<Object>();
        private AttackPreviewChecks _checks;
        private AttackPreviewFrame _frame;
        private T Own<T>(T value) where T : Object { _owned.Add(value); return value; }
        private Sprite Sprite(Vector2 pivot, bool unreadable = false)
        {
            var texture = Own(new Texture2D(8, 8, TextureFormat.RGBA32, false));
            var colors = new Color[64];
            // Texel centers (2.5,3.5), (3.5,3.5); weighted centroid (3.25,3.5).
            colors[3 * 8 + 2] = new Color(1, 1, 1, 0.25f);
            colors[3 * 8 + 3] = new Color(1, 1, 1, 0.75f);
            texture.SetPixels(colors);
            texture.Apply(false, unreadable);
            return Own(UnityEngine.Sprite.Create(texture, new Rect(0, 0, 8, 8), pivot, 8));
        }
        private SpriteRenderer Renderer(string name)
        {
            return Own(new GameObject(name)).AddComponent<SpriteRenderer>();
        }
        [SetUp]
        public void SetUp()
        {
            _checks = new AttackPreviewChecks();
            var hand = Renderer("Hand");
            hand.sprite = Sprite(new Vector2(3.25f / 8, 3.5f / 8));
            var weapon = Renderer("Weapon");
            weapon.sprite = hand.sprite;
            var body = Renderer("Body");
            hand.sortingOrder = 30;
            weapon.sortingOrder = 20;
            var grip = Own(new GameObject("Grip")).transform;
            grip.SetParent(hand.transform, false);
            _frame = new AttackPreviewFrame
            {
                HandRenderer = hand, WeaponRenderer = weapon, BodyRenderer = body, Grip = grip,
                ExpectedHandSprites = new[] { hand.sprite }, NeutralHandSprite = hand.sprite,
                BladeReach = 1f, TimeSeconds = 0.25f
            };
        }
        [TearDown]
        public void TearDown()
        {
            _checks?.Dispose();
            for (int i = _owned.Count - 1; i >= 0; i--) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }
        private AttackPreviewCheck Violation(AttackPreviewCheckId id) => _checks.Evaluate(_frame).Single(c => c.Id == id);

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void Centroid_UsesAlphaPivotFlipsAndWorldTransform(bool flipX, bool flipY)
        {
            var hand = _frame.HandRenderer;
            hand.sprite = Sprite(new Vector2(0.25f, 0.25f));
            hand.flipX = flipX; hand.flipY = flipY;
            hand.transform.SetPositionAndRotation(new Vector3(2, 3, 0), Quaternion.Euler(0, 0, 37));
            hand.transform.localScale = new Vector3(2, 0.5f, 1);
            var local = new Vector3((3.25f - 2) / 8 * (flipX ? -1 : 1), (3.5f - 2) / 8 * (flipY ? -1 : 1), 0);
            Assert.That(Vector3.Distance(_checks.DrawnHandWorldPoint(hand), hand.transform.TransformPoint(local)), Is.LessThan(0.001f));
        }
        [Test]
        public void Centroid_ReadsNonReadableTexture()
        {
            _frame.HandRenderer.sprite = Sprite(Vector2.zero, true);
            Assert.That(Vector3.Distance(_checks.DrawnHandWorldPoint(_frame.HandRenderer), new Vector3(3.25f / 8, 3.5f / 8)), Is.LessThan(0.001f));
        }
        [Test]
        public void WeaponGrip_UsesSpriteLocalUnitsNotPixelCoordinates()
        {
            var weapon = _frame.WeaponRenderer;
            weapon.flipX = true;
            weapon.transform.SetPositionAndRotation(Vector3.one, Quaternion.Euler(0, 0, 40));
            weapon.transform.localScale = new Vector3(2, -1, 1);
            var grip = new Vector2(0.25f, -0.5f);
            Assert.That(Vector3.Distance(_checks.WeaponGripWorldPoint(weapon, grip), weapon.transform.TransformPoint(new Vector3(-0.25f, -0.5f))), Is.LessThan(0.001f));
        }
        [Test]
        public void AlignedFrame_HasNoGripViolations()
        {
            Assert.That(_checks.Evaluate(_frame).Where(c => c.Id == AttackPreviewCheckId.WeaponGripOnHand || c.Id == AttackPreviewCheckId.GripTransformOnHand), Is.Empty);
        }
        [TestCase(2f, AttackPreviewSeverity.Warning)]
        [TestCase(4f, AttackPreviewSeverity.Error)]
        public void WeaponOffset_ReportsPixelsSeverityAndLeavesGripClean(float pixels, AttackPreviewSeverity severity)
        {
            _frame.WeaponRenderer.transform.position = new Vector3(pixels / 8, 0);
            var result = Violation(AttackPreviewCheckId.WeaponGripOnHand);
            Assert.That(result.MeasuredValue, Is.EqualTo(pixels).Within(0.001f));
            Assert.That(result.Severity, Is.EqualTo(severity));
            Assert.That(result.TimeSeconds, Is.EqualTo(0.25f));
            Assert.That(result.Hand, Is.EqualTo(AttackPreviewHand.Main));
            Assert.That(result.Threshold, Is.EqualTo(severity == AttackPreviewSeverity.Error ? 3 : 1.5f));
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.GripTransformOnHand), Is.False);
        }
        [Test]
        public void GripOffset_IsIndependentOfAlignedWeapon()
        {
            _frame.Grip.localPosition = new Vector3(0.5f, 0);
            Assert.That(Violation(AttackPreviewCheckId.GripTransformOnHand).MeasuredValue, Is.EqualTo(4).Within(0.001f));
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.WeaponGripOnHand), Is.False);
        }
        [Test]
        public void OffHandIdentity_RejectsRightSpriteByReferenceNotName()
        {
            _frame.Hand = AttackPreviewHand.Off;
            var left = Sprite(Vector2.zero);
            left.name = _frame.HandRenderer.sprite.name; // Same name cannot override source ownership.
            _frame.ExpectedHandSprites = new[] { left };
            Assert.That(Violation(AttackPreviewCheckId.HandSpriteIdentity).Hand, Is.EqualTo(AttackPreviewHand.Off));
        }
        [Test]
        public void DepthOrder_RejectsOppositeSidesOfBody()
        {
            _frame.HandRenderer.sortingOrder = -10;
            Assert.That(Violation(AttackPreviewCheckId.DepthOrder), Is.Not.Null);
        }
        [TestCase(true)]
        [TestCase(false)]
        public void DepthOrder_DoesNotInferOcclusionFromHandOverWeaponFlag(bool legacyFlag)
        {
            _frame.HandOverWeapon = legacyFlag;
            _frame.WeaponRenderer.sortingOrder = 32;
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.DepthOrder), Is.False,
                "Same-side ordering is a depth check; actual opaque overlap is a separate visual check.");
        }
        [Test]
        public void EmptyHand_SkipsHeldItemChecksEvenWithAStaleVisibleWeapon()
        {
            _frame.HasHeldItem = false;
            _frame.HandRenderer.sortingOrder = -10;
            _frame.WeaponRenderer.transform.position = Vector3.right;
            _frame.Grip.localPosition = Vector3.right;
            var results = _checks.Evaluate(_frame);
            Assert.That(results.Any(c => c.Id == AttackPreviewCheckId.DepthOrder ||
                c.Id == AttackPreviewCheckId.WeaponGripOnHand || c.Id == AttackPreviewCheckId.GripTransformOnHand ||
                c.Id == AttackPreviewCheckId.HandOccludedByWeapon), Is.False);
        }
        [Test]
        public void DepthOrder_BackHandAndWeaponPassUntilHandMovesInFrontOfBody()
        {
            _frame.BodyRenderer.sortingOrder = 2;
            _frame.HandRenderer.sortingOrder = -2;
            _frame.WeaponRenderer.sortingOrder = -10;
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.DepthOrder), Is.False);
            _frame.HandRenderer.sortingOrder = 30;
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.DepthOrder), Is.True);
        }
        [Test]
        public void VfxOrigin_RejectsEnabledDistantVfx()
        {
            _frame.VfxRenderer = Renderer("VFX");
            _frame.VfxRenderer.sprite = _frame.HandRenderer.sprite;
            _frame.VfxRenderer.transform.position = new Vector3(10, 0);
            Assert.That(Violation(AttackPreviewCheckId.VfxOrigin).Severity, Is.EqualTo(AttackPreviewSeverity.Warning));
            _frame.VfxRenderer.enabled = false;
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.VfxOrigin), Is.False);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ReturnsToIdle_RejectsResidualHandOrGripOffset(bool grip)
        {
            _frame.IsAfterDuration = true;
            (grip ? _frame.Grip : _frame.HandRenderer.transform).localPosition = new Vector3(0.5f, 0);
            Assert.That(Violation(AttackPreviewCheckId.ReturnsToIdle), Is.Not.Null);
        }
        [TestCase("sprite")]
        [TestCase("overlay")]
        [TestCase("scale")]
        public void ReturnsToIdle_IsolatedResidue_NamesOnlyFailingSubcondition(string condition)
        {
            _frame.IsAfterDuration = true;
            switch (condition)
            {
                case "sprite": _frame.NeutralHandSprite = Sprite(Vector2.zero); break;
                case "overlay": _frame.HasTransientOffHandOverlay = true; break;
                case "scale": _frame.Grip.localScale = Vector3.one * 2; break;
            }
            var result = Violation(AttackPreviewCheckId.ReturnsToIdle);
            Assert.That(result.IsViolation, Is.True);
            Assert.That(result.Severity, Is.EqualTo(AttackPreviewSeverity.Error));
            Assert.That(result.Message, Does.Contain(condition).IgnoreCase);
            foreach (string other in new[] { "sprite", "overlay", "scale" })
                if (other != condition) Assert.That(result.Message, Does.Not.Contain(other).IgnoreCase,
                    "A residue message must identify the actual failing sub-condition, not list unrelated alternatives.");
        }

        [Test]
        public void ReturnsToIdle_NonIdleLayerFailsEvenWhenPoseLooksNeutral()
        {
            _frame.IsAfterDuration = true;
            _frame.IsInIdleState = false;
            var result = Violation(AttackPreviewCheckId.ReturnsToIdle);
            Assert.That(result.Severity, Is.EqualTo(AttackPreviewSeverity.Error));
            Assert.That(result.Message, Does.Contain("idle state").IgnoreCase);
        }

        [Test]
        public void ReturnsToIdle_RejectsResidualOverlay()
        {
            _frame.IsAfterDuration = true;
            _frame.HasTransientOffHandOverlay = true;
            Assert.That(Violation(AttackPreviewCheckId.ReturnsToIdle), Is.Not.Null);
        }
        [Test]
        public void PivotTolerance_IsConfigurable()
        {
            _checks.Dispose();
            _checks = new AttackPreviewChecks(new AttackPreviewCheckSettings { PivotWarningPixels = 5, PivotErrorPixels = 6 });
            _frame.WeaponRenderer.transform.position = new Vector3(0.5f, 0, 0);
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.WeaponGripOnHand), Is.False);
        }
        [Test]
        public void ReturnsToIdle_AcceptsNeutralAndIgnoresUnfinishedAttack()
        {
            _frame.IsAfterDuration = true;
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.ReturnsToIdle), Is.False);
            _frame.IsAfterDuration = false;
            _frame.Grip.localPosition = Vector3.right;
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.ReturnsToIdle), Is.False);
        }
        [TestCase(false)]
        [TestCase(true)]
        public void ReturnsToIdle_RejectsRotationAndScaleIndependently(bool scale)
        {
            _frame.IsAfterDuration = true;
            if (scale) _frame.Grip.localScale = Vector3.one * 2;
            else _frame.Grip.localRotation = Quaternion.Euler(0, 0, 20);
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.ReturnsToIdle), Is.True);
        }
        [Test]
        public void VfxOrigin_AcceptsWithinReach()
        {
            _frame.VfxRenderer = Renderer("VFX");
            _frame.VfxRenderer.sprite = _frame.HandRenderer.sprite;
            _frame.VfxRenderer.transform.position = new Vector3(0.5f, 0, 0);
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.VfxOrigin), Is.False);
        }
        [Test]
        public void ReturnsToIdle_RejectsWrongSpriteWithoutTransformResidue()
        {
            _frame.IsAfterDuration = true;
            _frame.NeutralHandSprite = Sprite(Vector2.zero);
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.ReturnsToIdle), Is.True);
        }
        [Test]
        public void ReturnsToIdle_RejectsHandRotationWithoutSpriteResidue()
        {
            _frame.IsAfterDuration = true;
            _frame.HandRenderer.transform.localRotation = Quaternion.Euler(0, 0, 20);
            Assert.That(_checks.Evaluate(_frame).Any(c => c.Id == AttackPreviewCheckId.ReturnsToIdle), Is.True);
        }
    }
}
#endif
