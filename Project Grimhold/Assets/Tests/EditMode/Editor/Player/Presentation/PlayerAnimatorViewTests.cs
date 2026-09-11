using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class PlayerAnimatorViewTests
{
    private const string NetworkPlayerPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string SocialPlayerPath = "Assets/Prefabs/SocialPlayer.prefab";
    private const string AnimatorControllerPath = "Assets/Animations/Player/Character.controller";
    private const string PlaybackParameterName = "LocomotionPlaybackRate";
    private static readonly Vector2[] DirectionPositions =
    {
        new(0f, 1f),
        new(0.707f, 0.707f),
        new(0.707f, -0.707f),
        new(0f, -1f),
        new(-0.707f, -0.707f),
        new(-0.707f, 0.707f),
    };

    private static readonly MethodInfo CalculatePlaybackRateMethod =
        typeof(PlayerAnimatorView).GetMethod(
            "CalculateLocomotionPlaybackRate",
            BindingFlags.Static | BindingFlags.NonPublic);

    private static readonly MethodInfo SamplePlaybackRateMethod =
        typeof(PlayerAnimatorView).GetMethod(
            "SampleLocomotionPlaybackRate",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MethodInfo OnDisableMethod =
        typeof(PlayerAnimatorView).GetMethod(
            "OnDisable",
            BindingFlags.Instance | BindingFlags.NonPublic);

    [Test]
    public void CalculatePlaybackRate_ZeroMovement_ReturnsZero()
    {
        Assert.That(
            CalculatePlaybackRate(Vector2.zero, Vector2.zero, 0.25f, 4f),
            Is.EqualTo(0f));
    }

    [Test]
    public void CalculatePlaybackRate_ReferenceSpeed_ReturnsOne()
    {
        Assert.That(
            CalculatePlaybackRate(Vector2.zero, Vector2.right, 0.25f, 4f),
            Is.EqualTo(1f));
    }

    [Test]
    public void CalculatePlaybackRate_AboveReferenceSpeed_ReturnsProportion()
    {
        Assert.That(
            CalculatePlaybackRate(Vector2.zero, Vector2.right * 2f, 0.25f, 4f),
            Is.EqualTo(2f));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void CalculatePlaybackRate_InvalidReference_ReturnsOne(float referenceMovementSpeed)
    {
        Assert.That(
            CalculatePlaybackRate(
                Vector2.zero,
                Vector2.right,
                0.25f,
                referenceMovementSpeed),
            Is.EqualTo(1f));
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void CalculatePlaybackRate_InvalidDeltaTime_ReturnsOne(float deltaTime)
    {
        Assert.That(
            CalculatePlaybackRate(Vector2.zero, Vector2.right, deltaTime, 4f),
            Is.EqualTo(1f));
    }

    [Test]
    public void CalculatePlaybackRate_NonFinitePosition_ReturnsOne()
    {
        Assert.That(
            CalculatePlaybackRate(
                Vector2.zero,
                new Vector2(float.NaN, 0f),
                0.25f,
                4f),
            Is.EqualTo(1f));

        Assert.That(
            CalculatePlaybackRate(
                new Vector2(float.PositiveInfinity, 0f),
                Vector2.zero,
                0.25f,
                4f),
            Is.EqualTo(1f));
    }

    [Test]
    public void CalculatePlaybackRate_NonFiniteDistanceOrSpeed_ReturnsOne()
    {
        Assert.That(
            CalculatePlaybackRate(
                new Vector2(float.MaxValue, 0f),
                new Vector2(-float.MaxValue, 0f),
                0.25f,
                4f),
            Is.EqualTo(1f));

        Assert.That(
            CalculatePlaybackRate(
                Vector2.zero,
                new Vector2(float.MaxValue, 0f),
                float.Epsilon,
                4f),
            Is.EqualTo(1f));
    }

    [Test]
    public void SamplePlaybackRate_FirstSampleAndReactivation_ReturnOneWithoutSpike()
    {
        var gameObject = new GameObject("PlayerAnimatorViewTests");
        PlayerAnimatorView animatorView = gameObject.AddComponent<PlayerAnimatorView>();

        try
        {
            Assert.That(
                SamplePlaybackRate(animatorView, new Vector2(100f, 100f), 0.01f),
                Is.EqualTo(1f));

            Assert.That(
                SamplePlaybackRate(animatorView, new Vector2(101f, 100f), 0.25f),
                Is.EqualTo(1f));

            Assert.That(OnDisableMethod, Is.Not.Null);
            OnDisableMethod.Invoke(animatorView, null);

            Assert.That(
                SamplePlaybackRate(animatorView, new Vector2(500f, 500f), 0.01f),
                Is.EqualTo(1f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [TestCase(NetworkPlayerPath)]
    [TestCase(SocialPlayerPath)]
    public void PlayerPrefab_HasValidReferenceMovementSpeed(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.That(prefab, Is.Not.Null);

        PlayerAnimatorView animatorView =
            prefab.GetComponentInChildren<PlayerAnimatorView>(true);
        Assert.That(animatorView, Is.Not.Null);

        SerializedProperty referenceMovementSpeed =
            new SerializedObject(animatorView)
                .FindProperty("_referenceMovementSpeed");

        Assert.That(referenceMovementSpeed, Is.Not.Null);
        Assert.That(float.IsNaN(referenceMovementSpeed.floatValue), Is.False);
        Assert.That(float.IsInfinity(referenceMovementSpeed.floatValue), Is.False);
        Assert.That(referenceMovementSpeed.floatValue, Is.GreaterThan(0f));
    }

    [Test]
    public void CharacterController_UsesPlaybackRateOnlyForMovement()
    {
        AnimatorController controller =
            AssetDatabase.LoadAssetAtPath<AnimatorController>(
                AnimatorControllerPath);
        Assert.That(controller, Is.Not.Null);

        AnimatorControllerParameter parameter = controller.parameters.SingleOrDefault(
            candidate => candidate.name == PlaybackParameterName);

        Assert.That(parameter, Is.Not.Null);
        Assert.That(parameter.type, Is.EqualTo(AnimatorControllerParameterType.Float));
        Assert.That(parameter.defaultFloat, Is.EqualTo(1f));

        AnimatorState[] states = controller.layers
            .SelectMany(layer => layer.stateMachine.states)
            .Select(childState => childState.state)
            .ToArray();

        string[] movementStateNames = { "Movement", "RightHand-Movement", "LeftHand-Movement" };
        foreach (string stateName in movementStateNames)
        {
            AnimatorState movementState = states.Single(state => state.name == stateName);
            Assert.That(movementState.speedParameterActive, Is.True);
            Assert.That(movementState.speedParameter, Is.EqualTo(PlaybackParameterName));
        }

        foreach (AnimatorState state in states.Where(state => !movementStateNames.Contains(state.name)))
        {
            Assert.That(
                state.speedParameterActive &&
                state.speedParameter == PlaybackParameterName,
                Is.False,
                $"{state.name} must not use {PlaybackParameterName} as Speed Multiplier.");
        }
    }

    [Test]
    public void CharacterController_HasIndependentBodyAndHandLayers()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        Assert.That(controller, Is.Not.Null);
        Assert.That(
            controller.layers.Select(layer => layer.name),
            Is.EqualTo(new[] { "Base Layer", "RightHand", "LeftHand" }));

        AssertDirectionalTree(FindState(controller.layers[1], "RightHand-Idle").motion, "RightHand-Idle");
        AssertDirectionalTree(FindState(controller.layers[1], "RightHand-Movement").motion, "RightHand-Walk");
        AssertDirectionalTree(FindState(controller.layers[2], "LeftHand-Idle").motion, "LeftHand-Idle");
        AssertDirectionalTree(FindState(controller.layers[2], "LeftHand-Movement").motion, "LeftHand-Walk");
    }

    [Test]
    public void BodyLocomotionClips_AnimateTheNestedMainHandGrip()
    {
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };
        string[] states = { "Idle", "Walk" };

        foreach (string state in states)
        {
            foreach (string direction in directions)
            {
                string path = $"Assets/Animations/Player/{state}/{state}_{direction}.anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                Assert.That(clip, Is.Not.Null, path);

                EditorCurveBinding[] positionBindings = AnimationUtility.GetCurveBindings(clip)
                    .Where(binding =>
                        binding.type == typeof(Transform) &&
                        binding.propertyName.StartsWith("m_LocalPosition", StringComparison.Ordinal))
                    .ToArray();

                Assert.That(positionBindings, Is.Not.Empty, path);
                Assert.That(
                    positionBindings.All(binding =>
                        binding.path == "RightHandPivot/RightHand/MainHandGrip"),
                    Is.True,
                    $"{path} must keep the weapon grip aligned with the authored hand pose.");
            }
        }
    }

    [Test]
    public void CharacterController_EachWeaponAttackUsesSixDirectionalOneShotClips()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        AnimatorControllerLayer rightHandLayer = controller.layers.Single(layer => layer.name == "RightHand");
        string[] weapons = { "ArmingSword", "Rapier", "RondelDagger", "MagicWand" };

        foreach (string weapon in weapons)
        {
            AnimatorState state = FindState(rightHandLayer, $"{weapon}-Attack");
            AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Weapons/{weapon}_Attack.anim");
            Assert.That(state.tag, Is.EqualTo("Attack"));
            AssertDirectionalTree(state.motion, $"{weapon}-Attack-Directional");

            BlendTree tree = (BlendTree)state.motion;
            foreach (ChildMotion child in tree.children)
            {
                AnimationClip clip = child.motion as AnimationClip;
                Assert.That(clip, Is.Not.Null);
                Assert.That(clip.isLooping, Is.False, $"{clip.name} must remain one-shot.");
                Assert.That(
                    AnimationUtility.GetCurveBindings(clip).All(binding => binding.path == "RightHandPivot/RightHand"),
                    Is.True,
                    $"{clip.name} must target the restored authored hierarchy.");
                AssertCurveEndpointsEqual(source, clip);
            }
        }
    }

    [Test]
    public void DirectionalSouthClips_PreserveJuanAuthoredCurves()
    {
        string[] weapons = { "ArmingSword", "Rapier", "RondelDagger", "MagicWand" };
        foreach (string weapon in weapons)
        {
            AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Weapons/{weapon}_Attack.anim");
            AnimationClip south = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Weapons/Directional/{weapon}/{weapon}_Attack_S.anim");
            Assert.That(source, Is.Not.Null);
            Assert.That(south, Is.Not.Null);
            AssertCurvesEqual(source, south);
        }
    }

    [Test]
    public void DirectionalAttackRotations_PreserveTheSourceSwingAcrossAllFacings()
    {
        string[] weapons = { "ArmingSword", "Rapier", "RondelDagger", "MagicWand" };
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };

        foreach (string weapon in weapons)
        {
            AnimationClip source = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Weapons/{weapon}_Attack.anim");
            EditorCurveBinding sourceRotationBinding = AnimationUtility.GetCurveBindings(source)
                .Single(binding => binding.propertyName == "localEulerAnglesRaw.z");
            Keyframe[] sourceKeys = AnimationUtility.GetEditorCurve(source, sourceRotationBinding).keys;

            foreach (string direction in directions)
            {
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    $"Assets/Animations/Weapons/Directional/{weapon}/{weapon}_Attack_{direction}.anim");
                EditorCurveBinding rotationBinding = AnimationUtility.GetCurveBindings(clip)
                    .Single(binding => binding.propertyName == "localEulerAnglesRaw.z");
                Keyframe[] keys = AnimationUtility.GetEditorCurve(clip, rotationBinding).keys;
                Assert.That(keys, Has.Length.EqualTo(sourceKeys.Length), clip.name);
                for (int index = 0; index < keys.Length; index++)
                {
                    Assert.That(keys[index].time, Is.EqualTo(sourceKeys[index].time), clip.name);
                    Assert.That(
                        keys[index].value,
                        Is.EqualTo(sourceKeys[index].value).Within(0.0001f),
                        $"{clip.name} rotation key {index}");
                }
            }
        }
    }

    [Test]
    public void DirectionalAttackClips_KeepTheDirectionalIdleGripPose()
    {
        string[] weapons = { "ArmingSword", "Rapier", "RondelDagger", "MagicWand" };
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };
        const string gripPath = "RightHandPivot/RightHand/MainHandGrip";
        string[] positionProperties =
        {
            "m_LocalPosition.x",
            "m_LocalPosition.y",
            "m_LocalPosition.z"
        };

        foreach (string direction in directions)
        {
            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Player/Idle/Idle_{direction}.anim");

            foreach (string property in positionProperties)
            {
                EditorCurveBinding idleBinding = AnimationUtility.GetCurveBindings(idle)
                    .Single(binding => binding.path == gripPath && binding.propertyName == property);
                float expected = AnimationUtility.GetEditorCurve(idle, idleBinding).Evaluate(0f);

                foreach (string weapon in weapons)
                {
                    AnimationClip attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                        $"Assets/Animations/Weapons/Directional/{weapon}/{weapon}_Attack_{direction}.anim");
                    EditorCurveBinding attackBinding = AnimationUtility.GetCurveBindings(attack)
                        .Single(binding => binding.path == gripPath && binding.propertyName == property);
                    AnimationCurve curve = AnimationUtility.GetEditorCurve(attack, attackBinding);

                    Assert.That(curve.Evaluate(0f), Is.EqualTo(expected).Within(0.0001f), attack.name);
                    Assert.That(curve.Evaluate(attack.length), Is.EqualTo(expected).Within(0.0001f), attack.name);
                }
            }
        }
    }

    [Test]
    public void DirectionalAttackClips_KeepTheDirectionalRightHandSprite()
    {
        string[] weapons = { "ArmingSword", "Rapier", "RondelDagger", "MagicWand" };
        string[] directions = { "N", "NE", "NW", "S", "SE", "SW" };
        const string handPath = "RightHandPivot/RightHand";

        foreach (string direction in directions)
        {
            AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim");
            EditorCurveBinding idleBinding = AnimationUtility.GetObjectReferenceCurveBindings(idle)
                .Single(binding => binding.path == handPath && binding.propertyName == "m_Sprite");
            UnityEngine.Object expected =
                AnimationUtility.GetObjectReferenceCurve(idle, idleBinding)[0].value;

            foreach (string weapon in weapons)
            {
                AnimationClip attack = AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    $"Assets/Animations/Weapons/Directional/{weapon}/{weapon}_Attack_{direction}.anim");
                EditorCurveBinding attackBinding =
                    AnimationUtility.GetObjectReferenceCurveBindings(attack)
                        .Single(binding => binding.path == handPath && binding.propertyName == "m_Sprite");
                ObjectReferenceKeyframe[] keys =
                    AnimationUtility.GetObjectReferenceCurve(attack, attackBinding);

                Assert.That(keys, Has.Length.EqualTo(1), attack.name);
                Assert.That(keys[0].time, Is.Zero, attack.name);
                Assert.That(keys[0].value, Is.SameAs(expected), attack.name);
            }
        }
    }

    [Test]
    public void NetworkPlayer_RestoresHandPivotsAndExplicitMovementSource()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPath);
        PlayerAnimatorView animatorView = prefab.GetComponentInChildren<PlayerAnimatorView>(true);
        PlayerMovementNetworkController movement = prefab.GetComponent<PlayerMovementNetworkController>();
        Transform visualRoot = animatorView.transform;

        Assert.That(visualRoot.Find("RightHandPivot/RightHand/MainHandGrip"), Is.Not.Null);
        Assert.That(visualRoot.Find("LeftHandPivot/LeftHand/OffHandGrip"), Is.Not.Null);

        SerializedProperty source = new SerializedObject(animatorView).FindProperty("_movementControllerSource");
        Assert.That(source.objectReferenceValue, Is.SameAs(movement));
    }

    [Test]
    public void AttackFacing_PreservesPlayerLocomotion()
    {
        PropertyInfo property = typeof(PlayerAnimatorView).GetProperty(
            "StopsLocomotionDuringTemporalFacing",
            BindingFlags.Instance | BindingFlags.NonPublic);
        GameObject gameObject = new GameObject(nameof(AttackFacing_PreservesPlayerLocomotion));
        PlayerAnimatorView animatorView = gameObject.AddComponent<PlayerAnimatorView>();

        try
        {
            Assert.That(property, Is.Not.Null);
            Assert.That(property.GetValue(animatorView), Is.False);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(gameObject);
        }
    }

    [Test]
    public void CharacterController_RuntimeDrivesIdleAndMovementAwayFromPrefabPose()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPath);
        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;

        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            SpriteRenderer body = animator.transform.Find("Body").GetComponent<SpriteRenderer>();
            Sprite prefabPose = body.sprite;

            animator.Rebind();
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", -1f);
            animator.SetBool("IsMoving", false);
            animator.Update(0.1f);

            Assert.That(
                animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),
                Is.True);
            Assert.That(body.sprite, Is.Not.Null);
            Assert.That(body.sprite, Is.Not.SameAs(prefabPose));

            animator.SetBool("IsMoving", true);
            animator.Update(0.1f);
            animator.Update(0.1f);

            Assert.That(
                animator.GetCurrentAnimatorStateInfo(0).IsName("Movement"),
                Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static float CalculatePlaybackRate(
        Vector2 previousPosition,
        Vector2 currentPosition,
        float deltaTime,
        float referenceMovementSpeed)
    {
        Assert.That(CalculatePlaybackRateMethod, Is.Not.Null);

        return (float)CalculatePlaybackRateMethod.Invoke(
            null,
            new object[]
            {
                previousPosition,
                currentPosition,
                deltaTime,
                referenceMovementSpeed
            });
    }

    private static AnimatorState FindState(AnimatorControllerLayer layer, string stateName)
    {
        AnimatorState state = layer.stateMachine.states
            .Select(child => child.state)
            .SingleOrDefault(candidate => candidate.name == stateName);
        Assert.That(state, Is.Not.Null, $"Missing state '{stateName}' on layer '{layer.name}'.");
        return state;
    }

    private static void AssertDirectionalTree(Motion motion, string expectedName)
    {
        BlendTree tree = motion as BlendTree;
        Assert.That(tree, Is.Not.Null);
        Assert.That(tree.name, Is.EqualTo(expectedName));
        Assert.That(tree.blendType, Is.EqualTo(BlendTreeType.FreeformDirectional2D));
        Assert.That(tree.blendParameter, Is.EqualTo("MoveX"));
        Assert.That(tree.blendParameterY, Is.EqualTo("MoveY"));
        Assert.That(tree.children.Length, Is.EqualTo(6));
        for (int index = 0; index < DirectionPositions.Length; index++)
        {
            Assert.That(tree.children[index].position.x, Is.EqualTo(DirectionPositions[index].x).Within(0.001f));
            Assert.That(tree.children[index].position.y, Is.EqualTo(DirectionPositions[index].y).Within(0.001f));
        }
    }

    private static void AssertCurvesEqual(AnimationClip expected, AnimationClip actual)
    {
        EditorCurveBinding[] expectedBindings = AnimationUtility.GetCurveBindings(expected);
        EditorCurveBinding[] actualBindings = AnimationUtility.GetCurveBindings(actual);
        Assert.That(actualBindings, Is.EqualTo(expectedBindings));

        foreach (EditorCurveBinding binding in expectedBindings)
        {
            Keyframe[] expectedKeys = AnimationUtility.GetEditorCurve(expected, binding).keys;
            Keyframe[] actualKeys = AnimationUtility.GetEditorCurve(actual, binding).keys;
            Assert.That(actualKeys.Length, Is.EqualTo(expectedKeys.Length), binding.propertyName);
            for (int index = 0; index < expectedKeys.Length; index++)
            {
                Assert.That(actualKeys[index].time, Is.EqualTo(expectedKeys[index].time), binding.propertyName);
                Assert.That(actualKeys[index].value, Is.EqualTo(expectedKeys[index].value), binding.propertyName);
                Assert.That(actualKeys[index].inTangent, Is.EqualTo(expectedKeys[index].inTangent), binding.propertyName);
                Assert.That(actualKeys[index].outTangent, Is.EqualTo(expectedKeys[index].outTangent), binding.propertyName);
            }
        }
    }

    private static void AssertCurveEndpointsEqual(AnimationClip expected, AnimationClip actual)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(expected))
        {
            Keyframe[] expectedKeys = AnimationUtility.GetEditorCurve(expected, binding).keys;
            Keyframe[] actualKeys = AnimationUtility.GetEditorCurve(actual, binding).keys;
            Assert.That(actualKeys[0].value, Is.EqualTo(expectedKeys[0].value).Within(0.0001f), $"{actual.name} start {binding.propertyName}");
            Assert.That(actualKeys[actualKeys.Length - 1].value, Is.EqualTo(expectedKeys[expectedKeys.Length - 1].value).Within(0.0001f), $"{actual.name} end {binding.propertyName}");
        }
    }

    private static float SamplePlaybackRate(
        PlayerAnimatorView animatorView,
        Vector2 currentPosition,
        float deltaTime)
    {
        Assert.That(SamplePlaybackRateMethod, Is.Not.Null);

        return (float)SamplePlaybackRateMethod.Invoke(
            animatorView,
            new object[] { currentPosition, deltaTime });
    }
}
