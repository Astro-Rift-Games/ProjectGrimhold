using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ArmingSwordSlashPresentationTests
{
    private const string PrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
    private const string SwordPath = "Assets/Scriptable Objects/Loot/Definitions/ArmingSwordWeaponDefinition.asset";
    private GameObject _contents;
    private PlayerAttackVfxPresenter _presenter;
    private SpriteRenderer _renderer;
    private LootDefinitionCatalog _catalog;

    [SetUp]
    public void SetUp()
    {
        _contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        _presenter = _contents.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
        Assert.That(_presenter, Is.Not.Null);
        _renderer = (SpriteRenderer)new SerializedObject(_presenter).FindProperty("_vfxRenderer").objectReferenceValue;
        var equipment = _contents.GetComponent<PlayerWeaponEquipmentNetworkController>();
        _catalog = (LootDefinitionCatalog)new SerializedObject(equipment).FindProperty("_lootCatalog").objectReferenceValue;
        Assert.That(_catalog, Is.Not.Null);
    }

    [TearDown]
    public void TearDown()
    {
        if (_contents != null) PrefabUtility.UnloadPrefabContents(_contents);
    }

    private int IndexPlusOne(string path)
    {
        LootDefinition loot = AssetDatabase.LoadAssetAtPath<LootDefinition>(path);
        Assert.That(loot, Is.Not.Null, path);
        Assert.That(_catalog.TryGetIndex(loot.LootId, out int index), Is.True);
        return index + 1;
    }

    private void Perform(int index, Vector2 direction, AttackType type = AttackType.Melee,
        PlayerAttackVfxPresenter presenter = null)
    {
        var attack = new AttackPerformedEvent(default, type, Vector2.zero, direction, 0, index);
        typeof(PlayerAttackVfxPresenter).GetMethod("OnAttackPerformed", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(presenter != null ? presenter : _presenter, new object[] { attack });
    }

    private static object State(PlayerAttackVfxPresenter presenter, string name) =>
        typeof(PlayerAttackVfxPresenter).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presenter);

    [TestCase(CharacterVisualDirection.North, false)]
    [TestCase(CharacterVisualDirection.NorthEast, false)]
    [TestCase(CharacterVisualDirection.NorthWest, false)]
    [TestCase(CharacterVisualDirection.South, true)]
    [TestCase(CharacterVisualDirection.SouthEast, true)]
    [TestCase(CharacterVisualDirection.SouthWest, true)]
    public void ConfirmedSwordAttack_CapturesConfiguredDirectionalPose(CharacterVisualDirection direction, bool front)
    {
        Perform(IndexPlusOne("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset"),
            CharacterVisualDirectionResolver.GetCanonicalVector(direction));
        Assert.That(State(_presenter, "_pending"), Is.True);
        Assert.That(_renderer.transform.name, Is.EqualTo("AttackVfx"));
        Assert.That(_renderer.transform.parent.name, Is.EqualTo("VisualRoot"));
        Assert.That(_renderer.transform.localPosition,
            Is.EqualTo(front ? new Vector3(0.2f, -0.3f, 0f) : new Vector3(0.2f, 0f, 0f)));
        Assert.That(_renderer.transform.localScale,
            Is.EqualTo(new Vector3(0.65f, front ? 0.65f : -0.65f, 1f)));
        Assert.That(_renderer.sortingOrder, Is.EqualTo(front ? 21 : -9));
        Assert.That(_renderer.enabled, Is.False);
    }

    [Test]
    public void VfxConfiguration_HasExclusiveChildSpriteBindingAndFourOrderedFrames()
    {
        WeaponDefinition sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath);
        AttackVfxDefinition vfx = sword.Presentation.AttackVfx;
        Assert.That(vfx, Is.Not.Null);
        Assert.That(vfx.TryValidate(out string error), Is.True, error);
        Assert.That(vfx.StartSeconds, Is.EqualTo(0.1f));
        Assert.That(vfx.Clip.length, Is.EqualTo(0.4f).Within(0.0001f));
        var bindings = AnimationUtility.GetObjectReferenceCurveBindings(vfx.Clip);
        Assert.That(bindings, Has.Length.EqualTo(1));
        Assert.That(bindings[0].path, Is.EqualTo("AttackVfx"));
        Assert.That(bindings[0].type, Is.EqualTo(typeof(SpriteRenderer)));
        Assert.That(bindings[0].propertyName, Is.EqualTo("m_Sprite"));
        Assert.That(AnimationUtility.GetCurveBindings(vfx.Clip), Is.Empty);
        var frames = AnimationUtility.GetObjectReferenceCurve(vfx.Clip, bindings[0]);
        Assert.That(frames, Has.Length.EqualTo(4));
        for (int i = 0; i < 4; i++)
        {
            Assert.That(frames[i].time, Is.EqualTo(i * 0.1f).Within(0.0001f));
            Assert.That(frames[i].value.name, Is.EqualTo($"VFX-Slash_{i}"));
        }
        for (int i = 0; i < 6; i++)
        {
            var pose = vfx.GetPose(i);
            bool front = i >= 3;
            Assert.That(pose.Position, Is.EqualTo(front ? new Vector3(0.2f, -0.3f, 0) : new Vector3(0.2f, 0, 0)));
            Assert.That(pose.Scale, Is.EqualTo(new Vector3(0.65f, front ? 0.65f : -0.65f, 1)));
            Assert.That(pose.Rotation, Is.EqualTo(Quaternion.identity));
            Assert.That(pose.SortingOrder, Is.EqualTo(front ? 21 : -9));
        }
        WeaponDefinition rapier = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(
            "Assets/Scriptable Objects/Loot/Definitions/RapierWeaponDefinition.asset");
        Assert.That(rapier.Presentation.AttackVfx, Is.Null);
    }

    [Test]
    public void RejectedAttack_ClearsPendingEffect()
    {
        int sword = IndexPlusOne("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
        int rapier = IndexPlusOne("Assets/Scriptable Objects/Loot/Definitions/Rapier.asset");
        foreach (int rejected in new[] { 0, _catalog.DefinitionCount + 1, rapier })
        {
            Perform(sword, Vector2.down);
            Perform(rejected, Vector2.down);
            Assert.That(State(_presenter, "_pending"), Is.False);
            Assert.That(_renderer.sprite, Is.Null);
            Assert.That(_renderer.enabled, Is.False);
        }
        Perform(sword, Vector2.down, AttackType.Ranged);
        Assert.That(State(_presenter, "_pending"), Is.True, "Configured effect accepts confirmed non-melee attack types");
    }

    [Test]
    public void DisablingPresenter_ClearsEffect()
    {
        Perform(IndexPlusOne("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset"), Vector2.up);
        _renderer.enabled = true;
        typeof(PlayerAttackVfxPresenter).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(_presenter, null);
        Assert.That(State(_presenter, "_pending"), Is.False);
        Assert.That(_renderer.enabled, Is.False);
        Assert.That(_renderer.sprite, Is.Null);
    }

    [Test]
    public void SwordAttack_SamplesClipAtMatchingHandPhaseAndClearsAtEnd()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject player = null;
        AnimatorOverrideController overrides = null;
        try
        {
            player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            Animator animator = player.GetComponentInChildren<Animator>(true);
            var presenter = player.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
            SpriteRenderer renderer = (SpriteRenderer)new SerializedObject(presenter).FindProperty("_vfxRenderer").objectReferenceValue;
            var sword = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath);
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/Character.controller");
            var placeholder = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/Attack/GenericAttack_S.anim");
            overrides = new AnimatorOverrideController(controller);
            overrides[placeholder] = sword.Presentation.GetAttackClip(3);
            animator.runtimeAnimatorController = overrides;
            animator.SetFloat("MoveX", 0f);
            animator.SetFloat("MoveY", -1f);
            animator.SetBool("IsMoving", false);
            animator.SetBool("HasGenericAttack", true);
            animator.SetInteger("WeaponAnimationCategory", (int)sword.Presentation.AnimationCategory);
            typeof(PlayerAttackVfxPresenter).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, null);
            animator.SetTrigger("OnAttack");
            Perform(IndexPlusOne("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset"), Vector2.down,
                AttackType.Melee, presenter);
            MethodInfo update = typeof(PlayerAttackVfxPresenter).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            animator.Update(0f);
            int layer = animator.GetLayerIndex("RightHand");
            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"), Is.True);
            for (int step = 1; step <= 10; step++)
            {
                animator.Update(0.05f);
                update.Invoke(presenter, null);
                if (step == 3 || step == 6 || step == 10)
                {
                    AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(layer);
                    Assert.That(state.IsTag("Attack"), Is.True);
                    Assert.That(state.normalizedTime * state.length, Is.EqualTo(step * 0.05f).Within(0.01f));
                    if (step == 3 || step == 6)
                    {
                        Assert.That(renderer.enabled, Is.True);
                        Assert.That(renderer.sprite?.name, Is.EqualTo(step == 3 ? "VFX-Slash_0" : "VFX-Slash_2"));
                    }
                }
            }
            Assert.That(renderer.enabled, Is.False);
            Assert.That(renderer.sprite, Is.Null);
        }
        finally
        {
            if (player != null) Object.DestroyImmediate(player);
            if (overrides != null) Object.DestroyImmediate(overrides);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    [Test]
    public void Prefab_HasExplicitExistingAnimatorRootReferences()
    {
        Assert.That(_renderer.transform.parent.name, Is.EqualTo("VisualRoot"));
        Assert.That(_renderer.transform.name, Is.EqualTo("AttackVfx"));
        Assert.That(_renderer.sortingLayerName, Is.EqualTo("Characters"));
        Assert.That(_renderer.enabled, Is.False);
        SerializedObject serialized = new SerializedObject(_presenter);
        foreach (string field in new[] { "_combatController", "_equipmentSource", "_character", "_animator", "_vfxTransform", "_vfxRenderer" })
            Assert.That(serialized.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Animator animator = (Animator)serialized.FindProperty("_animator").objectReferenceValue;
        Assert.That(animator.transform, Is.EqualTo(_renderer.transform.parent));
    }
}
