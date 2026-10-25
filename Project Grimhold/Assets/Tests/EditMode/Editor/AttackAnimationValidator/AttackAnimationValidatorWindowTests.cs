#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.EditMode.AttackAnimationValidator
{
    public sealed class AttackAnimationValidatorWindowTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static T Field<T>(AttackAnimationValidatorWindow window, string name) =>
            (T)typeof(AttackAnimationValidatorWindow).GetField(name, PrivateInstance).GetValue(window);

        [Test]
        public void AssetInvalidation_WithStalePresenter_DoesNotLogFromBackgroundTicks()
        {
            int baseline = EditorSceneManager.previewSceneCount;
            var window = EditorWindow.CreateWindow<AttackAnimationValidatorWindow>();
            try
            {
                var rig = Field<AttackPreviewRig>(window, "_rig");
                var presenter = rig.Root.GetComponentInChildren<PlayerAttackVfxPresenter>(true);
                // Model an invalidated preview dependency without touching any project asset or open scene.
                // This is the exact missing-Animator branch that logs in InitializePresentation.
                using (var serialized = new SerializedObject(presenter))
                {
                    serialized.FindProperty("_animator").objectReferenceValue = null;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                typeof(AttackAnimationValidatorWindow).GetMethod("AssetsChanged", PrivateInstance).Invoke(window, null);
                var cell = new AttackPreviewCombination(AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3);
                Field<AttackPreviewFrameCache>(window, "_cache").Request(cell, 1f);

                // Tick must detect/defer unsafe work before any runtime presenter emits an error.
                window.Tick();
                LogAssert.NoUnexpectedReceived();
                window.Tick();
                LogAssert.NoUnexpectedReceived();
                Assert.That(window != null, Is.True);
            }
            finally
            {
                window.Close();
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(baseline));
            }
        }

        [Test]
        public void AssetRefresh_RebuildsPrefabAndAllPixelCachesLazily()
        {
            int baseline = EditorSceneManager.previewSceneCount;
            var window = EditorWindow.CreateWindow<AttackAnimationValidatorWindow>();
            try
            {
                var rig = Field<AttackPreviewRig>(window, "_rig");
                var oldRoot = rig.Root;
                var cell = new AttackPreviewCombination(AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3);
                rig.Configure(cell);
                var markers = Field<AttackPreviewChecks>(window, "_markerChecks");
                var hand = rig.HandRenderers[0];
                markers.DrawnHandWorldPoint(hand);
                var pixels = (System.Collections.IDictionary)typeof(AttackPreviewChecks).GetField("_pixels", PrivateInstance).GetValue(markers);
                Assert.That(pixels.Count, Is.GreaterThan(0));
                var cache = Field<AttackPreviewFrameCache>(window, "_cache");
                cache.Request(cell, 1);
                cache.Tick(1000); // Explicit test-owned work, not a background Test Runner tick.
                var thumbnail = cache.Get(cell.Id, 0);
                Assert.That(thumbnail != null, Is.True);
                typeof(AttackAnimationValidatorWindow).GetMethod("AssetsChanged", PrivateInstance).Invoke(window, null);
                Assert.That(oldRoot != null, Is.True, "The notification itself must not rebuild.");
                typeof(AttackAnimationValidatorWindow).GetMethod("EnsurePreview", PrivateInstance).Invoke(window, null);
                Assert.That(oldRoot == null, Is.True);
                Assert.That(thumbnail == null, Is.True);
                Assert.That(Field<AttackPreviewChecks>(window, "_markerChecks"), Is.Not.SameAs(markers));
                Assert.Throws<System.ObjectDisposedException>(() => markers.DrawnHandWorldPoint(hand));
                var freshPixels = (System.Collections.IDictionary)typeof(AttackPreviewChecks).GetField("_pixels", PrivateInstance)
                    .GetValue(Field<AttackPreviewChecks>(window, "_markerChecks"));
                Assert.That(freshPixels.Count, Is.Zero);
                Assert.That(Field<AttackPreviewFrameCache>(window, "_cache").TextureCount, Is.Zero);
            }
            finally
            {
                window.Close();
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(baseline));
            }
        }

        [Test]
        public void ClosingOwnedWindow_PreservesPreExistingWindowAndItsPreview()
        {
            int originalBaseline = EditorSceneManager.previewSceneCount;
            // Both instances belong to this test. Never discover, reuse or close a user's window.
            var preExisting = EditorWindow.CreateWindow<AttackAnimationValidatorWindow>();
            AttackAnimationValidatorWindow owned = null;
            Texture2D frame = null;
            try
            {
                var originalRig = Field<AttackPreviewRig>(preExisting, "_rig");
                var originalModel = Field<AttackPreviewWindowModel>(preExisting, "_model");
                int baseline = EditorSceneManager.previewSceneCount;
                owned = EditorWindow.CreateWindow<AttackAnimationValidatorWindow>();
                Assert.That(owned, Is.Not.SameAs(preExisting));
                owned.Tick();
                owned.Close();
                owned = null;
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(baseline));
                Assert.That(preExisting != null, Is.True, "Closing the owned window must leave the pre-existing instance open.");
                preExisting.Tick();
                Assert.That(Field<AttackPreviewRig>(preExisting, "_rig"), Is.SameAs(originalRig));
                Assert.That(Field<AttackPreviewWindowModel>(preExisting, "_model"), Is.SameAs(originalModel));
                Assert.That(originalRig.IsIntact, Is.True);
                // Tick is intentionally paused during Test Runner execution. An explicit render
                // additionally proves that the surviving window still owns functional resources.
                var cell = new AttackPreviewCombination(AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3);
                frame = preExisting.RenderDetailFrame(cell, 0.1f, 64);
                Assert.That(frame.width, Is.EqualTo(64));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (frame != null) Object.DestroyImmediate(frame);
                if (owned != null) owned.Close();
                if (preExisting != null) preExisting.Close();
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(originalBaseline));
            }
        }

        [Test]
        public void OpenTickClose_ReleasesPreviewScenesAndPreservesOpenSceneDirtyState()
        {
            int baseline = EditorSceneManager.previewSceneCount;
            var scenes = new Scene[SceneManager.sceneCount];
            var dirty = new bool[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
            {
                scenes[i] = SceneManager.GetSceneAt(i);
                dirty[i] = scenes[i].isDirty;
            }
            AttackAnimationValidatorWindow window = null;
            Camera camera = null;
            RenderTexture target = null;
            Texture2D frame = null;
            try
            {
                window = EditorWindow.CreateWindow<AttackAnimationValidatorWindow>();
                Assert.That(window, Is.Not.Null);
                window.Tick();
                var cell = new AttackPreviewCombination(AttackPreviewCatalogTests.Sword, null, AttackPreviewAction.MainAttack, 3);
                Field<AttackPreviewWindowModel>(window, "_model").Selection = cell;
                frame = window.RenderDetailFrame(cell, 0.1f, 128);
                Assert.That(frame.width, Is.EqualTo(128));
                var renderer = Field<AttackPreviewRenderer>(window, "_renderer");
                camera = (Camera)typeof(AttackPreviewRenderer).GetField("_camera", PrivateInstance).GetValue(renderer);
                target = (RenderTexture)typeof(AttackPreviewRenderer).GetField("_target", PrivateInstance).GetValue(renderer);
                Assert.That(target.IsCreated(), Is.True);
                Assert.That(EditorSceneManager.previewSceneCount, Is.GreaterThan(baseline), "Enabled window must own a preview rig.");
            }
            finally
            {
                if (window != null) window.Close();
                if (frame != null) Object.DestroyImmediate(frame); // Readback ownership belongs to the caller.
                Assert.That(camera == null, Is.True, "Window must destroy its camera.");
                Assert.That(target == null, Is.True, "Window must release/destroy its GPU render target.");
                Assert.That(EditorSceneManager.previewSceneCount, Is.EqualTo(baseline));
                for (int i = 0; i < scenes.Length; i++)
                {
                    Assert.That(scenes[i].IsValid(), Is.True);
                    Assert.That(scenes[i].isDirty, Is.EqualTo(dirty[i]), scenes[i].name);
                }
            }
        }
    }
}
#endif
