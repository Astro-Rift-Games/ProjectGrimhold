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

    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void ConfirmedSwordAttack_CapturesConfiguredDirectionalPose(CharacterVisualDirection direction, int index)
    {
        AttackVfxDefinition.DirectionalPose pose =
            AssetDatabase.LoadAssetAtPath<WeaponDefinition>(SwordPath).Presentation.AttackVfx.GetPose(index);
        Perform(IndexPlusOne("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset"),
            CharacterVisualDirectionResolver.GetCanonicalVector(direction));
        Assert.That(State(_presenter, "_pending"), Is.True);
        Assert.That(_renderer.transform.name, Is.EqualTo("AttackVfx"));
        Assert.That(_renderer.transform.parent.name, Is.EqualTo("VisualRoot"));
        Assert.That(_renderer.transform.localPosition, Is.EqualTo(pose.Position));
        Assert.That(Quaternion.Angle(_renderer.transform.localRotation, pose.Rotation), Is.EqualTo(0f).Within(0.001f));
        Assert.That(_renderer.transform.localScale, Is.EqualTo(pose.Scale));
        Assert.That(_renderer.sortingOrder, Is.EqualTo(pose.SortingOrder));
        Assert.That(_renderer.enabled, Is.False);
    }

    // Functional contract: once the presenter applies the configured directional pose, the VFX sprite
    // sequence must sweep in the same rotational sense as the weapon swing during the VFX window.
    // Both sweeps are measured in the Animator root space, so no mirror axis or weapon is assumed.
    [TestCase(CharacterVisualDirection.North, 0)]
    [TestCase(CharacterVisualDirection.NorthEast, 1)]
    [TestCase(CharacterVisualDirection.NorthWest, 2)]
    [TestCase(CharacterVisualDirection.South, 3)]
    [TestCase(CharacterVisualDirection.SouthEast, 4)]
    [TestCase(CharacterVisualDirection.SouthWest, 5)]
    public void AttackVfx_SweepsInSameRotationalSenseAsWeaponSwing(CharacterVisualDirection direction, int index)
    {
        var weaponPresenter = _contents.GetComponentInChildren<PlayerWeaponPresenter>(true);
        Assert.That(weaponPresenter, Is.Not.Null);
        Transform grip = (Transform)new SerializedObject(weaponPresenter).FindProperty("_mainHandGrip").objectReferenceValue;
        Transform root = _renderer.transform.parent;
        Assert.That(grip, Is.Not.Null);
        Assert.That(grip.IsChildOf(root), Is.True);

        int checkedWeapons = 0;
        for (int catalogIndex = 0; catalogIndex < _catalog.DefinitionCount; catalogIndex++)
        {
            if (!_catalog.TryGetByIndex(catalogIndex, out LootDefinition loot) || loot.WeaponDefinition == null) continue;
            WeaponDefinition.PresentationConfig presentation = loot.WeaponDefinition.Presentation;
            AttackVfxDefinition vfx = presentation.AttackVfx;
            if (vfx == null || !presentation.HasGenericAttack) continue;

            Perform(catalogIndex + 1, CharacterVisualDirectionResolver.GetCanonicalVector(direction));
            Assert.That(State(_presenter, "_pending"), Is.True, loot.name);
            float vfxSweep = MeasureVfxSweep(vfx.Clip, root.worldToLocalMatrix * _renderer.transform.localToWorldMatrix);
            float weaponSweep = MeasureWeaponSweep(presentation.GetAttackClip(index), root, grip,
                vfx.StartSeconds, vfx.StartSeconds + vfx.Clip.length);

            string context = $"{loot.name} {direction}: VFX sweep {vfxSweep:F1}°, weapon sweep {weaponSweep:F1}°";
            Assert.That(Mathf.Abs(vfxSweep), Is.GreaterThan(1f), context);
            Assert.That(Mathf.Abs(weaponSweep), Is.GreaterThan(1f), context);
            Assert.That(Mathf.Sign(vfxSweep), Is.EqualTo(Mathf.Sign(weaponSweep)), context);
            checkedWeapons++;
        }
        Assert.That(checkedWeapons, Is.GreaterThan(0), "No catalog weapon configures an Attack VFX.");
    }

    /// <summary>Signed degrees swept by the frame centroids of the VFX sprite sequence, in root space.</summary>
    private float MeasureVfxSweep(AnimationClip clip, Matrix4x4 vfxToRoot)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        Assert.That(bindings, Has.Length.EqualTo(1));
        ObjectReferenceKeyframe[] frames = AnimationUtility.GetObjectReferenceCurve(clip, bindings[0]);
        Assert.That(frames.Length, Is.GreaterThanOrEqualTo(2));
        Vector2 flip = new Vector2(_renderer.flipX ? -1f : 1f, _renderer.flipY ? -1f : 1f);
        float sweep = 0f;
        float previous = 0f;
        for (int i = 0; i < frames.Length; i++)
        {
            var sprite = frames[i].value as Sprite;
            Assert.That(sprite, Is.Not.Null, $"frame {i}");
            Vector2 centroid = Vector2.Scale(CalculateMeshCentroid(sprite), flip);
            float angle = Angle(vfxToRoot.MultiplyVector(centroid));
            if (i > 0) sweep += Mathf.DeltaAngle(previous, angle);
            previous = angle;
        }
        return sweep;
    }

    /// <summary>Signed degrees swept by the main-hand grip axis while the attack clip plays the window.</summary>
    private static float MeasureWeaponSweep(AnimationClip attack, Transform root, Transform grip, float start, float end)
    {
        Assert.That(attack, Is.Not.Null);
        const int Samples = 40;
        float sweep = 0f;
        float previous = 0f;
        for (int i = 0; i <= Samples; i++)
        {
            attack.SampleAnimation(root.gameObject, Mathf.Lerp(start, end, i / (float)Samples));
            float angle = Angle((root.worldToLocalMatrix * grip.localToWorldMatrix).MultiplyVector(Vector3.right));
            if (i > 0) sweep += Mathf.DeltaAngle(previous, angle);
            previous = angle;
        }
        return sweep;
    }

    private static Vector2 CalculateMeshCentroid(Sprite sprite)
    {
        Vector2[] vertices = sprite.vertices;
        ushort[] triangles = sprite.triangles;
        Vector2 weighted = Vector2.zero;
        float area = 0f;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 a = vertices[triangles[i]];
            Vector2 b = vertices[triangles[i + 1]];
            Vector2 c = vertices[triangles[i + 2]];
            float triangleArea = Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) * 0.5f;
            weighted += (a + b + c) / 3f * triangleArea;
            area += triangleArea;
        }
        Assert.That(area, Is.GreaterThan(0f), sprite.name);
        return weighted / area;
    }

    private static float Angle(Vector3 vector) => Mathf.Atan2(vector.y, vector.x) * Mathf.Rad2Deg;

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
        // Index order: N, NE, NW, S, SE, SW.
        var positions = new[]
        {
            new Vector3(0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f),
            new Vector3(0.2f, -0.3f, 0f), new Vector3(0.2f, -0.3f, 0f), new Vector3(0.2f, -0.3f, 0f)
        };
        var scales = new[]
        {
            new Vector3(0.65f, -0.65f, 1f), new Vector3(0.65f, -0.65f, 1f), new Vector3(0.65f, -0.65f, 1f),
            new Vector3(0.65f, -0.65f, 1f), new Vector3(0.65f, -0.65f, 1f), new Vector3(0.65f, -0.65f, 1f)
        };
        var sortingOrders = new[] { -9, -9, -9, 21, 21, 21 };
        for (int i = 0; i < 6; i++)
        {
            var pose = vfx.GetPose(i);
            Assert.That(pose.Position, Is.EqualTo(positions[i]), $"pose {i}");
            Assert.That(pose.Scale, Is.EqualTo(scales[i]), $"pose {i}");
            Assert.That(pose.Rotation, Is.EqualTo(Quaternion.identity), $"pose {i}");
            Assert.That(pose.SortingOrder, Is.EqualTo(sortingOrders[i]), $"pose {i}");
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
