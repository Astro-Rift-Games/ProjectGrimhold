using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Validates the player knockback reaction wiring: the clips, the Animator layers that play them and the
/// presentation trigger that starts them.
/// </summary>
public sealed class PlayerKnockbackAnimationTests
{
    private const string NetworkPlayerPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string KnockbackFolder = "Assets/Animations/Player/Knockback";
    private const string IdleFolder = "Assets/Animations/Player/Idle";
    private const float KnockbackSeconds = 0.3f;

    // Same order and positions as the directional blend trees of Character.controller.
    private static readonly string[] DirectionNames = { "N", "NE", "SE", "S", "SW", "NW" };
    private static readonly Vector2[] DirectionPositions =
    {
        new(0f, 1f),
        new(0.707f, 0.707f),
        new(0.707f, -0.707f),
        new(0f, -1f),
        new(-0.707f, -0.707f),
        new(-0.707f, 0.707f),
    };

    private static readonly MethodInfo InitializeHashesMethod =
        typeof(CharacterAnimatorView).GetMethod("InitializeHashes", BindingFlags.Instance | BindingFlags.NonPublic);

    [Test]
    public void KnockbackTrigger_PlaysBodyAndBothHandsInEveryDirectionThenReturnsToIdle()
    {
        GameObject instance = InstantiatePlayer();

        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            int rightHandLayer = animator.GetLayerIndex("RightHand");
            int leftHandLayer = animator.GetLayerIndex("LeftHand");
            SpriteRenderer body = animator.transform.Find("Body").GetComponent<SpriteRenderer>();
            SpriteRenderer rightHand = animator.transform.Find("RightHandPivot/RightHand").GetComponent<SpriteRenderer>();
            SpriteRenderer leftHand = animator.transform.Find("LeftHandPivot/LeftHand").GetComponent<SpriteRenderer>();

            for (int index = 0; index < DirectionNames.Length; index++)
            {
                string direction = DirectionNames[index];
                animator.Rebind();
                animator.SetFloat("MoveX", DirectionPositions[index].x);
                animator.SetFloat("MoveY", DirectionPositions[index].y);
                animator.SetBool("IsMoving", false);
                animator.Update(0.1f);

                animator.SetTrigger("OnKnockback");
                animator.SetTrigger("OnKnockbackRightHand");
                animator.SetTrigger("OnKnockbackLeftHand");
                animator.Update(0.02f);

                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Knockback"), Is.True, direction);
                Assert.That(animator.GetCurrentAnimatorStateInfo(rightHandLayer).IsName("RightHand-Knockback"), Is.True, direction);
                Assert.That(animator.GetCurrentAnimatorStateInfo(leftHandLayer).IsName("LeftHand-Knockback"), Is.True, direction);
                Assert.That(body.sprite.name, Does.Contain($"Knockback-{direction}-Sheet"), direction);
                Assert.That(rightHand.sprite.name, Does.Contain($"Knockback-{direction}-Sheet"), direction);
                Assert.That(leftHand.sprite.name, Does.Contain($"Knockback-{direction}-Sheet"), direction);

                animator.Update(KnockbackSeconds);
                animator.Update(0.1f);

                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True, direction);
                Assert.That(animator.GetCurrentAnimatorStateInfo(rightHandLayer).IsName("RightHand-Idle"), Is.True, direction);
                Assert.That(animator.GetCurrentAnimatorStateInfo(leftHandLayer).IsName("LeftHand-Idle"), Is.True, direction);
            }
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void KnockbackTrigger_RestartsTheReactionWhenHitAgain()
    {
        GameObject instance = InstantiatePlayer();

        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            animator.Rebind();
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", -1f);
            animator.SetBool("IsMoving", false);
            animator.Update(0.1f);

            animator.SetTrigger("OnKnockback");
            animator.Update(0.02f);
            animator.Update(0.18f);
            float firstHitProgress = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            Assert.That(firstHitProgress, Is.GreaterThan(0.5f));

            animator.SetTrigger("OnKnockback");
            animator.Update(0.02f);
            animator.Update(0.02f);

            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Knockback"), Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.LessThan(firstHitProgress));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void TriggerKnockback_IsIgnoredWhileDefeated()
    {
        GameObject instance = InstantiatePlayer();

        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            PlayerAnimatorView view = instance.GetComponentInChildren<PlayerAnimatorView>(true);
            Assert.That(InitializeHashesMethod, Is.Not.Null);
            InitializeHashesMethod.Invoke(view, null);

            animator.Rebind();
            animator.Update(0.1f);
            view.SetDefeated(true);
            animator.Update(0.1f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Defeated"), Is.True);

            view.TriggerKnockback();
            animator.Update(0.1f);

            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Defeated"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void TriggerKnockback_PlaysThroughTheViewOnTheRealController()
    {
        GameObject instance = InstantiatePlayer();

        try
        {
            Animator animator = instance.GetComponentInChildren<Animator>(true);
            PlayerAnimatorView view = instance.GetComponentInChildren<PlayerAnimatorView>(true);
            InitializeHashesMethod.Invoke(view, null);

            animator.Rebind();
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", -1f);
            animator.Update(0.1f);

            view.TriggerKnockback();
            animator.Update(0.02f);

            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Knockback"), Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(animator.GetLayerIndex("RightHand")).IsName("RightHand-Knockback"), Is.True);
            Assert.That(animator.GetCurrentAnimatorStateInfo(animator.GetLayerIndex("LeftHand")).IsName("LeftHand-Knockback"), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void KnockbackClips_AreFourFramesOfThreeHundredMillisecondsWithResolvedSprites()
    {
        foreach (string direction in DirectionNames)
        {
            AnimationClip body = LoadClip($"{KnockbackFolder}/Knockback_{direction}.anim");
            AnimationClip left = LoadClip($"{KnockbackFolder}/LeftHand/LeftHand_Knockback_{direction}.anim");
            AnimationClip right = LoadClip($"{KnockbackFolder}/RightHand/RightHand_Knockback_{direction}.anim");

            foreach (AnimationClip clip in new[] { body, left, right })
            {
                Assert.That(clip.isLooping, Is.False, clip.name);
                Assert.That(clip.length, Is.EqualTo(KnockbackSeconds).Within(0.0001f), clip.name);
                foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    Assert.That(keys.Length, Is.EqualTo(4), $"{clip.name} {binding.path}");
                    Assert.That(keys.All(key => key.value is Sprite), Is.True, $"{clip.name} {binding.path}");
                }
            }

            Assert.That(
                AnimationUtility.GetObjectReferenceCurveBindings(body).Select(binding => binding.path),
                Is.EquivalentTo(new[] { "Legs", "Body", "Head", "LeftHand", "RightHand" }),
                body.name);
        }
    }

    [Test]
    public void KnockbackBodyClips_StartOnTheIdleGripPoseAndSortingOrder()
    {
        foreach (string direction in DirectionNames)
        {
            AnimationClip idle = LoadClip($"{IdleFolder}/Idle_{direction}.anim");
            AnimationClip knockback = LoadClip($"{KnockbackFolder}/Knockback_{direction}.anim");

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(idle)
                .Where(candidate => candidate.propertyName.StartsWith("m_LocalPosition") ||
                                    candidate.propertyName == "m_SortingOrder"))
            {
                AnimationCurve knockbackCurve = AnimationUtility.GetEditorCurve(knockback, binding);
                Assert.That(knockbackCurve, Is.Not.Null, $"{direction} {binding.path} {binding.propertyName}");
                Assert.That(
                    knockbackCurve.Evaluate(0f),
                    Is.EqualTo(AnimationUtility.GetEditorCurve(idle, binding).Evaluate(0f)).Within(0.0001f),
                    $"{direction} {binding.path} {binding.propertyName}");
            }
        }
    }

    private static GameObject InstantiatePlayer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPlayerPath);
        Assert.That(prefab, Is.Not.Null);
        return PrefabUtility.InstantiatePrefab(prefab) as GameObject;
    }

    private static AnimationClip LoadClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        Assert.That(clip, Is.Not.Null, path);
        return clip;
    }
}
