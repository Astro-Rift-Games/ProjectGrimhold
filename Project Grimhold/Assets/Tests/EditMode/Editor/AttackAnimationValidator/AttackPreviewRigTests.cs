#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackPreviewRigTests
    {
        private AttackPreviewRig _rig;
        private Scene _referenceScene;
        [TearDown]
        public void TearDown()
        {
            try { _rig?.Dispose(); }
            finally { if (_referenceScene.IsValid()) EditorSceneManager.ClosePreviewScene(_referenceScene); }
        }

        private void Configure(bool offHand)
        {
            _rig = new AttackPreviewRig();
            _rig.Configure(new AttackPreviewCombination(offHand ? null : AttackPreviewCatalogTests.Sword,
                offHand ? AttackPreviewCatalogTests.Sword : null,
                offHand ? AttackPreviewAction.OffAttack : AttackPreviewAction.MainAttack, 3));
        }

        private Transform ReferenceRoot()
        {
            _referenceScene = EditorSceneManager.NewPreviewScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _referenceScene);
            var root = avatar.GetComponentInChildren<PlayerAnimatorView>(true).transform;
            foreach (string path in new[] { "Idle/Idle_S", "Idle/RightHand/RightHand_Idle_S", "Idle/LeftHand/LeftHand_Idle_S" })
                AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/" + path + ".anim").SampleAnimation(root.gameObject, 0f);
            return root;
        }

        [Test]
        public void MainAttack_MidpointMatchesRealSouthClip()
        {
            Configure(false);
            _rig.Evaluate(0f);
            Vector3 initial = _rig.VisualRoot.InverseTransformPoint(_rig.MainHandGrip.position);
            var clip = AttackPreviewCatalogTests.Sword.WeaponDefinition.Presentation.GetAttackClip(3);
            float mid = clip.length * 0.5f;
            var reference = ReferenceRoot();
            clip.SampleAnimation(reference.gameObject, mid);
            _rig.Evaluate(mid);
            Vector3 actual = _rig.VisualRoot.InverseTransformPoint(_rig.MainHandGrip.position);
            Vector3 expected = reference.InverseTransformPoint(reference.Find("RightHandPivot/RightHand/MainHandGrip").position);
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(actual, initial), Is.GreaterThan(0.001f));
            Assert.That(_rig.Duration, Is.GreaterThanOrEqualTo(clip.length));
        }

        [Test]
        public void OffAttack_MidpointMatchesRealOneHandPoseSampler()
        {
            Configure(true);
            var clip = AttackPreviewCatalogTests.Sword.WeaponDefinition.Presentation.GetOffHandAttackClip(3);
            Assert.That(clip, Is.Not.Null);
            float mid = clip.length * 0.5f;
            var reference = ReferenceRoot();
            var hand = reference.Find("LeftHandPivot/LeftHand");
            var grip = hand.Find("OffHandGrip");
            using var sampler = new OneHandPoseSampler();
            sampler.Sample(clip, mid, hand, grip, hand.GetComponent<SpriteRenderer>());
            _rig.Evaluate(mid);
            Assert.That(Vector3.Distance(_rig.VisualRoot.InverseTransformPoint(_rig.OffHandGrip.position),
                reference.InverseTransformPoint(grip.position)), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(_rig.OffHandGrip.parent.localRotation, hand.localRotation), Is.LessThan(0.01f));
            Assert.That(_rig.OffHandGrip.parent.GetComponent<SpriteRenderer>().sprite, Is.SameAs(hand.GetComponent<SpriteRenderer>().sprite));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CompletedAttack_ReturnsToNeutralAndSupportsBackwardSeek(bool offHand)
        {
            Configure(offHand);
            var reference = ReferenceRoot();
            string path = offHand ? "LeftHandPivot/LeftHand/OffHandGrip" : "RightHandPivot/RightHand/MainHandGrip";
            Transform grip = offHand ? _rig.OffHandGrip : _rig.MainHandGrip;
            _rig.Evaluate(0f);
            Vector3 start = _rig.VisualRoot.InverseTransformPoint(grip.position);
            _rig.Evaluate(_rig.Duration + 0.1f);
            Assert.That(Vector3.Distance(_rig.VisualRoot.InverseTransformPoint(grip.position), reference.InverseTransformPoint(reference.Find(path).position)), Is.LessThan(0.001f));
            _rig.Evaluate(0f);
            Assert.That(Vector3.Distance(_rig.VisualRoot.InverseTransformPoint(grip.position), start), Is.LessThan(0.001f));
        }

        [Test]
        public void Reconfigure_RemovesPreviousOffHandSpriteAndOverlay()
        {
            Configure(true);
            _rig.Evaluate(AttackPreviewCatalogTests.Sword.WeaponDefinition.AttackReleaseSeconds);
            Assert.That(_rig.WeaponRenderers.Any(r => r.enabled && r.sprite != null && r.transform.IsChildOf(_rig.OffHandGrip)), Is.True);
            _rig.Configure(new AttackPreviewCombination(AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3));
            _rig.Evaluate(0f);
            Assert.That(_rig.WeaponRenderers.Where(r => r.transform.IsChildOf(_rig.OffHandGrip)).All(r => !r.enabled || r.sprite == null), Is.True);
            var reference = ReferenceRoot();
            Assert.That(Vector3.Distance(_rig.VisualRoot.InverseTransformPoint(_rig.OffHandGrip.position), reference.InverseTransformPoint(reference.Find("LeftHandPivot/LeftHand/OffHandGrip").position)), Is.LessThan(0.001f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void OffOnlyCell_NeverRendersAMainWeapon(int facing)
        {
            _rig = new AttackPreviewRig();
            _rig.Configure(new AttackPreviewCombination(null, AttackPreviewCatalogTests.Sword,
                AttackPreviewAction.OffAttack, facing));
            foreach (float seconds in new[] { 0f, _rig.Duration * 0.5f, _rig.Duration + 0.1f })
            {
                _rig.Evaluate(seconds);
                var main = _rig.WeaponRenderers[0];
                var off = _rig.WeaponRenderers[1];
                Assert.That(main.enabled && main.sprite != null, Is.False, $"Main ghost at {seconds}s facing {facing}.");
                Assert.That(off.enabled && off.sprite != null, Is.True, "The actual Off Hand item must still render.");
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ShieldInEitherHand_CoversItsHandInEveryFacingThroughDefenseAndIdle(bool mainShield)
        {
            var cells = AttackPreviewCatalog.Enumerate().Where(c => c.Action == AttackPreviewAction.Defend &&
                (mainShield ? c.MainHand : c.OffHand)?.Category == LootCategory.Shield).ToArray();
            Assert.That(cells, Is.Not.Empty);
            _rig = new AttackPreviewRig();
            foreach (var cell in cells)
            {
                _rig.Configure(cell);
                foreach (float seconds in new[] { 0f, _rig.Duration * 0.5f, _rig.Duration + 0.2f })
                {
                    _rig.Evaluate(seconds);
                    int index = mainShield ? 0 : 1;
                    Assert.That(_rig.WeaponRenderers[index].sortingOrder,
                        Is.GreaterThan(_rig.HandRenderers[index].sortingOrder + 1), $"{cell.Id} at {seconds}s");
                }
                // An attack cell's cleanup tail exercises idle with the same shield loadout.
                var idle = new AttackPreviewCombination(cell.MainHand, cell.OffHand,
                    mainShield ? AttackPreviewAction.OffAttack : AttackPreviewAction.MainAttack, cell.FacingIndex);
                _rig.Configure(idle);
                _rig.Evaluate(_rig.Duration + 0.2f);
                int hand = mainShield ? 0 : 1;
                Assert.That(_rig.WeaponRenderers[hand].sortingOrder,
                    Is.GreaterThan(_rig.HandRenderers[hand].sortingOrder + 1), idle.Id);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BrokenPresenter_RebuildsBeforeConfigureOrEvaluateWithoutLogging(bool configureAgain)
        {
            Configure(false);
            var oldRoot = _rig.Root;
            var presenter = oldRoot.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
            using (var serialized = new SerializedObject(presenter))
            {
                serialized.FindProperty("_animator").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Assert.That(_rig.IsIntact, Is.False);
            if (configureAgain)
                _rig.Configure(new AttackPreviewCombination(AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3));
            else _rig.Evaluate(0.1f);
            Assert.That(oldRoot == null, Is.True);
            Assert.That(_rig.IsIntact, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Dispose_ClosesSceneDestroysAvatarAndIsIdempotent()
        {
            Configure(false);
            Scene scene = _rig.Scene;
            GameObject root = _rig.Root;
            Assert.That(EditorSceneManager.IsPreviewScene(scene), Is.True);
            _rig.Dispose();
            Assert.That(root == null, Is.True);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.False);
            Assert.DoesNotThrow(() => _rig.Dispose());
            _rig = null;
        }
    }
}
#endif
