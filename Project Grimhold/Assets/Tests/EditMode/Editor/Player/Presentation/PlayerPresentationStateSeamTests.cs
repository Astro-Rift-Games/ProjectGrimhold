#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Tests.EditMode.Player.Presentation
{
    public sealed class PlayerPresentationStateSeamTests
    {
        private const string MainHand = "RightHandPivot/RightHand";
        private const string OffHand = "LeftHandPivot/LeftHand";
        private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private Scene _scene;

        [SetUp]
        public void SetUp() => _scene = EditorSceneManager.NewPreviewScene();

        [TearDown]
        public void TearDown()
        {
            if (!_scene.IsValid()) return;
            try
            {
                foreach (GameObject root in _scene.GetRootGameObjects())
                {
                    foreach (var weapon in root.GetComponentsInChildren<PlayerWeaponPresenter>(true)) weapon.ReleasePresentation();
                    foreach (var vfx in root.GetComponentsInChildren<PlayerAttackVfxPresenter>(true)) vfx.ReleasePresentation();
                    foreach (var view in root.GetComponentsInChildren<PlayerAnimatorView>(true)) view.ReleasePresentation();
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(_scene); }
        }

        [Test]
        public void NetworkAdapter_PreservesUnspawnedReadsAndForwardsBothEventsUntilDisposed()
        {
            GameObject avatar = CreateAvatar();
            var combat = avatar.GetComponent<PlayerCombatNetworkController>();
            var equipment = avatar.GetComponent<PlayerWeaponEquipmentNetworkController>();
            var movement = avatar.GetComponent<PlayerMovementNetworkController>();
            var shield = avatar.GetComponent<PlayerShieldDefenseNetworkController>();
            var downed = avatar.GetComponent<PlayerDownedStateNetworkController>();
            var character = avatar.GetComponent<CharacterBase>();
            var consumables = avatar.GetComponent<PlayerConsumableNetworkController>();
            var state = new NetworkPlayerPresentationState(combat, equipment, movement, shield, downed, character, consumables);
            IDisposable disposable = state;
            int performed = 0, resumed = 0;
            AttackPerformedEvent last = default;
            state.AttackPerformed += attack => { performed++; last = attack; };
            state.AttackPresentationResumed += attack => { resumed++; last = attack; };
            AttackPerformedEvent payload = Attack(EquipmentSlot.WeaponSetAOffHand);
            try
            {
                Assert.That(state.IsEquipmentReadable, Is.False);
                Assert.That(state.IsAimReadable, Is.False);
                Assert.That(state.HasInputAuthority, Is.False);
                Assert.That(state.IsAimStance, Is.False);
                Assert.That(state.IsDefending, Is.False);
                Assert.That(state.IsAlive, Is.EqualTo(character.IsAlive));
                Assert.That(state.IsDowned, Is.EqualTo(PlayerDownedGate.IsDowned(character)));
                Assert.That(state.IsConsuming, Is.EqualTo(consumables.IsConsuming));
                Assert.That(state.ActiveWeaponSetSlot, Is.EqualTo(equipment.ActiveWeaponSetSlot));
                Assert.That(state.GetActiveHandSlot(false), Is.EqualTo(equipment.GetActiveHandSlot(false)));
                Assert.That(state.GetActiveHandSlot(true), Is.EqualTo(equipment.GetActiveHandSlot(true)));
                Assert.That(state.TryGetAttackPresentationSeconds(payload, out float seconds),
                    Is.EqualTo(combat.TryGetAttackPresentationSeconds(payload, out float expectedSeconds)));
                Assert.That(seconds, Is.EqualTo(expectedSeconds));
                Assert.That(state.TryGetAimStanceElapsedSeconds(out seconds),
                    Is.EqualTo(movement.TryGetAimStanceElapsedSeconds(out expectedSeconds)));
                Assert.That(seconds, Is.EqualTo(expectedSeconds));
                Assert.That(state.TryGetSlotDefinition(EquipmentSlot.WeaponSetAOffHand, out LootDefinition actual),
                    Is.EqualTo(equipment.TryGetSlotDefinition(EquipmentSlot.WeaponSetAOffHand, out LootDefinition expected)));
                Assert.That(actual, Is.SameAs(expected));
                Assert.That(state.TryGetEquippedDefinition(out actual), Is.EqualTo(equipment.TryGetEquippedDefinition(out expected)));
                Assert.That(actual, Is.SameAs(expected));
                Assert.That(state.TryGetWeaponByCatalogIndexPlusOne(1, out actual),
                    Is.EqualTo(equipment.TryGetWeaponByCatalogIndexPlusOne(1, out expected)));
                Assert.That(actual, Is.SameAs(expected), "Catalog resolution must not acquire an Object validity guard");
                Assert.That(state.TryGetConsumingDefinition(out actual), Is.EqualTo(consumables.TryGetConsumingDefinition(out expected)));
                Assert.That(actual, Is.SameAs(expected));

                RaiseControllerEvent(combat, nameof(IPlayerPresentationState.AttackPerformed), payload);
                Assert.That(performed, Is.EqualTo(1));
                Assert.That(last.SourceSlot, Is.EqualTo(payload.SourceSlot));
                Assert.That(last.Sequence, Is.EqualTo(payload.Sequence));
                RaiseControllerEvent(combat, nameof(IPlayerPresentationState.AttackPresentationResumed), payload);
                Assert.That(resumed, Is.EqualTo(1));
                Assert.That(last.Direction, Is.EqualTo(payload.Direction));
                disposable.Dispose();
                RaiseControllerEvent(combat, nameof(IPlayerPresentationState.AttackPerformed), payload);
                RaiseControllerEvent(combat, nameof(IPlayerPresentationState.AttackPresentationResumed), payload);
                Assert.That(performed, Is.EqualTo(1), "Dispose must detach AttackPerformed");
                Assert.That(resumed, Is.EqualTo(1), "Dispose must detach AttackPresentationResumed");
            }
            finally { disposable.Dispose(); }
        }

        [Test]
        public void WeaponStyleAdapter_MissingDownedControllerDoesNotUseCharacterGate()
        {
            GameObject avatar = CreateAvatar();
            var character = avatar.GetComponent<CharacterBase>();
            Assert.That(character, Is.Not.Null);
            using var state = new NetworkPlayerPresentationState(null, null, null, null, null, character, null);
            Assert.That(state.IsDowned, Is.False, "Weapon visibility requires its own valid downed controller, never a character-gate fallback");
        }

        [Test]
        public void VfxStyleAdapter_UsesCharacterGateIndependentlyOfDownedController()
        {
            GameObject avatar = CreateAvatar();
            var character = avatar.GetComponent<CharacterBase>();
            var controller = avatar.GetComponent<PlayerDownedStateNetworkController>();
            Assert.That(character, Is.Not.Null);
            Assert.That(controller, Is.Not.Null);
            using var withController = new NetworkPlayerPresentationState(
                null, null, null, null, controller, character, null, useCharacterDownedGate: true);
            using var withoutController = new NetworkPlayerPresentationState(
                null, null, null, null, null, character, null, useCharacterDownedGate: true);
            Assert.That(withController.IsDowned, Is.EqualTo(PlayerDownedGate.IsDowned(character)));
            Assert.That(withoutController.IsDowned, Is.EqualTo(withController.IsDowned));
            // Neither source can be made authoritatively downed on this real prefab without a
            // running Fusion object. These assertions cover the unspawned guards, not replicated true state.
            Assert.That(withController.IsDowned, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OffHandAttack_PreviewFrameMatchesRealOneHandPoseSampler(bool resume)
        {
            GameObject avatar = CreateAvatar();
            GameObject reference = CreateAvatar();
            var view = avatar.GetComponentInChildren<PlayerAnimatorView>(true);
            Transform root = view.transform;
            Transform referenceRoot = reference.GetComponentInChildren<PlayerAnimatorView>(true).transform;
            // This seam compares sampling/lifecycle and ordinary grip alignment. Full mirrored blade
            // alignment is covered on the real prefab by OffHandMirroredStrokeTests.
            var state = WeaponState(EquipmentSlot.WeaponSetAOffHand, "Rapier");
            Assert.That(state.AttackWeapon.WeaponDefinition.Presentation.OffHandAttackAnimationSet.OffHandStroke,
                Is.EqualTo(OffHandStroke.Reanchored));
            BindAndInitialize(avatar, state);
            PresentFrame(avatar);
            SampleNeutral(referenceRoot);
            Transform hand = root.Find(OffHand), grip = root.Find(OffHand + "/OffHandGrip");
            Transform referenceHand = referenceRoot.Find(OffHand), referenceGrip = referenceRoot.Find(OffHand + "/OffHandGrip");
            Vector3 idleGrip = root.InverseTransformPoint(grip.position);
            Matrix4x4 mainBefore = root.Find(MainHand).localToWorldMatrix;
            AnimationClip clip = state.AttackWeapon.WeaponDefinition.Presentation.GetOffHandAttackClip(3);
            Assert.That(clip, Is.Not.Null, "Generated Rapier South Off Hand clip is required");
            AttackPerformedEvent attack = Attack(EquipmentSlot.WeaponSetAOffHand, state.AttackWeapon.WeaponDefinition.AttackReleaseSeconds);
            if (resume) state.ResumeAttack(attack); else state.RaiseAttack(attack);
            PresentFrame(avatar);
            using (var sampler = new OneHandPoseSampler())
            {
                sampler.Sample(clip, state.AttackSeconds, referenceHand, referenceGrip, referenceHand.GetComponent<SpriteRenderer>());
                Assert.That(Vector3.Distance(root.InverseTransformPoint(grip.position), referenceRoot.InverseTransformPoint(referenceGrip.position)),
                    Is.LessThan(0.001f), "Preview must use the real generated left-hand stroke");
                Assert.That(Vector3.Distance(grip.localPosition, referenceGrip.localPosition), Is.LessThan(0.001f));
                Assert.That(Quaternion.Angle(hand.localRotation, referenceHand.localRotation), Is.LessThan(0.01f));
                Assert.That(Vector3.Distance(hand.localPosition, referenceHand.localPosition), Is.LessThan(0.001f));
                Assert.That(Vector3.Distance(hand.localScale, referenceHand.localScale), Is.LessThan(0.001f));
                Assert.That(hand.GetComponent<SpriteRenderer>().sprite, Is.SameAs(referenceHand.GetComponent<SpriteRenderer>().sprite));
                Assert.That(hand.GetComponent<SpriteRenderer>().sortingOrder, Is.EqualTo(referenceHand.GetComponent<SpriteRenderer>().sortingOrder));
            }
            Assert.That(Vector3.Distance(root.InverseTransformPoint(grip.position), idleGrip), Is.GreaterThan(0.001f), "Attack must move OffHandGrip away from idle");
            Assert.That(root.Find(MainHand).localToWorldMatrix, Is.EqualTo(mainBefore), "Off Hand must not replace Main Hand motion");
            var weapon = avatar.GetComponentInChildren<PlayerWeaponPresenter>(true);
            var serialized = new SerializedObject(weapon);
            var renderer = (SpriteRenderer)serialized.FindProperty("_offHandRenderer").objectReferenceValue;
            var presentation = state.AttackWeapon.WeaponDefinition.Presentation;
            Sprite expectedSprite = PlayerWeaponPresentationMath.ResolveMainHandSprite(
                state.AttackWeapon.WorldSprite ?? state.AttackWeapon.Icon, presentation.AttackSpriteAnimation, true, state.AttackSeconds);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.sprite, Is.SameAs(expectedSprite));
            Assert.That(renderer.transform.IsChildOf(grip), Is.True);
            float angle = PlayerWeaponPresentationMath.CalculateFacingAngleDegrees(Vector2.down) +
                PlayerWeaponPresentationMath.ResolveAngleCorrection(presentation.AngleCorrection, false);
            Vector2 offset = PlayerWeaponPresentationMath.CalculateGripAlignedWeaponPosition(
                presentation.GripPoint, renderer.transform.localScale, angle);
            Assert.That(Vector2.Distance(renderer.transform.localPosition, offset), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(renderer.transform.localRotation, Quaternion.Euler(0f, 0f, angle)), Is.LessThan(0.01f));
            Assert.That(renderer.sortingOrder, Is.EqualTo(PlayerWeaponPresentationMath.ResolveOffHandSortingOrder(
                true, 20, -10, hand.GetComponent<SpriteRenderer>().sortingOrder)));

            // Beginning the next frame releases the overlay even before Animator evaluation.
            weapon.BeginFrame();
            Assert.That(Vector3.Distance(root.InverseTransformPoint(grip.position), idleGrip), Is.LessThan(0.001f));
            state.HasAttackClock = false;
            PresentFrame(avatar);
            Assert.That(view.TryGetPresentedOffHandAttack(out _, out _, out _), Is.False);
            Assert.That(Vector3.Distance(root.InverseTransformPoint(grip.position), idleGrip), Is.LessThan(0.001f));
        }

        [Test]
        public void MainHandAttack_PreviewFrameSeeksRightHandLayerAndMovesGrip()
        {
            GameObject avatar = CreateAvatar();
            var view = avatar.GetComponentInChildren<PlayerAnimatorView>(true);
            var animator = view.GetComponent<Animator>();
            Transform grip = view.transform.Find(MainHand + "/MainHandGrip");
            var state = WeaponState(EquipmentSlot.WeaponSetAMainHand);
            BindAndInitialize(avatar, state);
            PresentFrame(avatar);
            Vector3 idle = view.transform.InverseTransformPoint(grip.position);
            state.RaiseAttack(Attack(EquipmentSlot.WeaponSetAMainHand));
            PresentFrame(avatar);
            int layer = animator.GetLayerIndex("RightHand");
            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).IsTag("Attack"), Is.True);
            Assert.That(view.TryGetPresentedAttackSeconds(out float seconds), Is.True);
            Assert.That(seconds, Is.EqualTo(state.AttackSeconds).Within(0.0001f));
            Assert.That(Vector3.Distance(view.transform.InverseTransformPoint(grip.position), idle), Is.GreaterThan(0.001f));
            AnimationClip clip = state.AttackWeapon.WeaponDefinition.Presentation.GetAttackClip(3);
            Assert.That(animator.GetCurrentAnimatorStateInfo(layer).normalizedTime * clip.length,
                Is.EqualTo(state.AttackSeconds).Within(0.001f));
        }

        private GameObject CreateAvatar()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
            Assert.That(prefab, Is.Not.Null);
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _scene);
            Assert.That(avatar.GetComponentInChildren<Animator>(true), Is.Not.Null);
            return avatar;
        }

        private static FakePlayerPresentationState WeaponState(EquipmentSlot slot, string weaponName = "ArmingSword")
        {
            var loot = AssetDatabase.LoadAssetAtPath<LootDefinition>($"Assets/Scriptable Objects/Loot/Definitions/{weaponName}.asset");
            Assert.That(loot, Is.Not.Null);
            var state = new FakePlayerPresentationState { AttackWeapon = loot, AttackSeconds = loot.WeaponDefinition.AttackReleaseSeconds };
            state.Equipment.Add(slot, loot);
            return state;
        }

        private static AttackPerformedEvent Attack(EquipmentSlot slot, float? releaseSeconds = null) => new AttackPerformedEvent(
            default, default, Vector2.zero, Vector2.down, 10, 1, 7, 11,
            releaseSeconds ?? AssetDatabase.LoadAssetAtPath<LootDefinition>("Assets/Scriptable Objects/Loot/Definitions/ArmingSword.asset").WeaponDefinition.AttackReleaseSeconds,
            sourceSlot: slot);

        private static void BindAndInitialize(GameObject avatar, FakePlayerPresentationState state)
        {
            var view = avatar.GetComponentInChildren<PlayerAnimatorView>(true);
            var weapon = avatar.GetComponentInChildren<PlayerWeaponPresenter>(true);
            var vfx = avatar.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
            var armor = avatar.GetComponentInChildren<PlayerArmorPresenter>(true);
            view.BindPresentationState(state);
            view.BindMovementState(state);
            weapon.BindPresentationState(state);
            vfx.BindPresentationState(state);
            armor.BindEquipmentVisualSource(state);
            view.InitializePresentation();
            vfx.InitializePresentation();
            weapon.InitializePresentation();
            armor.InitializePresentation();
        }

        private static void PresentFrame(GameObject avatar)
        {
            var view = avatar.GetComponentInChildren<PlayerAnimatorView>(true);
            var weapon = avatar.GetComponentInChildren<PlayerWeaponPresenter>(true);
            weapon.BeginFrame(); // Weapon.Update restores the previous overlay.
            view.BeginFrame(); // Animator view's Update updates facing/aim parameters.
            view.GetComponent<Animator>().Update(0f);
            view.PresentFrame(); // Animator view's LateUpdate seeks timed attacks.
            avatar.GetComponentInChildren<PlayerAttackVfxPresenter>(true).PresentFrame();
            weapon.PresentFrame(); // Execution order 100.
            avatar.GetComponentInChildren<PlayerArmorPresenter>(true).PresentFrame(); // Order 200.
        }

        private static void SampleNeutral(Transform root)
        {
            foreach (string node in new[] { MainHand, MainHand + "/MainHandGrip", OffHand, OffHand + "/OffHandGrip" })
            {
                root.Find(node).SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.Find(node).localScale = Vector3.one;
            }
            foreach (string path in new[] {
                "Assets/Animations/Player/Idle/Idle_S.anim",
                "Assets/Animations/Player/Idle/RightHand/RightHand_Idle_S.anim",
                "Assets/Animations/Player/Idle/LeftHand/LeftHand_Idle_S.anim" })
                AssetDatabase.LoadAssetAtPath<AnimationClip>(path).SampleAnimation(root.gameObject, 0f);
        }

        private static void RaiseControllerEvent(PlayerCombatNetworkController controller, string name, AttackPerformedEvent attack)
        {
            var field = typeof(PlayerCombatNetworkController).GetField(name, InstanceMembers);
            Assert.That(field, Is.Not.Null, $"Controller event backing field {name} missing");
            (field.GetValue(controller) as Action<AttackPerformedEvent>)?.Invoke(attack);
        }
    }
}
#endif
