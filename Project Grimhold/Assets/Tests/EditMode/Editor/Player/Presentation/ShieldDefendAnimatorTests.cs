using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ShieldDefendAnimatorTests
{
    private const string ControllerPath = "Assets/Animations/Player/Character.controller";
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string ShieldLootPath = "Assets/Scriptable Objects/Loot/Definitions/Shield.asset";
    private const string SpriteSetPath = "Assets/Scriptable Objects/Loot/ShieldSpriteSets/Shield.asset";
    private const string OffHand = "LeftHandPivot/LeftHand";
    private const string IsDefending = "IsDefending";

    // The facing buckets as the directional blend trees place them.
    private static IEnumerable<TestCaseData> Facings()
    {
        yield return new TestCaseData("N", new Vector2(0f, 1f));
        yield return new TestCaseData("NE", new Vector2(0.71f, 0.71f));
        yield return new TestCaseData("NW", new Vector2(-0.71f, 0.71f));
        yield return new TestCaseData("S", new Vector2(0f, -1f));
        yield return new TestCaseData("SE", new Vector2(0.71f, -0.71f));
        yield return new TestCaseData("SW", new Vector2(-0.71f, -0.71f));
    }

    private Scene _scene;
    private GameObject _player;

    [TearDown]
    public void TearDown()
    {
        if (_player != null) UnityEngine.Object.DestroyImmediate(_player);
        if (_scene.IsValid()) EditorSceneManager.ClosePreviewScene(_scene);
    }

    [Test]
    public void Controller_DeclaresTheDefenseParameterAsABool()
    {
        AnimatorControllerParameter parameter = Controller().parameters.Single(candidate => candidate.name == IsDefending);

        Assert.That(parameter.type, Is.EqualTo(AnimatorControllerParameterType.Bool));
        Assert.That(parameter.defaultBool, Is.False);
    }

    [Test]
    public void LeftHandLayer_BlendsTheSixDefendClipsAtTheLocomotionFacings()
    {
        AnimatorStateMachine machine = Layer("LeftHand").stateMachine;
        var defend = (BlendTree)State(machine, "Defend").motion;
        var idle = (BlendTree)State(machine, "LeftHand-Idle").motion;

        Assert.That(defend.blendType, Is.EqualTo(BlendTreeType.FreeformDirectional2D));
        Assert.That(defend.blendParameter, Is.EqualTo("MoveX"));
        Assert.That(defend.blendParameterY, Is.EqualTo("MoveY"));
        Assert.That(defend.children, Has.Length.EqualTo(6));
        foreach (ChildMotion locomotion in idle.children)
        {
            string direction = locomotion.motion.name.Substring(locomotion.motion.name.LastIndexOf('_') + 1);
            ChildMotion child = defend.children.Single(candidate => candidate.position == locomotion.position);
            Assert.That(child.motion, Is.SameAs(AssetDatabase.LoadAssetAtPath<AnimationClip>(
                $"Assets/Animations/Weapons/Directional/Shield/Shield_Defend_{direction}.anim")), direction);
        }
    }

    [Test]
    public void LeftHandLayer_EntersAndLeavesDefendInstantlyOnTheDefenseParameter()
    {
        AnimatorStateMachine machine = Layer("LeftHand").stateMachine;
        AnimatorState defend = State(machine, "Defend");
        AnimatorState idle = State(machine, "LeftHand-Idle");
        AnimatorState movement = State(machine, "LeftHand-Movement");

        foreach (AnimatorState locomotion in new[] { idle, movement })
        {
            // Listed first, so defense wins over a locomotion change in the same update.
            AnimatorStateTransition enter = locomotion.transitions[0];
            Assert.That(enter.destinationState, Is.SameAs(defend), locomotion.name);
            AssertInstant(enter);
            AssertConditions(enter, (IsDefending, AnimatorConditionMode.If));
        }

        AnimatorStateTransition[] exits = defend.transitions;
        Assert.That(exits, Has.Length.EqualTo(2));
        AnimatorStateTransition toIdle = exits.Single(transition => transition.destinationState == idle);
        AnimatorStateTransition toMovement = exits.Single(transition => transition.destinationState == movement);
        AssertInstant(toIdle);
        AssertInstant(toMovement);
        AssertConditions(toIdle, (IsDefending, AnimatorConditionMode.IfNot), ("IsMoving", AnimatorConditionMode.IfNot));
        AssertConditions(toMovement, (IsDefending, AnimatorConditionMode.IfNot), ("IsMoving", AnimatorConditionMode.If));
    }

    [Test]
    public void BaseAndMainHandLayers_DoNotObserveDefense()
    {
        foreach (string layerName in new[] { "Base Layer", "RightHand" })
        {
            AnimatorStateMachine machine = Layer(layerName).stateMachine;
            IEnumerable<AnimatorStateTransition> transitions = machine.anyStateTransitions
                .Concat(machine.states.SelectMany(child => child.state.transitions));
            Assert.That(transitions.SelectMany(transition => transition.conditions)
                .Any(condition => condition.parameter == IsDefending), Is.False, layerName);
            Assert.That(machine.states.Any(child => child.state.name == "Defend"), Is.False, layerName);
        }
    }

    [TestCaseSource(nameof(Facings))]
    public void Runtime_DefenseHoldsTheFacingsClipWhileWalkingAndReturnsToLocomotion(string direction, Vector2 facing)
    {
        Animator animator = SpawnAnimator();
        int leftHand = animator.GetLayerIndex("LeftHand");
        int rightHand = animator.GetLayerIndex("RightHand");
        animator.SetFloat("MoveX", facing.x);
        animator.SetFloat("MoveY", facing.y);
        animator.Update(0.1f);
        Transform grip = animator.transform.Find(OffHand + "/OffHandGrip");
        Vector3 idleGrip = grip.localPosition;

        animator.SetBool(IsDefending, true);
        animator.Update(0.02f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(leftHand).IsName("Defend"), Is.True);
        Assert.That(DominantClip(animator, leftHand), Is.EqualTo($"Shield_Defend_{direction}"));
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            $"Assets/Animations/Weapons/Directional/Shield/Shield_Defend_{direction}.anim");
        foreach (string property in new[] { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z" })
        {
            AnimationCurve curve = AnimationUtility.GetEditorCurve(clip,
                EditorCurveBinding.FloatCurve(OffHand + "/OffHandGrip", typeof(Transform), property));
            float actual = property.EndsWith(".x", StringComparison.Ordinal) ? grip.localPosition.x :
                property.EndsWith(".y", StringComparison.Ordinal) ? grip.localPosition.y : grip.localPosition.z;
            float before = property.EndsWith(".x", StringComparison.Ordinal) ? idleGrip.x :
                property.EndsWith(".y", StringComparison.Ordinal) ? idleGrip.y : idleGrip.z;
            Assert.That(curve, Is.Not.Null, property);
            Assert.That(actual, Is.EqualTo(curve.Evaluate(0f)).Within(0.001f), property);
            Assert.That(actual, Is.EqualTo(before).Within(0.001f), $"Idle to Defend {property}");
        }

        // Past the clip, the pose holds its final authored key.
        animator.Update(1f);
        Transform hand = animator.transform.Find(OffHand);
        Assert.That(hand.localPosition.x, Is.EqualTo(EndValue(clip, "m_LocalPosition.x")).Within(0.001f));
        Assert.That(hand.localPosition.y, Is.EqualTo(EndValue(clip, "m_LocalPosition.y")).Within(0.001f));

        animator.SetBool("IsMoving", true);
        animator.Update(0.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(leftHand).IsName("Defend"), Is.True);
        Assert.That(animator.GetCurrentAnimatorStateInfo(rightHand).IsName("RightHand-Movement"), Is.True);

        animator.SetBool(IsDefending, false);
        animator.Update(0.02f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(leftHand).IsName("LeftHand-Movement"), Is.True);

        animator.SetBool(IsDefending, true);
        animator.Update(0.02f);
        animator.SetBool("IsMoving", false);
        animator.SetBool(IsDefending, false);
        animator.Update(0.02f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(leftHand).IsName("LeftHand-Idle"), Is.True);
    }

    [Test]
    public void Runtime_DefenseDoesNotMoveTheMainHand()
    {
        Animator animator = SpawnAnimator();
        animator.SetFloat("MoveY", -1f);
        animator.Update(0.3f);
        Transform mainHand = animator.transform.Find("RightHandPivot/RightHand");
        Vector3 position = mainHand.localPosition;
        Quaternion rotation = mainHand.localRotation;

        animator.SetBool(IsDefending, true);
        animator.Update(0.3f);

        Assert.That(mainHand.localPosition, Is.EqualTo(position));
        Assert.That(mainHand.localRotation, Is.EqualTo(rotation));
    }

    [Test]
    public void OffHandSprite_UsesEachFacingSpriteInLocomotionAndDefense()
    {
        var texture = new Texture2D(4, 4);
        Sprite world = Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), Vector2.zero);
        DirectionalShieldSpriteSet set = AssetDatabase.LoadAssetAtPath<DirectionalShieldSpriteSet>(SpriteSetPath);
        try
        {
            foreach (CharacterVisualDirection direction in Enum.GetValues(typeof(CharacterVisualDirection)))
            {
                Assert.That(PlayerWeaponPresentationMath.ResolveOffHandSprite(world, set, direction),
                    Is.SameAs(set.GetSprite(direction)), direction.ToString());
            }

            Assert.That(PlayerWeaponPresentationMath.ResolveOffHandSprite(world, null,
                CharacterVisualDirection.South), Is.SameAs(world));
            Assert.That(PlayerWeaponPresentationMath.ResolveOffHandSprite(null, set,
                CharacterVisualDirection.South), Is.Null);
            DirectionalShieldSpriteSet empty = ScriptableObject.CreateInstance<DirectionalShieldSpriteSet>();
            try
            {
                Assert.That(PlayerWeaponPresentationMath.ResolveOffHandSprite(world, empty,
                    CharacterVisualDirection.South), Is.SameAs(world));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(empty);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(world);
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [TestCase(true, 4, 20)]
    [TestCase(true, 32, 34)]
    [TestCase(false, 4, -10)]
    [TestCase(false, -2, -10)]
    public void OffHandSorting_DrawsTheFrontItemOverItsHandAndKeepsBackItemsBehind(
        bool frontFacing, int handOrder, int expected)
    {
        Assert.That(PlayerWeaponPresentationMath.ResolveOffHandSortingOrder(frontFacing, 20, -10, handOrder),
            Is.EqualTo(expected));
    }

    [Test]
    public void PlayerPrefab_AnimatorReadsDefenseButWeaponPresenterDoesNot()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var defense = prefab.GetComponent<PlayerShieldDefenseNetworkController>();
        Assert.That(defense, Is.Not.Null);

        var view = prefab.GetComponentInChildren<PlayerAnimatorView>(true);
        var presenter = prefab.GetComponentInChildren<PlayerWeaponPresenter>(true);
        Assert.That(new SerializedObject(view).FindProperty("_shieldDefense").objectReferenceValue, Is.SameAs(defense));
        Assert.That(new SerializedObject(presenter).FindProperty("_shieldDefense"), Is.Null);
    }

    [Test]
    public void ShieldLoot_LinksItsDefenseSpritesAsPresentationData()
    {
        LootDefinition shield = AssetDatabase.LoadAssetAtPath<LootDefinition>(ShieldLootPath);

        Assert.That(shield.DefenseSprites,
            Is.SameAs(AssetDatabase.LoadAssetAtPath<DirectionalShieldSpriteSet>(SpriteSetPath)));
        Assert.That(shield.TryValidate(out string error), Is.True, error);
    }

    [Test]
    public void LootValidation_RejectsDefenseSpritesOutsideACompleteShield()
    {
        LootDefinition shield = AssetDatabase.LoadAssetAtPath<LootDefinition>(ShieldLootPath);
        LootDefinition copy = UnityEngine.Object.Instantiate(shield);
        DirectionalShieldSpriteSet incomplete = ScriptableObject.CreateInstance<DirectionalShieldSpriteSet>();
        try
        {
            SetField(copy, "_defenseSprites", incomplete);
            Assert.That(copy.TryValidate(out _), Is.False);

            SetField(copy, "_defenseSprites", shield.DefenseSprites);
            SetField(copy, "_shieldDefinition", null);
            SetField(copy, "_category", LootCategory.Valuable);
            Assert.That(copy.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("defense sprites"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(copy);
            UnityEngine.Object.DestroyImmediate(incomplete);
        }
    }

    private Animator SpawnAnimator()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), _scene);
        Animator animator = _player.GetComponentInChildren<Animator>(true);
        animator.runtimeAnimatorController = Controller();
        animator.Rebind();
        animator.Update(0f);
        return animator;
    }

    private static string DominantClip(Animator animator, int layer)
    {
        var clips = new List<AnimatorClipInfo>();
        animator.GetCurrentAnimatorClipInfo(layer, clips);
        return clips.OrderByDescending(info => info.weight).First().clip.name;
    }

    private static float EndValue(AnimationClip clip, string property) =>
        AnimationUtility.GetEditorCurve(clip, EditorCurveBinding.FloatCurve(OffHand, typeof(Transform), property))
            .Evaluate(clip.length);

    private static void AssertInstant(AnimatorStateTransition transition)
    {
        Assert.That(transition.hasExitTime, Is.False, transition.destinationState.name);
        Assert.That(transition.duration, Is.Zero, transition.destinationState.name);
    }

    private static void AssertConditions(AnimatorStateTransition transition,
        params (string parameter, AnimatorConditionMode mode)[] expected)
    {
        Assert.That(transition.conditions.Select(condition => (condition.parameter, condition.mode)),
            Is.EquivalentTo(expected), transition.destinationState.name);
    }

    private static AnimatorController Controller() =>
        AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

    private static AnimatorControllerLayer Layer(string name) =>
        Controller().layers.Single(layer => layer.name == name);

    private static AnimatorState State(AnimatorStateMachine machine, string name) =>
        machine.states.Single(child => child.state.name == name).state;

    private static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}
