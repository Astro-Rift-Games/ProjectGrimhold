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

        AnimatorState movementState = states.Single(state => state.name == "Movement");
        Assert.That(movementState.speedParameterActive, Is.True);
        Assert.That(movementState.speedParameter, Is.EqualTo(PlaybackParameterName));

        foreach (AnimatorState state in states.Where(state => state.name != "Movement"))
        {
            Assert.That(
                state.speedParameterActive &&
                state.speedParameter == PlaybackParameterName,
                Is.False,
                $"{state.name} must not use {PlaybackParameterName} as Speed Multiplier.");
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
