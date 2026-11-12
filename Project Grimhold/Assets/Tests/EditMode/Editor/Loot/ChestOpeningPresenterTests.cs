using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.EditMode.Loot
{
    public sealed class ChestOpeningPresenterTests
    {
        [Test]
        public void ProductiveChest_HasOrderedFramesReplaceableSoundAndSharedRenderer()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LootContainer.prefab");
            ChestOpeningPresenter presenter = prefab.GetComponent<ChestOpeningPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(prefab.GetComponent<Fusion.NetworkObject>().NetworkedBehaviours,
                Does.Contain(presenter));
            Fusion.NetworkBehaviour[] baked = prefab.GetComponent<Fusion.NetworkObject>().NetworkedBehaviours;
            Assert.That(baked[baked.Length - 1], Is.SameAs(presenter));
            var serialized = new SerializedObject(presenter);
            Assert.That(serialized.FindProperty("_interactable").objectReferenceValue,
                Is.SameAs(prefab.GetComponent<NetworkLootContainerInteractable>()));
            SpriteRenderer renderer = serialized.FindProperty("_renderer").objectReferenceValue as SpriteRenderer;
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(renderer.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(renderer.sprite, Is.Not.Null);
            Assert.That(renderer.sprite.name, Is.EqualTo("Chest-Open-Sheet_0"));
            Assert.That(AssetDatabase.GetAssetPath(renderer.sprite),
                Is.EqualTo("Assets/Art/Props/Chest-Open-Sheet.png"));
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Art/Props/Chest-Open-Sheet.png");
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(16));
            Assert.That(renderer.sprite.bounds.size.x * renderer.transform.localScale.x,
                Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(renderer.transform.localPosition.y + renderer.sprite.bounds.min.y * renderer.transform.localScale.y,
                Is.EqualTo(-0.5f).Within(0.001f));
            Sprite[] sheetSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Props/Chest-Open-Sheet.png")
                .OfType<Sprite>()
                .ToArray();
            SerializedProperty frames = serialized.FindProperty("_openingFrames");
            Assert.That(frames.arraySize, Is.EqualTo(4));
            for (int index = 0; index < 4; index++)
            {
                Sprite frame = frames.GetArrayElementAtIndex(index).objectReferenceValue as Sprite;
                Assert.That(frame, Is.Not.Null);
                Assert.That(frame.name, Is.EqualTo($"Chest-Open-Sheet_{index}"));
                Assert.That(sheetSprites, Does.Contain(frame));
            }
            Assert.That(serialized.FindProperty("_framesPerSecond").floatValue, Is.EqualTo(12f));
            SerializedProperty sound = serialized.FindProperty("_openingSound");
            Assert.That(sound.FindPropertyRelative("_clips").GetArrayElementAtIndex(0).objectReferenceValue.name,
                Is.EqualTo("Secret door"));
            Assert.That(sound.FindPropertyRelative("_loop").boolValue, Is.False);
        }

        [Test]
        public void MissingSound_DoesNotInvalidateOrBlockAnimation()
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LootContainer.prefab"));
            try
            {
                ChestOpeningPresenter presenter = instance.GetComponent<ChestOpeningPresenter>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ChestOpeningPresenter).GetField("_openingSound", flags).SetValue(presenter, default(CustomClip));
                typeof(ChestOpeningPresenter).GetMethod("Awake", flags).Invoke(presenter, null);
                Assert.That(typeof(ChestOpeningPresenter).GetField("_configurationValid", flags).GetValue(presenter), Is.True);
                var state = (ChestOpeningPresentationState)typeof(ChestOpeningPresenter).GetField("_state", flags).GetValue(presenter);
                state.Initialize(false);
                LogAssert.Expect(LogType.Warning,
                    "ChestOpeningPresenter omitted opening sound: configure a non-looping clip and an available AudioManager.");
                typeof(ChestOpeningPresenter).GetMethod("ObserveOpen", flags).Invoke(presenter, new object[] { true });
                Assert.That(state.IsOpening, Is.True);
                Assert.That(state.Advance(1f, 4, 12f), Is.EqualTo(3));
                Assert.That(state.IsOpening, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void LostPresentationDependency_CompletesPlaybackInsteadOfBlockingUi()
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LootContainer.prefab"));
            try
            {
                ChestOpeningPresenter presenter = instance.GetComponent<ChestOpeningPresenter>();
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var state = (ChestOpeningPresentationState)typeof(ChestOpeningPresenter).GetField("_state", flags).GetValue(presenter);
                state.Initialize(false);
                state.Observe(true);
                typeof(ChestOpeningPresenter).GetField("_initialized", flags).SetValue(presenter, true);
                typeof(ChestOpeningPresenter).GetField("_interactable", flags).SetValue(presenter, null);
                Assert.That(presenter.IsOpening, Is.False);
                Assert.That(typeof(ChestOpeningPresenter).GetMethod("CanObserve", flags).Invoke(presenter, null), Is.False);
                Assert.That(state.IsOpening, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("Assets/Prefabs/LootContainer.prefab")]
        [TestCase("Assets/Prefabs/LockedChest.prefab")]
        public void AnimationFrames_KeepTheClosedChestBottomAnchor(string prefabPath)
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            try
            {
                ChestOpeningPresenter presenter = instance.GetComponent<ChestOpeningPresenter>();
                typeof(ChestOpeningPresenter).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(presenter, null);
                SpriteRenderer renderer = instance.GetComponentInChildren<SpriteRenderer>();
                int frameCount = new SerializedObject(presenter).FindProperty("_openingFrames").arraySize;
                Assert.That(frameCount, Is.EqualTo(4), prefabPath);
                float bottom = renderer.bounds.min.y;
                for (int index = 0; index < frameCount; index++)
                {
                    typeof(ChestOpeningPresenter).GetMethod("ApplyFrame", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(presenter, new object[] { index });
                    Assert.That(renderer.bounds.min.y, Is.EqualTo(bottom).Within(0.001f), $"{prefabPath} frame {index}");
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [TestCase("Assets/Prefabs/NetworkPlayer.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemy.prefab")]
        [TestCase("Assets/Prefabs/Enemies/NetworkEnemyRanged.prefab")]
        public void CorpseEndpoints_DoNotHaveChestPresentation(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null, path);
            Assert.That(prefab.GetComponentInChildren<ChestOpeningPresenter>(true), Is.Null);
        }
    }
}
