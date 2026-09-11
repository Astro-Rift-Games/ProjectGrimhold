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
        public void CharacterController_UsesSemanticAttackParameters()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null);

            AssertParameter(controller, "OnAttack", AnimatorControllerParameterType.Trigger);
            AssertParameter(controller, "WeaponAnimationCategory", AnimatorControllerParameterType.Int);
            Assert.That(controller.parameters.Any(parameter => parameter.name == "IsLMBPressed"), Is.False);
            Assert.That(controller.parameters.Any(parameter => parameter.name == "IsRMBPressed"), Is.False);

            AnimatorControllerLayer attackLayer =
                controller.layers.Single(layer => layer.name == "RightHand");
            AnimatorState[] attackStates = attackLayer.stateMachine.states
                .Select(child => child.state)
                .Where(state => state.tag == "Attack")
                .ToArray();
            Assert.That(attackStates, Has.Length.EqualTo(4));
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
        public void ArmingSword_UsesArmingSwordPresentationWithoutNetworkState()
        {
            LootDefinition definition = AssetDatabase.LoadAssetAtPath<LootDefinition>(TrainingSwordPath);
            Assert.That(definition, Is.Not.Null);
            Assert.That(definition.WeaponDefinition, Is.Not.Null);
            Assert.That(
                definition.WeaponDefinition.Presentation.AnimationCategory,
                Is.EqualTo(WeaponAnimationCategory.ArmingSword));

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

        private static void AssertVector(Vector2 actual, Vector2 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Tolerance));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Tolerance));
        }
    }
}
