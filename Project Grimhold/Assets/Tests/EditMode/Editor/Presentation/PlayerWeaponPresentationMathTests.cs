using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Tests.EditMode.Presentation
{
    public sealed class PlayerWeaponPresentationMathTests
    {
        private const float Tolerance = 0.0001f;
        private const float ReflectionTolerance = 0.00001f;
        private const string BasePlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string MeleePlayerPrefabPath = "Assets/Prefabs/NetworkPlayerMelee.prefab";
        private const string RangedPlayerPrefabPath = "Assets/Prefabs/NetworkPlayerRanged.prefab";
        private const string ControllerPath = "Assets/Animations/Player/Character.controller";
        private const string TrainingSwordPath =
            "Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset";

        private static readonly string[] AttackClipPaths =
        {
            "Assets/Animations/Weapons/ArmingSword_Attack.anim",
            "Assets/Animations/Weapons/Rapier_Attack.anim",
            "Assets/Animations/Weapons/RondelDagger_Attack.anim",
            "Assets/Animations/Weapons/MagicWand_Attack.anim"
        };

        [TestCase(0f)]
        [TestCase(37f)]
        [TestCase(-135f)]
        public void GripAlignedWeaponPosition_KeepsGripAtParentOrigin(float angle)
        {
            Vector2 gripPoint = new Vector2(0.2f, -0.35f);
            Vector2 scale = new Vector2(1.5f, 0.75f);
            Vector2 weaponPosition =
                PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                    gripPoint,
                    scale,
                    angle);

            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            Vector2 transformedGrip = weaponPosition +
                (Vector2)(rotation * Vector2.Scale(gripPoint, scale));

            AssertVector(transformedGrip, Vector2.zero);
        }

        [TestCase(0f, -1f, -90f)]
        [TestCase(1f, -1f, -45f)]
        [TestCase(1f, 1f, 45f)]
        [TestCase(0f, 1f, 90f)]
        [TestCase(-1f, 1f, 135f)]
        [TestCase(-1f, -1f, -135f)]
        public void FacingAngle_MatchesSixDirectionBodyPose(float x, float y, float expected)
        {
            float angle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(
                new Vector2(x, y));

            Assert.That(angle, Is.EqualTo(expected).Within(Tolerance));
        }

        [TestCase(1f, false)]
        [TestCase(0f, false)]
        [TestCase(-1f, true)]
        public void WeaponMirror_FollowsBodySide(float x, bool expected)
        {
            Assert.That(
                PlayerWeaponPresentationMath.ShouldMirror(new Vector2(x, 0f)),
                Is.EqualTo(expected));
        }

        [TestCase(-90f)]
        [TestCase(180f)]
        [TestCase(0f)]
        [TestCase(-45f)]
        [TestCase(30f)]
        public void ResolveAngleCorrection_KeepsTheCorrectionWhenNotMirrored(float correction)
        {
            Assert.That(
                PlayerWeaponPresentationMath.ResolveAngleCorrection(correction, false),
                Is.EqualTo(correction));
        }

        [Test]
        public void ResolveAngleCorrection_LeavesTheAlignedMinusNinetyCorrectionUnchangedWhenMirrored()
        {
            Assert.That(
                PlayerWeaponPresentationMath.ResolveAngleCorrection(-90f, true),
                Is.EqualTo(-90f));
        }

        [Test]
        public void ResolveAngleCorrection_MirrorsAcrossTheArtAxisForHeldAcrossArt()
        {
            Assert.That(
                PlayerWeaponPresentationMath.ResolveAngleCorrection(180f, true),
                Is.EqualTo(-360f));
        }

        // The pivot turns to the facing and reflects Y when facing left, so the mirrored visual needs the resolved
        // correction to end up as a reflection across the weapon art axis (sprite +Y): in pivot space
        // diag(1,-1) * R(resolved) * (p - grip) must equal R(correction) * (-(p - grip).x, (p - grip).y).
        [TestCase(-90f)]
        [TestCase(180f)]
        [TestCase(0f)]
        [TestCase(-45f)]
        [TestCase(30f)]
        public void MirroredHeldWeapon_IsAReflectionAcrossTheWeaponArtAxis(float correction)
        {
            const float facingAngle = 135f;
            Vector2 grip = new Vector2(0.2f, -0.35f);
            Vector2[] points =
            {
                grip,
                new Vector2(0f, 0.75f),
                new Vector2(0.3f, -0.1f),
                new Vector2(-0.4f, 0.6f)
            };
            Quaternion facing = Quaternion.Euler(0f, 0f, facingAngle);
            Quaternion art = Quaternion.Euler(0f, 0f, correction);
            var pivotObject = new GameObject("WeaponPivot");
            var visualObject = new GameObject("WeaponVisual");
            try
            {
                Transform pivot = pivotObject.transform;
                Transform visual = visualObject.transform;
                visual.SetParent(pivot, false);
                pivot.localRotation = facing;

                foreach (bool mirrored in new[] { false, true })
                {
                    float resolved = PlayerWeaponPresentationMath.ResolveAngleCorrection(correction, mirrored);
                    pivot.localScale = new Vector3(1f, mirrored ? -1f : 1f, 1f);
                    visual.localPosition = PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                        grip, Vector2.one, resolved);
                    visual.localRotation = Quaternion.Euler(0f, 0f, resolved);

                    AssertVector(visual.TransformPoint(grip), Vector2.zero, ReflectionTolerance);
                    foreach (Vector2 point in points)
                    {
                        Vector2 offset = point - grip;
                        Vector2 expectedInPivotSpace = mirrored
                            ? (Vector2)(art * new Vector3(-offset.x, offset.y, 0f))
                            : (Vector2)(art * offset);
                        AssertVector(visual.TransformPoint(point), (Vector2)(facing * expectedInPivotSpace), ReflectionTolerance);

                        if (!mirrored) continue;
                        Vector2 reflected = (Vector2)(Quaternion.Euler(0f, 0f, resolved) * offset);
                        reflected.y = -reflected.y;
                        AssertVector(reflected, expectedInPivotSpace, ReflectionTolerance);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(visualObject);
                Object.DestroyImmediate(pivotObject);
            }
        }

        // Critical regression proof: only the weapons whose sources hold them across the facing (Great Hammer,
        // Long Sword, Zweihander) moved to the 180 correction; every other weapon keeps -90, for which the resolved
        // value is bit-identical, so its held pose is exactly what it was before the mirror was generalized.
        [Test]
        public void ExistingWeapons_KeepTheirHeldPoseBitIdentical()
        {
            string[] guids = AssetDatabase.FindAssets("t:WeaponDefinition",
                new[] { "Assets/Scriptable Objects/Loot/Definitions" });
            int checkedWeapons = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (weapon == null) continue;

                WeaponDefinition.PresentationConfig presentation = weapon.Presentation;
                if (path.EndsWith("GreatHammerWeaponDefinition.asset") ||
                    path.EndsWith("LongSwordCombatDefinition.asset") ||
                    path.EndsWith("ZweihanderWeaponDefinition.asset"))
                {
                    Assert.That(presentation.AngleCorrection, Is.EqualTo(180f), path);
                    continue;
                }

                Assert.That(presentation.AngleCorrection, Is.EqualTo(-90f), path);
                Vector2 expected = PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                    presentation.GripPoint, Vector2.one, presentation.AngleCorrection);
                foreach (bool mirrored in new[] { false, true })
                {
                    float resolved = PlayerWeaponPresentationMath.ResolveAngleCorrection(
                        presentation.AngleCorrection, mirrored);
                    Assert.That(resolved, Is.EqualTo(presentation.AngleCorrection), $"{path} mirrored={mirrored}");
                    Vector2 actual = PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                        presentation.GripPoint, Vector2.one, resolved);
                    Assert.That(actual.x, Is.EqualTo(expected.x), $"{path} mirrored={mirrored} x");
                    Assert.That(actual.y, Is.EqualTo(expected.y), $"{path} mirrored={mirrored} y");
                }
                checkedWeapons++;
            }
            Assert.That(checkedWeapons, Is.GreaterThanOrEqualTo(8), "Every other weapon definition is covered.");
        }

        [Test]
        public void PlayerVariants_ReuseAnimatorOwnedHeldVisualHierarchy()
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
            AssertNoPresenterOverrides(meleePrefab);
            AssertNoPresenterOverrides(rangedPrefab);

            SerializedObject serializedPresenter = new SerializedObject(basePresenter);
            Transform mainGrip = Reference<Transform>(serializedPresenter, "_mainHandGrip");
            PlayerAnimatorView animatorView = Reference<PlayerAnimatorView>(serializedPresenter, "_animatorView");
            Transform mainPivot = Reference<Transform>(serializedPresenter, "_mainHandWeaponPivot");
            Transform mainVisual = Reference<Transform>(serializedPresenter, "_mainHandWeaponVisual");
            SpriteRenderer mainRenderer = Reference<SpriteRenderer>(serializedPresenter, "_mainHandRenderer");
            Transform offGrip = Reference<Transform>(serializedPresenter, "_offHandGrip");
            Transform offVisual = Reference<Transform>(serializedPresenter, "_offHandVisual");
            SpriteRenderer offRenderer = Reference<SpriteRenderer>(serializedPresenter, "_offHandRenderer");

            Assert.That(mainGrip.name, Is.EqualTo("MainHandGrip"));
            Assert.That(animatorView, Is.SameAs(basePrefab.GetComponentInChildren<PlayerAnimatorView>(true)));
            Assert.That(mainGrip.parent.name, Is.EqualTo("RightHand"));
            Assert.That(mainPivot.parent, Is.SameAs(mainGrip));
            Assert.That(mainVisual.parent, Is.SameAs(mainPivot));
            Assert.That(mainRenderer.transform, Is.SameAs(mainVisual));
            Assert.That(offGrip.name, Is.EqualTo("OffHandGrip"));
            Assert.That(offGrip.parent.name, Is.EqualTo("LeftHand"));
            Assert.That(offVisual.parent, Is.SameAs(offGrip));
            Assert.That(offRenderer.transform, Is.SameAs(offVisual));
            Assert.That(mainRenderer.sortingLayerID, Is.EqualTo(SortingLayer.NameToID("Characters")));
            Assert.That(offRenderer.sortingLayerID, Is.EqualTo(SortingLayer.NameToID("Characters")));

            Assert.That(basePrefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(component => component != null && component.GetType().Name == "PlayerCombatPresenter"), Is.False);
        }

        [Test]
        public void PlayerPrefab_OffersOneWeaponVisualToTheMainHandOrItsOwnPose()
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePlayerPrefabPath);
            Assert.That(basePrefab, Is.Not.Null);
            PlayerWeaponPresenter presenter = basePrefab.GetComponentInChildren<PlayerWeaponPresenter>(true);
            Assert.That(presenter, Is.Not.Null);

            SerializedObject serializedPresenter = new SerializedObject(presenter);
            Transform mainGrip = Reference<Transform>(serializedPresenter, "_mainHandGrip");
            Transform weaponPose = Reference<Transform>(serializedPresenter, "_weaponPose");
            Transform offGrip = Reference<Transform>(serializedPresenter, "_offHandGrip");
            Transform mainPivot = Reference<Transform>(serializedPresenter, "_mainHandWeaponPivot");

            // A weapon-driven pose lives on the Animator root beside the hands, never inside a hand.
            Animator animator = basePrefab.GetComponentInChildren<Animator>(true);
            Assert.That(weaponPose.name, Is.EqualTo("WeaponPose"));
            Assert.That(weaponPose.parent, Is.SameAs(animator.transform));
            Assert.That(weaponPose.IsChildOf(mainGrip.parent), Is.False);
            Assert.That(weaponPose.IsChildOf(offGrip.parent), Is.False);
            Assert.That(weaponPose.childCount, Is.Zero);
            Assert.That(weaponPose.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(weaponPose.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(weaponPose.localScale, Is.EqualTo(Vector3.one));

            // A weapon-driven weapon is held by the left hand, whose renderer draws over the front weapon (20)
            // and under the main hand.
            SpriteRenderer weaponPoseHand = Reference<SpriteRenderer>(serializedPresenter, "_weaponPoseHandRenderer");
            Assert.That(weaponPoseHand.transform, Is.SameAs(offGrip.parent));
            Assert.That(PlayerWeaponPresenter.WeaponPoseHandSortingOrderFront, Is.GreaterThan(21));
            Assert.That(PlayerWeaponPresenter.WeaponPoseHandSortingOrderFront + 1,
                Is.LessThan(mainGrip.parent.GetComponent<SpriteRenderer>().sortingOrder));

            // One weapon visual, parked on the main hand until a weapon-driven definition moves it.
            Assert.That(mainPivot.parent, Is.SameAs(mainGrip));
            Assert.That(basePrefab.GetComponentsInChildren<Transform>(true)
                .Count(transform => transform.name == "WeaponSprite"), Is.EqualTo(1));
        }

        [Test]
        public void CharacterController_UsesSemanticAttackParameters()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null);

            AssertParameter(controller, "OnAttack", AnimatorControllerParameterType.Trigger);
            AssertParameter(controller, "HasGenericAttack", AnimatorControllerParameterType.Bool);
            Assert.That(controller.parameters.Any(parameter => parameter.name == "WeaponAnimationCategory"), Is.False);
            Assert.That(controller.parameters.Any(parameter => parameter.name == "IsLMBPressed"), Is.False);
            Assert.That(controller.parameters.Any(parameter => parameter.name == "IsRMBPressed"), Is.False);

            AnimatorControllerLayer attackLayer =
                controller.layers.Single(layer => layer.name == "RightHand");
            AnimatorState[] attackStates = attackLayer.stateMachine.states
                .Select(child => child.state)
                .Where(state => state.tag == "Attack")
                .ToArray();
            Assert.That(attackStates.Select(state => state.name),
                Is.EquivalentTo(new[] { "Attack" }));
            Assert.That(attackLayer.stateMachine.anyStateTransitions.Select(transition => transition.destinationState),
                Is.EquivalentTo(attackStates));
            Assert.That(attackLayer.stateMachine.states.Any(child =>
                child.state.name.StartsWith("MagicWand-")), Is.False);
            Assert.That(attackStates.All(state => state.motion is BlendTree), Is.True);
            Assert.That(controller.layers.Any(layer => layer.name == "Off Hand Defense"), Is.False);
        }

        [Test]
        public void ImportedAttacks_AreOneShotAndAnimateTheRightHand()
        {
            foreach (string path in AttackClipPaths)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Assert.That(clip, Is.Not.Null, path);
                Assert.That(clip.isLooping, Is.False, path);

                EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
                Assert.That(bindings, Is.Not.Empty, path);
                Assert.That(bindings.All(binding => binding.path == "RightHandPivot/RightHand"), Is.True, path);
                Assert.That(bindings.Any(binding => binding.type == typeof(Transform)), Is.True, path);
            }
        }

        [Test]
        public void ArmingSword_UsesGenericAttackSetWithoutNetworkState()
        {
            LootDefinition definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(TrainingSwordPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.WeaponDefinition, Is.Not.Null);
            Assert.That(definition.WeaponDefinition.Presentation.HasGenericAttack, Is.True);
            Assert.That(definition.WeaponDefinition.Presentation.AttackAnimationSet, Is.Not.Null);

            const BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo field in typeof(WeaponDefinition.PresentationConfig).GetFields(flags))
            {
                Assert.That(field.GetCustomAttribute<Fusion.NetworkedAttribute>(), Is.Null, field.Name);
            }
        }

        [Test]
        public void WeaponPresenter_HasNoProceduralAttackState()
        {
            string[] obsoleteFields =
            {
                "_swingTimer",
                "_isSwinging",
                "_swingFacingDirection",
                "_weaponPivot",
                "_directionPresets",
                "_combatController",
                "_movementStateSource"
            };

            const BindingFlags flags = BindingFlags.Instance |
                BindingFlags.Public | BindingFlags.NonPublic;
            foreach (string fieldName in obsoleteFields)
            {
                Assert.That(typeof(PlayerWeaponPresenter).GetField(fieldName, flags), Is.Null, fieldName);
            }
        }

        [Test]
        public void PlayerPresenters_PreSpawnRefreshDoesNotReadNetworkedEquipment()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BasePlayerPrefabPath);
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            try
            {
                PlayerWeaponPresenter weaponPresenter = instance.GetComponentInChildren<PlayerWeaponPresenter>(true);
                PlayerAnimatorView animatorView = instance.GetComponentInChildren<PlayerAnimatorView>(true);
                MethodInfo weaponLateUpdate = typeof(PlayerWeaponPresenter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo animatorLateUpdate = typeof(PlayerAnimatorView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(() => weaponLateUpdate.Invoke(weaponPresenter, null), Throws.Nothing);
                Assert.That(() => animatorLateUpdate.Invoke(animatorView, null), Throws.Nothing);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static T Reference<T>(SerializedObject serializedObject, string propertyName)
            where T : Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            T value = property.objectReferenceValue as T;
            Assert.That(value, Is.Not.Null, propertyName);
            return value;
        }

        private static void AssertParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            AnimatorControllerParameter parameter =
                controller.parameters.SingleOrDefault(candidate => candidate.name == name);
            Assert.That(parameter, Is.Not.Null, name);
            Assert.That(parameter.type, Is.EqualTo(type), name);
        }

        private static void AssertNoPresenterOverrides(GameObject variantPrefab)
        {
            PropertyModification[] modifications = PrefabUtility.GetPropertyModifications(variantPrefab);
            foreach (PropertyModification modification in modifications)
            {
                if (modification.target is PlayerWeaponPresenter)
                {
                    Assert.Fail($"{variantPrefab.name} overrides '{modification.propertyPath}'.");
                }
            }
        }

        private static void AssertVector(Vector2 actual, Vector2 expected, float tolerance = Tolerance)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(tolerance));
        }
    }
}
