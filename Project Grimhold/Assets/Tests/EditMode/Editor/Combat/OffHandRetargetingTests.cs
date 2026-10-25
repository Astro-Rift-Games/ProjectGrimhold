#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.EditMode.Combat
{
    public sealed class OffHandRetargetingTests
    {
        private const string Main = "RightHandPivot/RightHand";
        private const string Off = "LeftHandPivot/LeftHand";

        private static IEnumerable<TestCaseData> Cases()
        {
            foreach (string weapon in new[] { "ArmingSword", "MagicCinquedea", "MagicSword", "MagicWand", "Rapier", "RondelDagger" })
                for (int direction = 0; direction < 6; direction++) yield return new TestCaseData(weapon, direction);
        }

        [TestCaseSource(nameof(Cases))]
        public void AttackRemainsAtTheDrawnLeftHand_OnTheRealAvatar(string asset, int direction)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
            var live = Object.Instantiate(prefab);
            var reference = Object.Instantiate(prefab);
            live.SetActive(false);
            reference.SetActive(false);
            try
            {
                Transform destination = live.GetComponentInChildren<PlayerAnimatorView>(true).transform;
                Transform source = reference.GetComponentInChildren<PlayerAnimatorView>(true).transform;
                string facing = new[] { "N", "NE", "NW", "S", "SE", "SW" }[direction];
                SampleNeutral(source, facing);
                SampleNeutral(destination, facing);
                Vector3 mainNeutral = source.InverseTransformPoint(source.Find(Main + "/MainHandGrip").position);
                Vector3 offNeutral = destination.InverseTransformPoint(destination.Find(Off + "/OffHandGrip").position);
                Sprite leftSprite = destination.Find(Off).GetComponent<SpriteRenderer>().sprite;
                var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>($"Assets/Scriptable Objects/Loot/Definitions/{asset}.asset");
                var weapon = loot.WeaponDefinition;
                AnimationClip mainClip = weapon.Presentation.GetAttackClip(direction);
                AnimationClip offClip = weapon.Presentation.GetOffHandAttackClip(direction);
                Assert.That(offClip, Is.Not.Null);
                Assert.That(offClip.length, Is.EqualTo(mainClip.length).Within(0.0001f));
                Hash128 sourceBefore = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(mainClip));
                Matrix4x4 liveMain = destination.Find(Main).localToWorldMatrix;
                Transform hand = destination.Find(Off);
                Transform grip = destination.Find(Off + "/OffHandGrip");
                using (var sampler = new OneHandPoseSampler())
                {
                    int frames = Mathf.CeilToInt(mainClip.length * 60f);
                    for (int frame = 0; frame <= frames; frame++)
                    {
                        float seconds = frame * mainClip.length / frames;
                        sampler.Restore();
                        SampleNeutral(source, facing);
                        SampleNeutral(destination, facing);
                        mainClip.SampleAnimation(source.gameObject, seconds);
                        Vector3 delta = source.InverseTransformPoint(source.Find(Main + "/MainHandGrip").position) - mainNeutral;
                        var set = weapon.Presentation.OffHandAttackAnimationSet;
                        Vector3 expected = offNeutral + delta; // Reanchored thrust/cast contract is unchanged.
                        if (set.OffHandStroke == OffHandStroke.Mirrored)
                        {
                            Vector2 f = PlayerWeaponPresentationMath.CanonicalFacing(direction);
                            Vector3 Reflect(Vector3 p)
                            {
                                Vector3 axis = f;
                                var result = 2 * Vector3.Dot(p, axis) * axis - p;
                                result.z = p.z;
                                return result;
                            }
                            expected = offNeutral + Reflect(delta);
                            if (set.CanonicalSlash)
                            {
                                // Approved slash geometry mirrors about the body during the stroke,
                                // blending anatomical neutral corrections only outside that window.
                                var window = set.SlashStrokeWindow;
                                float weight = seconds < window.x ? Mathf.SmoothStep(0, 1, seconds / window.x) :
                                    seconds > window.y ? 1 - Mathf.SmoothStep(0, 1,
                                        (seconds - window.y) / (set.SlashRecoveryEndSeconds - window.y)) : 1;
                                expected = Reflect(mainNeutral + delta) + (offNeutral - Reflect(mainNeutral)) * (1 - weight);
                            }
                        }
                        sampler.Sample(offClip, seconds, hand, grip, hand.GetComponent<SpriteRenderer>());
                        Assert.That(Vector3.Distance(destination.InverseTransformPoint(grip.position), expected), Is.LessThan(0.001f),
                            $"{asset}/{facing} at {seconds}: Off Hand must follow its set's geometry and anatomical recovery.");
                        Assert.That(hand.GetComponent<SpriteRenderer>().sprite, Is.EqualTo(leftSprite), "Keep the drawn left hand.");
                        Assert.That(destination.Find(Main).localToWorldMatrix, Is.EqualTo(liveMain), "Do not move Main Hand.");
                    }
                    sampler.Restore();
                    Assert.That(destination.InverseTransformPoint(grip.position), Is.EqualTo(offNeutral));
                }
                Assert.That(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(mainClip)), Is.EqualTo(sourceBefore));
            }
            finally
            {
                Object.DestroyImmediate(live);
                Object.DestroyImmediate(reference);
            }
        }

        private static IEnumerable<TestCaseData> FrontCases()
        {
            foreach (var cell in Cases())
                if ((int)cell.Arguments[1] >= 3) yield return cell;
        }

        [TestCaseSource(nameof(FrontCases))]
        public void FrontOffHand_StaysBetweenItsWeaponAndMainHandThroughIdleAndAttack(string asset, int direction)
        {
            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>($"Assets/Scriptable Objects/Loot/Definitions/{asset}.asset");
            using var rig = new AttackPreviewRig();
            rig.Configure(new AttackPreviewCombination(null, loot, AttackPreviewAction.OffAttack, direction), sequential: true);
            var armor = rig.Root.GetComponentInChildren<PlayerArmorPresenter>(true);
            using var serialized = new SerializedObject(armor);
            var glove = (SpriteRenderer)serialized.FindProperty("_leftGloveVisual").objectReferenceValue;
            var glovesConfig = new EquipmentVisualDefinition(Color.cyan);
            var glovesField = typeof(PlayerArmorPresenter).GetField("_glovesConfig", BindingFlags.Instance | BindingFlags.NonPublic);
            var body = rig.BodyRenderers.Single(r => r.name == "Body");
            float end = rig.Duration + 0.2f;
            int frames = Mathf.CeilToInt(end * 60f);
            // Sequential evaluation exercises previous-frame sorting and the idle cleanup tail.
            for (int frame = 0; frame <= frames; frame++)
            {
                float seconds = frame * end / frames;
                rig.Evaluate(seconds);
                // The catalog preview equips hands only: its armor pass disables unequipped gloves
                // (and its first revision refresh clears injected config). Supply the test glove
                // after that refresh, then run the real Armor pass after Animator and Weapon.
                glovesField.SetValue(armor, glovesConfig);
                glove.enabled = true;
                armor.PresentFrame();
                int hand = rig.HandRenderers[1].sortingOrder;
                int weapon = rig.WeaponRenderers[1].sortingOrder;
                string context = $"{asset}/{direction} at {seconds}s";
                Assert.That(hand, Is.EqualTo(22), context);
                Assert.That(weapon, Is.EqualTo(20), context);
                Assert.That(hand, Is.GreaterThan(weapon).And.LessThan(rig.HandRenderers[0].sortingOrder), context);
                Assert.That(rig.HandRenderers[0].sortingOrder, Is.EqualTo(30), context);
                Assert.That(glove.sortingOrder, Is.EqualTo(hand + 1), context);
                Assert.That(weapon, Is.GreaterThan(body.sortingOrder), context);
            }
        }

        [TestCaseSource(nameof(FrontCases))]
        public void FrontOffAttack_ValidatorReportsNoHandOccludedByWeapon(string asset, int direction)
        {
            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>($"Assets/Scriptable Objects/Loot/Definitions/{asset}.asset");
            using var rig = new AttackPreviewRig();
            var cell = new AttackPreviewCombination(null, loot, AttackPreviewAction.OffAttack, direction);
            var report = new AttackPreviewValidator().Validate(rig, cell);
            Assert.That(report.WorstChecks.Where(check => check.Id == AttackPreviewCheckId.HandOccludedByWeapon &&
                check.Hand == AttackPreviewHand.Off && check.IsViolation), Is.Empty, cell.Id);
        }

        private static void SampleNeutral(Transform root, string direction)
        {
            foreach (string node in new[] { Main, Main + "/MainHandGrip", Off, Off + "/OffHandGrip" })
            {
                root.Find(node).SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.Find(node).localScale = Vector3.one;
            }
            foreach (string path in new[] {
                $"Assets/Animations/Player/Idle/Idle_{direction}.anim",
                $"Assets/Animations/Player/Idle/RightHand/RightHand_Idle_{direction}.anim",
                $"Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_{direction}.anim" })
                AssetDatabase.LoadAssetAtPath<AnimationClip>(path).SampleAnimation(root.gameObject, 0f);
        }

        [TestCase("Idle")]
        [TestCase("Walk")]
        public void OverlayRestoresTheEvaluatedPoseAndLeavesVisibilityToItsOwner(string motion)
        {
            var avatar = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab"));
            avatar.SetActive(false);
            try
            {
                Transform root = avatar.GetComponentInChildren<PlayerAnimatorView>(true).transform;
                Transform hand = root.Find(Off), grip = root.Find(Off + "/OffHandGrip");
                var renderer = hand.GetComponent<SpriteRenderer>();
                using var sampler = new OneHandPoseSampler();
                var sword = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
                var wand = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/MagicWand.asset");
                for (int direction = 0; direction < 6; direction++)
                {
                    string facing = new[] { "N", "NE", "NW", "S", "SE", "SW" }[direction];
                    sampler.Restore(); // Presenter.Update, before the Animator evaluates the new frame.
                    SampleNeutral(root, facing);
                    AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/{motion}/{motion}_{facing}.anim").SampleAnimation(root.gameObject, 0.125f);
                    AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/Animations/Player/{motion}/LeftHand/LeftHand_{motion}_{facing}.anim").SampleAnimation(root.gameObject, 0.125f);
                    Matrix4x4 handBase = hand.localToWorldMatrix, gripBase = grip.localToWorldMatrix;
                    Sprite spriteBase = renderer.sprite;
                    int orderBase = renderer.sortingOrder;
                    WeaponDefinition weapon = direction % 2 == 0 ? sword.WeaponDefinition : wand.WeaponDefinition;
                    sampler.Sample(weapon.Presentation.GetOffHandAttackClip(direction), weapon.AttackReleaseSeconds,
                        hand, grip, renderer);
                    renderer.enabled = false; // Downed/gloves may change visibility after the overlay.
                    sampler.Restore(); // Also the end/cancel/set-change/disable seam.
                    sampler.Restore(); // Idempotent release.
                    Assert.That(hand.localToWorldMatrix, Is.EqualTo(handBase));
                    Assert.That(grip.localToWorldMatrix, Is.EqualTo(gripBase));
                    Assert.That(renderer.sprite, Is.EqualTo(spriteBase));
                    Assert.That(renderer.sortingOrder, Is.EqualTo(orderBase));
                    Assert.That(renderer.enabled, Is.False);
                }
            }
            finally { Object.DestroyImmediate(avatar); }
        }

        [Test]
        public void SamplerResetsPropertiesMissingFromTheNextClip()
        {
            var hand = new GameObject("LeftHand");
            var grip = new GameObject("OffHandGrip");
            grip.transform.SetParent(hand.transform, false);
            var renderer = hand.AddComponent<SpriteRenderer>();
            var empty = new AnimationClip();
            try
            {
                var sword = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
                using var sampler = new OneHandPoseSampler();
                sampler.Sample(sword.WeaponDefinition.Presentation.GetOffHandAttackClip(3),
                    sword.WeaponDefinition.AttackReleaseSeconds, hand.transform, grip.transform, renderer);
                sampler.Sample(empty, 0f, hand.transform, grip.transform, renderer);
                Assert.That(hand.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(Quaternion.Angle(hand.transform.localRotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(grip.transform.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(hand.transform.localScale, Is.EqualTo(Vector3.one));
            }
            finally { Object.DestroyImmediate(hand); Object.DestroyImmediate(empty); }
        }

        [Test]
        public void MissingLeftSetDoesNotBorrowTheRightClip()
        {
            var original = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
            WeaponDefinition clone = Object.Instantiate(original.WeaponDefinition);
            try
            {
                var serialized = new SerializedObject(clone);
                serialized.FindProperty("_presentation").FindPropertyRelative("_offHandAttackAnimationSet").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(clone.Presentation.GetAttackClip(3), Is.Not.Null);
                Assert.That(clone.Presentation.GetOffHandAttackClip(3), Is.Null);
                Assert.That(clone.TryValidate(out _), Is.True, "Legacy configuration remains valid.");
            }
            finally { Object.DestroyImmediate(clone); }
        }

        [Test]
        public void GlovesCopyTheFinalLeftSpriteAndSortingAfterWeaponPresentation()
        {
            var avatar = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab"));
            avatar.SetActive(false);
            try
            {
                Transform root = avatar.GetComponentInChildren<PlayerAnimatorView>(true).transform;
                SampleNeutral(root, "SW");
                Transform hand = root.Find(Off), grip = root.Find(Off + "/OffHandGrip");
                var renderer = hand.GetComponent<SpriteRenderer>();
                var armor = avatar.GetComponentInChildren<PlayerArmorPresenter>(true);
                var fields = new SerializedObject(armor);
                var glove = (SpriteRenderer)fields.FindProperty("_leftGloveVisual").objectReferenceValue;
                glove.enabled = true;
                typeof(PlayerArmorPresenter).GetField("_glovesConfig", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(armor, new EquipmentVisualDefinition(Color.cyan));
                var sword = AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset");
                using var sampler = new OneHandPoseSampler();
                sampler.Sample(sword.WeaponDefinition.Presentation.GetOffHandAttackClip(5),
                    sword.WeaponDefinition.AttackReleaseSeconds, hand, grip, renderer);
                typeof(PlayerArmorPresenter).GetMethod("UpdateVisualsToMatchBase", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(armor, null);
                Assert.That(glove.sprite, Is.EqualTo(renderer.sprite));
                Assert.That(glove.sortingOrder, Is.EqualTo(renderer.sortingOrder + 1));
                int armorOrder = typeof(PlayerArmorPresenter).GetCustomAttribute<DefaultExecutionOrder>().order;
                int weaponOrder = typeof(PlayerWeaponPresenter).GetCustomAttribute<DefaultExecutionOrder>().order;
                Assert.That(armorOrder, Is.GreaterThan(weaponOrder));
            }
            finally { Object.DestroyImmediate(avatar); }
        }

        [TestCase(45f, 17f)]
        [TestCase(135f, -17f)]
        [TestCase(-45f, -17f)]
        [TestCase(-135f, 17f)]
        public void FreeAimVfxUsesTheActualLeftGripAndKeepsTheAuthoredLead(float facing, float residual)
        {
            var root = new GameObject("VisualRoot");
            var hand = new GameObject("LeftHand");
            hand.transform.SetParent(root.transform, false);
            var grip = new GameObject("OffHandGrip");
            grip.transform.SetParent(hand.transform, false);
            try
            {
                hand.transform.localRotation = Quaternion.Euler(0f, 0f, 11f);
                grip.transform.localPosition = new Vector3(0.375f, -0.25f, 0f);
                Vector2 aim = Quaternion.Euler(0f, 0f, facing + residual) * Vector2.right;
                var lead = new AttackVfxDefinition.DirectionalPose(new Vector3(7f, 8f, 3f), Vector3.zero, 0.25f, false, 21);
                var pose = FreeAimAttackVfxPose.ResolveAtGrip(root.transform, grip.transform, aim, facing > 90f || facing < -90f, lead, 21);
                Assert.That(Vector2.Distance(pose.Position, grip.transform.localToWorldMatrix.MultiplyPoint3x4(Vector3.zero)), Is.LessThan(0.0001f));
                Assert.That(Vector2.Angle(pose.Rotation * Vector2.right, grip.transform.TransformVector(aim)), Is.LessThan(0.001f));
                Assert.That(pose.Position.z, Is.EqualTo(lead.Position.z));
                Assert.That(pose.ReachOffset, Is.EqualTo(lead.ReachOffset));
                Assert.That(pose.SortingOrder, Is.EqualTo(21));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CatalogValidation_RejectsTwoHandedWeaponWithStaleOffHandSet()
        {
            var source = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSwordWeaponDefinition.asset");
            var weapon = Object.Instantiate(source);
            try
            {
                Assert.That(weapon.Presentation.OffHandAttackAnimationSet, Is.Not.Null);
                var data = new SerializedObject(weapon);
                data.FindProperty("_handedness").enumValueIndex = (int)WeaponHandedness.TwoHanded;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(OffHandAnimationGenerator.TryValidateCatalog(new[] { weapon }, out string error), Is.False);
                Assert.That(error, Does.Contain("two-handed").And.Contain("Off Hand"));
            }
            finally { Object.DestroyImmediate(weapon); }
        }

        [Test]
        public void CatalogValidation_AcceptsExactlySixOneHandedWeapons()
        {
            var weapons = AssetDatabase.FindAssets("t:WeaponDefinition")
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponDefinition>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(w => w.Handedness == WeaponHandedness.OneHanded && w.Presentation.HasGenericAttack).ToArray();
            Assert.That(weapons, Has.Length.EqualTo(6));
            Assert.That(OffHandAnimationGenerator.TryValidateCatalog(weapons, out string error), Is.True, error);
            Assert.That(OffHandAnimationGenerator.TryValidateCatalog(out error), Is.True, error);
        }

        [Test]
        public void GeneratedCatalogIsCompleteAndRegenerationPreservesGuidsAndSourceBytes()
        {
            Assert.That(OffHandAnimationGenerator.TryValidateCatalog(out string error), Is.True, error);
            var files = new Dictionary<string, string>();
            var generated = new Dictionary<string, string>();
            foreach (string name in new[] { "ArmingSword", "MagicCinquedea", "MagicSword", "MagicWand", "Rapier", "RondelDagger" })
            {
                var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>($"Assets/Scriptable Objects/Loot/Definitions/{name}.asset");
                for (int direction = 0; direction < 6; direction++)
                {
                    string source = AssetDatabase.GetAssetPath(loot.WeaponDefinition.Presentation.GetAttackClip(direction));
                    files[source] = System.Convert.ToBase64String(System.IO.File.ReadAllBytes(source));
                    string derived = AssetDatabase.GetAssetPath(loot.WeaponDefinition.Presentation.GetOffHandAttackClip(direction));
                    generated[derived] = AssetDatabase.AssetPathToGUID(derived);
                    foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(loot.WeaponDefinition.Presentation.GetOffHandAttackClip(direction)))
                        Assert.That(binding.path == Off || binding.path == Off + "/OffHandGrip", Is.True, binding.path);
                }
                string set = AssetDatabase.GetAssetPath(loot.WeaponDefinition.Presentation.OffHandAttackAnimationSet);
                generated[set] = AssetDatabase.AssetPathToGUID(set);
            }
            var derivedBytes = new Dictionary<string, string>();
            foreach (string path in generated.Keys) derivedBytes[path] = System.Convert.ToBase64String(System.IO.File.ReadAllBytes(path));
            OffHandAnimationGenerator.GenerateAll();
            foreach (var pair in files)
                Assert.That(System.Convert.ToBase64String(System.IO.File.ReadAllBytes(pair.Key)), Is.EqualTo(pair.Value), pair.Key);
            foreach (var pair in generated)
            {
                Assert.That(AssetDatabase.AssetPathToGUID(pair.Key), Is.EqualTo(pair.Value), pair.Key);
                Assert.That(System.Convert.ToBase64String(System.IO.File.ReadAllBytes(pair.Key)), Is.EqualTo(derivedBytes[pair.Key]), pair.Key);
            }
        }
    }
}
#endif
