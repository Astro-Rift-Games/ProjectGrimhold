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
        private const string TrainingSwordPath =
            "Assets/Scriptable Objects/Loot/Definitions/TrainingSword.asset";

        [Test]
        public void CalculateWeaponPivotPosition_UsesCanonicalFacingAndEllipticalOrbit()
        {
            Vector2 result = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(
                new Vector2(0.1f, -0.2f),
                CharacterVisualDirectionResolver.CanonicalSouthEast,
                new Vector2(0.5f, 0.25f),
                new Vector2(0.04f, 0.04f));

            float expectedX = 0.1f + 0.70710678f * 0.5f + 0.04f;
            float expectedY = -0.2f - 0.70710678f * 0.25f + 0.04f;

            AssertVector(result, new Vector2(expectedX, expectedY));
        }

        [Test]
        public void AnchorMovedUp_ShiftsTheEntireOrbitUp()
        {
            Vector2 canonicalFacing = CharacterVisualDirectionResolver.CanonicalNorthEast;
            Vector2 orbit = new Vector2(0.4f, 0.2f);
            Vector2 lowerPose = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(
                Vector2.zero,
                canonicalFacing,
                orbit,
                Vector2.zero);
            Vector2 upperPose = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(
                new Vector2(0f, 0.45f),
                canonicalFacing,
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
                Transform anchor = new GameObject("WeaponOrbitAnchor").transform;
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

        [Test]
        public void PositionalStability_AimAnglesWithinSameBucket_HaveIdenticalPositionAndDifferentRotation()
        {
            Vector2 anchor = new Vector2(0f, -0.18f);
            Vector2 orbit = new Vector2(0.26f, 0.10f);
            Vector2 stance = new Vector2(0.02f, 0.00f);

            // NorthEast angles: 20° and 40°
            Vector2 facing20 = DirectionFromDegrees(20f);
            Vector2 facing40 = DirectionFromDegrees(40f);

            CharacterVisualDirection bucket20 = CharacterVisualDirectionResolver.Resolve(facing20);
            CharacterVisualDirection bucket40 = CharacterVisualDirectionResolver.Resolve(facing40);
            Assert.That(bucket20, Is.EqualTo(CharacterVisualDirection.NorthEast));
            Assert.That(bucket40, Is.EqualTo(CharacterVisualDirection.NorthEast));

            Vector2 canonicalNE = CharacterVisualDirectionResolver.GetCanonicalVector(bucket20);
            Vector2 pos20 = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(anchor, canonicalNE, orbit, stance);
            Vector2 pos40 = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(anchor, canonicalNE, orbit, stance);

            AssertVector(pos20, pos40);

            float angle20 = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing20);
            float angle40 = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(facing40);
            Assert.That(angle20, Is.EqualTo(20f).Within(Tolerance));
            Assert.That(angle40, Is.EqualTo(40f).Within(Tolerance));
            Assert.That(Mathf.Abs(angle40 - angle20), Is.GreaterThan(10f));

            // SouthEast angles: -20° and -50°
            Vector2 facingM20 = DirectionFromDegrees(-20f);
            Vector2 facingM50 = DirectionFromDegrees(-50f);

            CharacterVisualDirection bucketM20 = CharacterVisualDirectionResolver.Resolve(facingM20);
            CharacterVisualDirection bucketM50 = CharacterVisualDirectionResolver.Resolve(facingM50);
            Assert.That(bucketM20, Is.EqualTo(CharacterVisualDirection.SouthEast));
            Assert.That(bucketM50, Is.EqualTo(CharacterVisualDirection.SouthEast));

            Vector2 canonicalSE = CharacterVisualDirectionResolver.GetCanonicalVector(bucketM20);
            Vector2 posM20 = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(anchor, canonicalSE, orbit, stance);
            Vector2 posM50 = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(anchor, canonicalSE, orbit, stance);

            AssertVector(posM20, posM50);
        }

        [Test]
        public void BoundaryTransition_CrossingSectorBoundary_ChangesBucketAndPivotPosition()
        {
            Vector2 anchor = new Vector2(0f, -0.18f);
            Vector2 neOrbit = new Vector2(0.26f, 0.10f);
            Vector2 neStance = new Vector2(0.02f, 0.00f);
            Vector2 nOrbit = new Vector2(0.25f, 0.08f);
            Vector2 nStance = new Vector2(0.00f, 0.02f);

            // 67° (NorthEast) vs 68° (North)
            Vector2 facing67 = DirectionFromDegrees(67f);
            Vector2 facing68 = DirectionFromDegrees(68f);

            CharacterVisualDirection bucket67 = CharacterVisualDirectionResolver.Resolve(facing67);
            CharacterVisualDirection bucket68 = CharacterVisualDirectionResolver.Resolve(facing68);

            Assert.That(bucket67, Is.EqualTo(CharacterVisualDirection.NorthEast));
            Assert.That(bucket68, Is.EqualTo(CharacterVisualDirection.North));

            Vector2 pos67 = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(
                anchor,
                CharacterVisualDirectionResolver.GetCanonicalVector(bucket67),
                neOrbit,
                neStance);

            Vector2 pos68 = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(
                anchor,
                CharacterVisualDirectionResolver.GetCanonicalVector(bucket68),
                nOrbit,
                nStance);

            Assert.That(Vector2.Distance(pos67, pos68), Is.GreaterThan(0.01f));
        }

        [Test]
        public void WeaponStanceOffset_ShiftsPoseWithoutReplacingAnchor()
        {
            Vector2 anchor = new Vector2(0.2f, -0.18f);
            Vector2 stanceOffset = new Vector2(-0.08f, 0.03f);
            Vector2 withoutStance = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(
                anchor, CharacterVisualDirectionResolver.CanonicalNorth, new Vector2(0.28f, 0.10f), Vector2.zero);
            Vector2 withStance = PlayerWeaponPresentationMath.CalculateWeaponPivotPosition(
                anchor, CharacterVisualDirectionResolver.CanonicalNorth, new Vector2(0.28f, 0.10f), stanceOffset);

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
        public void ParentReflection_DoesNotSeparateGripFromWeaponPivot(bool mirrored)
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

            Vector3 weaponPivotWorld = pivotMatrix.MultiplyPoint3x4(Vector3.zero);
            Vector3 gripWorld = weaponMatrix.MultiplyPoint3x4(gripPoint);

            Assert.That(Vector3.Distance(weaponPivotWorld, gripWorld), Is.LessThan(Tolerance));
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
            AssertNoPresenterOverrides(meleePrefab);
            AssertNoPresenterOverrides(rangedPrefab);

            PlayerCombatPresenter attackPresenter =
                basePrefab.GetComponentInChildren<PlayerCombatPresenter>(true);
            Assert.That(attackPresenter, Is.Not.Null);
            Assert.That(attackPresenter.enabled, Is.False);

            SerializedObject serializedPresenter = new SerializedObject(basePresenter);
            Transform weaponPivot = serializedPresenter.FindProperty("_weaponPivot")
                .objectReferenceValue as Transform;
            Transform weaponOrbitAnchor = serializedPresenter.FindProperty("_weaponOrbitAnchor")
                .objectReferenceValue as Transform;
            Transform weaponVisual = serializedPresenter.FindProperty("_weaponVisual")
                .objectReferenceValue as Transform;
            SpriteRenderer weaponRenderer = serializedPresenter.FindProperty("_weaponSpriteRenderer")
                .objectReferenceValue as SpriteRenderer;

            Assert.That(weaponPivot, Is.Not.Null);
            Assert.That(weaponPivot.childCount, Is.EqualTo(1));
            Assert.That(weaponOrbitAnchor, Is.Not.Null);
            Assert.That(weaponOrbitAnchor.name, Is.EqualTo("RightHandGrip"));
            Assert.That(weaponOrbitAnchor.parent.name, Is.EqualTo("VisualRoot"));
            Assert.That(weaponOrbitAnchor.parent, Is.Not.SameAs(weaponPivot.parent));
            Assert.That(weaponVisual, Is.Not.Null);
            Assert.That(weaponRenderer, Is.Not.Null);
            Assert.That(weaponVisual.parent, Is.SameAs(weaponPivot));
            Assert.That(weaponRenderer.transform, Is.SameAs(weaponVisual));
            Assert.That(weaponRenderer.enabled, Is.True);
            Assert.That(weaponRenderer.sortingLayerID, Is.EqualTo(SortingLayer.NameToID("Characters")));

            Assert.That(serializedPresenter.FindProperty("_directionPresets"), Is.Not.Null);
            Assert.That(serializedPresenter.FindProperty("_directionPresets._south"), Is.Not.Null);
            Assert.That(serializedPresenter.FindProperty("_directionPresets._southEast"), Is.Not.Null);
            Assert.That(serializedPresenter.FindProperty("_directionPresets._northEast"), Is.Not.Null);
            Assert.That(serializedPresenter.FindProperty("_directionPresets._north"), Is.Not.Null);
            Assert.That(serializedPresenter.FindProperty("_directionPresets._northWest"), Is.Not.Null);
            Assert.That(serializedPresenter.FindProperty("_directionPresets._southWest"), Is.Not.Null);
            Assert.That(serializedPresenter.FindProperty("_handVisual"), Is.Null);
            Assert.That(serializedPresenter.FindProperty("_handSpriteRenderer"), Is.Null);
            Assert.That(serializedPresenter.FindProperty("_bodyCenter"), Is.Null);
            Assert.That(serializedPresenter.FindProperty("_weaponStanceOffset"), Is.Null);
            Assert.That(serializedPresenter.FindProperty("_weaponGripPoint"), Is.Null);
            Assert.That(serializedPresenter.FindProperty("_weaponAngleCorrection"), Is.Null);

            Animator bodyAnimator = basePrefab.GetComponentInChildren<Animator>(true);
            Assert.That(bodyAnimator, Is.Not.Null);
            Assert.That(bodyAnimator.enabled, Is.True);
        }

        [Test]
        public void TrainingSword_ResolvesWeaponOwnedPresentation()
        {
            LootDefinition definition =
                AssetDatabase.LoadAssetAtPath<LootDefinition>(TrainingSwordPath);

            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.WorldSprite, Is.Not.Null);
            Assert.That(definition.WeaponDefinition, Is.Not.Null);
            AssertVector(
                definition.WeaponDefinition.Presentation.StanceOffset,
                Vector2.zero);
            AssertVector(
                definition.WeaponDefinition.Presentation.GripPoint,
                new Vector2(-0.1875f, 0.1875f));
            Assert.That(
                definition.WeaponDefinition.Presentation.AngleCorrection,
                Is.EqualTo(-135f).Within(AngleTolerance));
        }

        [Test]
        public void ApplyEquippedDefinition_NoneClearsSpriteAndIdentityChangeReplacesPresentation()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePlayerPrefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            WeaponDefinition secondWeapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            LootDefinition secondLoot = ScriptableObject.CreateInstance<LootDefinition>();

            try
            {
                PlayerWeaponPresenter presenter =
                    instance.GetComponentInChildren<PlayerWeaponPresenter>(true);
                SpriteRenderer renderer = new SerializedObject(presenter)
                    .FindProperty("_weaponSpriteRenderer")
                    .objectReferenceValue as SpriteRenderer;
                LootDefinition trainingSword =
                    AssetDatabase.LoadAssetAtPath<LootDefinition>(TrainingSwordPath);
                ConfigurePresentation(
                    secondWeapon,
                    new Vector2(0.15f, -0.05f),
                    new Vector2(0.3f, 0.1f),
                    27f);
                SerializedObject secondLootObject = new SerializedObject(secondLoot);
                secondLootObject.FindProperty("_weaponDefinition").objectReferenceValue = secondWeapon;
                secondLootObject.FindProperty("_worldSprite").objectReferenceValue = trainingSword.WorldSprite;
                secondLootObject.ApplyModifiedPropertiesWithoutUndo();

                InvokeApplyEquippedDefinition(presenter, trainingSword);
                Assert.That(renderer.enabled, Is.True);
                Assert.That(renderer.sprite, Is.SameAs(trainingSword.WorldSprite));
                AssertVector(ReadAppliedPresentation(presenter).GripPoint, new Vector2(-0.1875f, 0.1875f));

                InvokeApplyEquippedDefinition(presenter, secondLoot);
                Assert.That(renderer.sprite, Is.SameAs(trainingSword.WorldSprite));
                AssertVector(ReadAppliedPresentation(presenter).StanceOffset, new Vector2(0.15f, -0.05f));
                AssertVector(ReadAppliedPresentation(presenter).GripPoint, new Vector2(0.3f, 0.1f));
                Assert.That(ReadAppliedPresentation(presenter).AngleCorrection, Is.EqualTo(27f).Within(AngleTolerance));

                InvokeApplyEquippedDefinition(presenter, null);
                Assert.That(renderer.enabled, Is.False);
                Assert.That(renderer.sprite, Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(secondLoot);
                Object.DestroyImmediate(secondWeapon);
            }
        }

        [Test]
        public void WeaponPresentationConfiguration_AddsNoFusionNetworkedState()
        {
            const BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic;

            foreach (FieldInfo field in typeof(WeaponDefinition.PresentationConfig).GetFields(flags))
            {
                Assert.That(
                    field.GetCustomAttribute<Fusion.NetworkedAttribute>(),
                    Is.Null,
                    field.Name);
            }

            Assert.That(typeof(PlayerWeaponPresenter).BaseType, Is.SameAs(typeof(MonoBehaviour)));
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
                Transform weaponOrbitAnchor = new GameObject("WeaponOrbitAnchor").transform;
                weaponOrbitAnchor.SetParent(body, false);
                weaponOrbitAnchor.localPosition = new Vector3(0f, -0.18f, 0f);
                Transform combatVisuals = new GameObject("CombatVisuals").transform;
                combatVisuals.SetParent(root.transform, false);
                Transform weaponPivot = new GameObject("WeaponPivot").transform;
                weaponPivot.SetParent(combatVisuals, false);
                Transform weaponVisual = new GameObject("WeaponVisual").transform;
                weaponVisual.SetParent(weaponPivot, false);
                SpriteRenderer weaponRenderer =
                    weaponVisual.gameObject.AddComponent<SpriteRenderer>();

                SerializedObject serializedPresenter = new SerializedObject(presenter);
                serializedPresenter.FindProperty("_movementStateSource").objectReferenceValue = movement;
                serializedPresenter.FindProperty("_weaponPivot").objectReferenceValue = weaponPivot;
                serializedPresenter.FindProperty("_weaponOrbitAnchor").objectReferenceValue = weaponOrbitAnchor;
                serializedPresenter.FindProperty("_weaponVisual").objectReferenceValue = weaponVisual;
                serializedPresenter.FindProperty("_weaponSpriteRenderer").objectReferenceValue = weaponRenderer;
                serializedPresenter.FindProperty("_directionPresets._south._orbit").vector2Value = new Vector2(0.28f, 0.10f);
                serializedPresenter.FindProperty("_directionPresets._south._stanceOffset").vector2Value = new Vector2(0f, -0.04f);
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
                Assert.That(weaponPivot.localPosition.x, Is.EqualTo(0f).Within(Tolerance));
                Assert.That(weaponPivot.localPosition.y, Is.EqualTo(-0.18f).Within(Tolerance));
                Assert.That(Quaternion.Angle(weaponPivot.localRotation, Quaternion.Euler(0f, 0f, -90f)), Is.LessThan(Tolerance));
                Assert.That(weaponPivot.localScale.y, Is.GreaterThan(0f));
                Assert.That(weaponRenderer.sortingOrder, Is.EqualTo(20));
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

        private static void AssertNoPresenterOverrides(GameObject variantPrefab)
        {
            PropertyModification[] modifications =
                PrefabUtility.GetPropertyModifications(variantPrefab);

            foreach (PropertyModification modification in modifications)
            {
                if (!(modification.target is PlayerWeaponPresenter))
                {
                    continue;
                }

                Assert.Fail(
                    $"{variantPrefab.name} overrides neutral presenter field "
                    + $"'{modification.propertyPath}'.");
            }
        }

        private static void ConfigurePresentation(
            WeaponDefinition weapon,
            Vector2 stanceOffset,
            Vector2 gripPoint,
            float angleCorrection)
        {
            SerializedObject serializedWeapon = new SerializedObject(weapon);
            serializedWeapon.FindProperty("_presentation._stanceOffset").vector2Value = stanceOffset;
            serializedWeapon.FindProperty("_presentation._gripPoint").vector2Value = gripPoint;
            serializedWeapon.FindProperty("_presentation._angleCorrection").floatValue = angleCorrection;
            serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InvokeApplyEquippedDefinition(
            PlayerWeaponPresenter presenter,
            LootDefinition definition)
        {
            MethodInfo method = typeof(PlayerWeaponPresenter).GetMethod(
                "ApplyEquippedDefinition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(presenter, new object[] { definition });
        }

        private static WeaponDefinition.PresentationConfig ReadAppliedPresentation(
            PlayerWeaponPresenter presenter)
        {
            FieldInfo field = typeof(PlayerWeaponPresenter).GetField(
                "_equippedPresentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (WeaponDefinition.PresentationConfig)field.GetValue(presenter);
        }
    }
}
