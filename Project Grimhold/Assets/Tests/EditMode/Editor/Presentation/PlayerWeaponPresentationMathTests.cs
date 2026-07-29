using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class PlayerWeaponPresentationMathTests
    {
        private const float Tolerance = 0.0001f;
        private const float AngleTolerance = 0.001f;
        private const string BasePlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string MeleePlayerPrefabPath = "Assets/Prefabs/NetworkPlayerMelee.prefab";
        private const string RangedPlayerPrefabPath = "Assets/Prefabs/NetworkPlayerRanged.prefab";

        [Test]
        public void ResolveSafeFacing_NormalizesFiniteNonZeroDirection()
        {
            Vector2 result = PlayerWeaponPresentationMath.ResolveSafeFacing(
                new Vector2(3f, 4f),
                Vector2.down);

            AssertVector(result, new Vector2(0.6f, 0.8f));
        }

        [TestCase(0f, 0f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        public void ResolveSafeFacing_InvalidDirectionRetainsPrevious(float x, float y)
        {
            Vector2 previous = new Vector2(-2f, 2f);

            Vector2 result = PlayerWeaponPresentationMath.ResolveSafeFacing(
                new Vector2(x, y),
                previous);

            AssertVector(result, previous.normalized);
        }

        [Test]
        public void ResolveSafeFacing_InvalidInitialStateFallsBackToDown()
        {
            Vector2 result = PlayerWeaponPresentationMath.ResolveSafeFacing(
                new Vector2(float.NaN, 0f),
                Vector2.zero);

            AssertVector(result, Vector2.down);
        }

        [Test]
        public void CalculateHandPosition_UsesEllipticalOrbitPerAxis()
        {
            Vector2 result = PlayerWeaponPresentationMath.CalculateHandPosition(
                new Vector2(0.1f, -0.2f),
                new Vector2(0.6f, 0.8f),
                new Vector2(0.5f, 0.25f),
                Vector2.zero);

            AssertVector(result, new Vector2(0.4f, 0f));
        }

        [Test]
        public void AnchorMovedUp_ShiftsTheEntireOrbitUp()
        {
            Vector2 facing = new Vector2(0.6f, 0.8f);
            Vector2 orbit = new Vector2(0.4f, 0.2f);
            Vector2 lowerPose = PlayerWeaponPresentationMath.CalculateHandPosition(
                Vector2.zero,
                facing,
                orbit,
                Vector2.zero);
            Vector2 upperPose = PlayerWeaponPresentationMath.CalculateHandPosition(
                new Vector2(0f, 0.45f),
                facing,
                orbit,
                Vector2.zero);

            AssertVector(upperPose - lowerPose, new Vector2(0f, 0.45f));
        }

        [Test]
        public void AnchorConversion_WorksAcrossBodyAndCombatVisualBranches()
        {
            GameObject root = new GameObject("PlayerVisualRoot");

            try
            {
                Transform body = new GameObject("Body").transform;
                body.SetParent(root.transform, false);
                body.position = new Vector3(3f, 2f, 0f);
                Transform anchor = new GameObject("HandOrbitAnchor").transform;
                anchor.SetParent(body, false);

                Transform combatVisuals = new GameObject("CombatVisuals").transform;
                combatVisuals.SetParent(root.transform, false);
                combatVisuals.position = new Vector3(1f, 2f, 0f);
                combatVisuals.rotation = Quaternion.Euler(0f, 0f, 90f);

                Vector2 anchorLocalPosition =
                    PlayerWeaponPresentationMath.CalculateAnchorLocalPosition(
                        anchor,
                        combatVisuals);

                AssertVector(anchorLocalPosition, new Vector2(0f, -2f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(0f, 1f, 0.15f, 0.68f)]
        [TestCase(0.70710678f, 0.70710678f, 0.362132f, 0.621421f)]
        [TestCase(0.70710678f, -0.70710678f, 0.362132f, 0.338579f)]
        [TestCase(0f, -1f, 0.15f, 0.28f)]
        [TestCase(-0.70710678f, -0.70710678f, -0.062132f, 0.338579f)]
        [TestCase(-0.70710678f, 0.70710678f, -0.062132f, 0.621421f)]
        public void HandPosition_SupportsSixBodyDirections(
            float facingX,
            float facingY,
            float expectedX,
            float expectedY)
        {
            Vector2 result = PlayerWeaponPresentationMath.CalculateHandPosition(
                new Vector2(0.1f, 0.5f),
                new Vector2(facingX, facingY),
                new Vector2(0.3f, 0.2f),
                new Vector2(0.05f, -0.02f));

            AssertVector(result, new Vector2(expectedX, expectedY));
        }

        [TestCase(0f)]
        [TestCase(180f)]
        public void IntermediateLateralAngles_RemainContinuous(float centerAngle)
        {
            Vector2 anchor = new Vector2(0f, 0.45f);
            Vector2 orbit = new Vector2(0.3f, 0.18f);
            Vector2 beforeFacing = DirectionFromDegrees(centerAngle - 1f);
            Vector2 centerFacing = DirectionFromDegrees(centerAngle);
            Vector2 afterFacing = DirectionFromDegrees(centerAngle + 1f);

            Vector2 before = PlayerWeaponPresentationMath.CalculateHandPosition(
                anchor, beforeFacing, orbit, Vector2.zero);
            Vector2 center = PlayerWeaponPresentationMath.CalculateHandPosition(
                anchor, centerFacing, orbit, Vector2.zero);
            Vector2 after = PlayerWeaponPresentationMath.CalculateHandPosition(
                anchor, afterFacing, orbit, Vector2.zero);

            Assert.That(Vector2.Distance(before, center), Is.LessThan(0.01f));
            Assert.That(Vector2.Distance(center, after), Is.LessThan(0.01f));

            Quaternion beforeRotation = Quaternion.Euler(
                0f, 0f, PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(beforeFacing));
            Quaternion centerRotation = Quaternion.Euler(
                0f, 0f, PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(centerFacing));
            Quaternion afterRotation = Quaternion.Euler(
                0f, 0f, PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(afterFacing));
            Assert.That(
                Quaternion.Angle(beforeRotation, centerRotation),
                Is.EqualTo(1f).Within(AngleTolerance));
            Assert.That(
                Quaternion.Angle(centerRotation, afterRotation),
                Is.EqualTo(1f).Within(AngleTolerance));
        }

        [Test]
        public void WeaponStanceOffset_ShiftsPoseWithoutReplacingAnchor()
        {
            Vector2 anchor = new Vector2(0.2f, 0.45f);
            Vector2 stanceOffset = new Vector2(-0.08f, 0.03f);
            Vector2 withoutStance = PlayerWeaponPresentationMath.CalculateHandPosition(
                anchor, Vector2.up, new Vector2(0.3f, 0.18f), Vector2.zero);
            Vector2 withStance = PlayerWeaponPresentationMath.CalculateHandPosition(
                anchor, Vector2.up, new Vector2(0.3f, 0.18f), stanceOffset);

            AssertVector(withStance - withoutStance, stanceOffset);
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, 1f, 90f)]
        [TestCase(-1f, 0f, 180f)]
        [TestCase(0f, -1f, -90f)]
        public void CalculateFacingAngleDegrees_RotatesPivotTowardFacing(
            float x,
            float y,
            float expectedAngle)
        {
            float angle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(
                new Vector2(x, y));

            Assert.That(angle, Is.EqualTo(expectedAngle).Within(Tolerance));
        }

        [TestCase(-1f, 0f, true)]
        [TestCase(-0.001f, 1f, true)]
        [TestCase(0f, 1f, false)]
        [TestCase(1f, 0f, false)]
        public void ShouldMirror_OnlyMirrorsLeftHemisphere(float x, float y, bool expected)
        {
            Assert.That(
                PlayerWeaponPresentationMath.ShouldMirror(new Vector2(x, y)),
                Is.EqualTo(expected));
        }

        [TestCase(0f, 1f, -10)]
        [TestCase(0f, -1f, 10)]
        [TestCase(1f, 0f, 10)]
        [TestCase(-1f, 0f, 10)]
        public void CalculateWeaponSortingOrder_PreservesFrontBackPolicy(
            float x,
            float y,
            int expectedOrder)
        {
            int weaponOrder =
                PlayerWeaponPresentationMath.CalculateWeaponSortingOrder(
                    new Vector2(x, y),
                    10,
                    -10);

            Assert.That(weaponOrder, Is.EqualTo(expectedOrder));
            Assert.That(weaponOrder + 1, Is.EqualTo(expectedOrder + 1));
        }

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(-135f)]
        public void GripAlignedWeaponPosition_KeepsGripAtPivot(float angle)
        {
            Vector2 gripPoint = new Vector2(0.2f, -0.35f);
            Vector2 scale = new Vector2(1.5f, 0.75f);
            Vector2 weaponPosition =
                PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                    gripPoint,
                    scale,
                    angle);

            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 transformedGrip = weaponPosition
                + (Vector2)(rotation * Vector2.Scale(gripPoint, scale));

            AssertVector(transformedGrip, Vector2.zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ParentReflection_DoesNotSeparateGripFromHandPivot(bool mirrored)
        {
            Vector2 gripPoint = new Vector2(-0.1f, 0.3f);
            Vector2 scale = new Vector2(1.2f, 0.8f);
            const float weaponAngle = 22f;
            Vector2 weaponPosition =
                PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                    gripPoint,
                    scale,
                    weaponAngle);

            Matrix4x4 pivotMatrix = Matrix4x4.TRS(
                new Vector3(0.25f, -0.15f),
                Quaternion.Euler(0f, 0f, 145f),
                new Vector3(1f, mirrored ? -1f : 1f, 1f));
            Matrix4x4 weaponMatrix = pivotMatrix * Matrix4x4.TRS(
                weaponPosition,
                Quaternion.Euler(0f, 0f, weaponAngle),
                scale);

            Vector3 handPivotWorld = pivotMatrix.MultiplyPoint3x4(Vector3.zero);
            Vector3 gripWorld = weaponMatrix.MultiplyPoint3x4(gripPoint);

            Assert.That(Vector3.Distance(handPivotWorld, gripWorld), Is.LessThan(Tolerance));
        }

        [Test]
        public void PlayerVariants_ReuseTheSingleBaseWeaponComposition()
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePlayerPrefabPath);
            GameObject meleePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MeleePlayerPrefabPath);
            GameObject rangedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RangedPlayerPrefabPath);

            Assert.That(basePrefab, Is.Not.Null);
            Assert.That(meleePrefab, Is.Not.Null);
            Assert.That(rangedPrefab, Is.Not.Null);

            PlayerWeaponPresenter basePresenter =
                basePrefab.GetComponentInChildren<PlayerWeaponPresenter>(true);
            PlayerWeaponPresenter meleePresenter =
                meleePrefab.GetComponentInChildren<PlayerWeaponPresenter>(true);
            PlayerWeaponPresenter rangedPresenter =
                rangedPrefab.GetComponentInChildren<PlayerWeaponPresenter>(true);
            Assert.That(basePresenter, Is.Not.Null);
            Assert.That(meleePrefab.GetComponentsInChildren<PlayerWeaponPresenter>(true), Has.Length.EqualTo(1));
            Assert.That(rangedPrefab.GetComponentsInChildren<PlayerWeaponPresenter>(true), Has.Length.EqualTo(1));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(meleePresenter), Is.SameAs(basePresenter));
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(rangedPresenter), Is.SameAs(basePresenter));
            Assert.That(basePresenter.gameObject.name, Is.EqualTo("CombatVisuals"));
            AssertOnlyWeaponSpecificPresenterOverrides(meleePrefab);
            AssertOnlyWeaponSpecificPresenterOverrides(rangedPrefab);

            PlayerCombatPresenter attackPresenter =
                basePrefab.GetComponentInChildren<PlayerCombatPresenter>(true);
            Assert.That(attackPresenter, Is.Not.Null);
            Assert.That(attackPresenter.enabled, Is.False);

            SerializedObject serializedPresenter = new SerializedObject(basePresenter);
            Transform handPivot = serializedPresenter.FindProperty("_handPivot")
                .objectReferenceValue as Transform;
            Transform handOrbitAnchor = serializedPresenter.FindProperty("_handOrbitAnchor")
                .objectReferenceValue as Transform;
            Transform handVisual = serializedPresenter.FindProperty("_handVisual")
                .objectReferenceValue as Transform;
            Transform weaponVisual = serializedPresenter.FindProperty("_weaponVisual")
                .objectReferenceValue as Transform;
            SpriteRenderer handRenderer = serializedPresenter.FindProperty("_handSpriteRenderer")
                .objectReferenceValue as SpriteRenderer;
            SpriteRenderer weaponRenderer = serializedPresenter.FindProperty("_weaponSpriteRenderer")
                .objectReferenceValue as SpriteRenderer;

            Assert.That(handPivot, Is.Not.Null);
            Assert.That(handOrbitAnchor, Is.Not.Null);
            Assert.That(handOrbitAnchor.name, Is.EqualTo("HandOrbitAnchor"));
            Assert.That(handOrbitAnchor.parent.name, Is.EqualTo("Body"));
            Assert.That(handOrbitAnchor.parent, Is.Not.SameAs(handPivot.parent));
            Assert.That(handVisual, Is.Not.Null);
            Assert.That(weaponVisual, Is.Not.Null);
            Assert.That(handRenderer, Is.Not.Null);
            Assert.That(weaponRenderer, Is.Not.Null);
            Assert.That(handVisual.parent, Is.SameAs(handPivot));
            Assert.That(weaponVisual.parent, Is.SameAs(handPivot));
            Assert.That(handRenderer.transform, Is.SameAs(handVisual));
            Assert.That(weaponRenderer.transform, Is.SameAs(weaponVisual));
            Assert.That(handRenderer.enabled, Is.True);
            Assert.That(weaponRenderer.enabled, Is.True);
            Assert.That(handRenderer.sortingLayerID, Is.EqualTo(SortingLayer.NameToID("Characters")));
            Assert.That(weaponRenderer.sortingLayerID, Is.EqualTo(SortingLayer.NameToID("Characters")));

            Assert.That(serializedPresenter.FindProperty("_bodyCenter"), Is.Null);
            Assert.That(serializedPresenter.FindProperty("_weaponStanceOffset"), Is.Not.Null);

            Animator bodyAnimator = basePrefab.GetComponentInChildren<Animator>(true);
            Assert.That(bodyAnimator, Is.Not.Null);
            Assert.That(bodyAnimator.enabled, Is.True);
        }

        [Test]
        public void PresenterEnabledBeforeFusionSpawn_UsesFallbackWithoutReadingNetworkedFacing()
        {
            GameObject root = new GameObject("UnspawnedPlayer");
            root.SetActive(false);

            try
            {
                Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
                rigidbody.bodyType = RigidbodyType2D.Kinematic;
                rigidbody.gravityScale = 0f;
                root.AddComponent<BoxCollider2D>();
                PlayerMovementNetworkController movement =
                    root.AddComponent<PlayerMovementNetworkController>();
                PlayerWeaponPresenter presenter =
                    root.AddComponent<PlayerWeaponPresenter>();

                Transform body = new GameObject("Body").transform;
                body.SetParent(root.transform, false);
                Transform handOrbitAnchor = new GameObject("HandOrbitAnchor").transform;
                handOrbitAnchor.SetParent(body, false);
                handOrbitAnchor.localPosition = new Vector3(0f, 0.45f, 0f);
                Transform combatVisuals = new GameObject("CombatVisuals").transform;
                combatVisuals.SetParent(root.transform, false);
                Transform handPivot = new GameObject("HandPivot").transform;
                handPivot.SetParent(combatVisuals, false);
                Transform handVisual = new GameObject("HandVisual").transform;
                handVisual.SetParent(handPivot, false);
                SpriteRenderer handRenderer =
                    handVisual.gameObject.AddComponent<SpriteRenderer>();
                Transform weaponVisual = new GameObject("WeaponVisual").transform;
                weaponVisual.SetParent(handPivot, false);
                SpriteRenderer weaponRenderer =
                    weaponVisual.gameObject.AddComponent<SpriteRenderer>();

                SerializedObject serializedPresenter = new SerializedObject(presenter);
                serializedPresenter.FindProperty("_movementStateSource").objectReferenceValue = movement;
                serializedPresenter.FindProperty("_handPivot").objectReferenceValue = handPivot;
                serializedPresenter.FindProperty("_handOrbitAnchor").objectReferenceValue = handOrbitAnchor;
                serializedPresenter.FindProperty("_handVisual").objectReferenceValue = handVisual;
                serializedPresenter.FindProperty("_handSpriteRenderer").objectReferenceValue = handRenderer;
                serializedPresenter.FindProperty("_weaponVisual").objectReferenceValue = weaponVisual;
                serializedPresenter.FindProperty("_weaponSpriteRenderer").objectReferenceValue = weaponRenderer;
                serializedPresenter.ApplyModifiedPropertiesWithoutUndo();

                root.SetActive(true);

                // EditMode does not synchronously dispatch the runtime callback
                // for every transient activation. Invoke only this presenter's
                // callback so Fusion NetworkBehaviours do not receive it too.
                MethodInfo onEnable = typeof(PlayerWeaponPresenter).GetMethod(
                    "OnEnable",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onEnable, Is.Not.Null);
                Assert.DoesNotThrow(() => onEnable.Invoke(presenter, null));
                Assert.That(handPivot.localPosition.x, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(handPivot.localPosition.y, Is.EqualTo(0.25f).Within(Tolerance));
                Assert.That(Quaternion.Angle(handPivot.localRotation, Quaternion.Euler(0f, 0f, -90f)), Is.LessThan(Tolerance));
                Assert.That(handPivot.localScale.y, Is.GreaterThan(0f));
                Assert.That(weaponRenderer.sortingOrder, Is.EqualTo(10));
                Assert.That(handRenderer.sortingOrder, Is.EqualTo(11));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }

        private static Vector2 DirectionFromDegrees(float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static void AssertOnlyWeaponSpecificPresenterOverrides(GameObject variantPrefab)
        {
            PropertyModification[] modifications =
                PrefabUtility.GetPropertyModifications(variantPrefab);

            foreach (PropertyModification modification in modifications)
            {
                if (!(modification.target is PlayerWeaponPresenter))
                {
                    continue;
                }

                CollectionAssert.Contains(
                    new[]
                    {
                        "_weaponStanceOffset.x",
                        "_weaponStanceOffset.y",
                        "_weaponGripPoint.x",
                        "_weaponGripPoint.y",
                        "_weaponAngleCorrection"
                    },
                    modification.propertyPath,
                    $"{variantPrefab.name} overrides shared presenter field "
                    + $"'{modification.propertyPath}'.");
            }
        }
    }
}
