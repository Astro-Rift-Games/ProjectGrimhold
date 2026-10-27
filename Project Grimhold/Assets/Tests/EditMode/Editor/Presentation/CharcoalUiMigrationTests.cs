#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode.Presentation
{
    public sealed class CharcoalUiMigrationTests
    {
        private const string Atlas = "Assets/Art/UI/Charcoal/grimhold-ui-charcoal.png";

        [Serializable] private sealed class Manifest { public Entry[] images; }
        [Serializable] private sealed class Entry
        {
            public string asset, fileId, sprite, reason, target;
            public bool migrate;
            public float[] color, sizeDelta, anchorMin, anchorMax, pivot;
            public bool enabled, raycast, preserveAspect;
        }
        [Serializable] private sealed class Slices { public Slice[] sprites; }
        [Serializable] private sealed class Slice { public string name; public Box unityRect; public Border border; }
        [Serializable] private sealed class Box { public float x, y, w, h; }
        [Serializable] private sealed class Border { public float left, right, top, bottom; }

        [Test]
        public void AtlasIsTheExactApprovedArtworkWithUiSafeImportSettings()
        {
            Assert.That(File.Exists(Atlas), Is.True, "Approved charcoal atlas is not imported");
            using var sha = SHA256.Create();
            Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Atlas))).Replace("-", ""),
                Is.EqualTo("1EEE278AEA3666DF37029FBABF7382DC2FB80CD33305BE4FAEBA2B924B05122F"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(Atlas);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Sprite));
            Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.None));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
        }

        [Test]
        public void NativeSpritesMatchAllApprovedRectanglesAndBorders()
        {
            var slices = JsonUtility.FromJson<Slices>(File.ReadAllText("Assets/Art/UI/Charcoal/grimhold-ui-charcoal.slices.json"));
            var sprites = AssetDatabase.LoadAllAssetsAtPath(Atlas).OfType<Sprite>().ToDictionary(s => s.name);
            Assert.That(sprites.Count, Is.EqualTo(42));
            foreach (var slice in slices.sprites)
            {
                Assert.That(sprites.TryGetValue(slice.name, out var sprite), Is.True, slice.name);
                Assert.That(sprite.rect, Is.EqualTo(new Rect(slice.unityRect.x, slice.unityRect.y, slice.unityRect.w, slice.unityRect.h)), slice.name);
                Assert.That(sprite.border, Is.EqualTo(new Vector4(slice.border.left, slice.border.bottom, slice.border.right, slice.border.top)), slice.name);
            }
        }

        [Test]
        public void EveryReviewedOwnerImageUsesItsAssignedSpriteOrPreservesItsExcludedSprite()
        {
            const string path = "Assets/Editor/CharcoalUiMigrationManifest.json";
            Assert.That(File.Exists(path), Is.True, "Explicit reviewed migration manifest required");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path));
            Assert.That(manifest.images.Length, Is.GreaterThan(500));
            foreach (var group in manifest.images.GroupBy(e => e.asset))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                Assert.That(prefab, Is.Not.Null, group.Key);
                var images = prefab.GetComponentsInChildren<Image>(true).ToDictionary(i =>
                {
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(i, out string _, out long id);
                    return id.ToString();
                });
                foreach (var entry in group)
                {
                    Assert.That(images.TryGetValue(entry.fileId, out var image), Is.True, group.Key + ":" + entry.fileId);
                    Assert.That(entry.reason, Is.Not.Empty);
                    Assert.That(image.sprite != null ? image.sprite.name : "", Is.EqualTo(entry.migrate ? entry.target : entry.sprite), entry.asset + ":" + entry.fileId);
                    Assert.That(image.enabled, Is.EqualTo(entry.enabled));
                    Assert.That(image.raycastTarget, Is.EqualTo(entry.raycast));
                    Assert.That(image.preserveAspect, Is.EqualTo(entry.preserveAspect));
                    var rect = image.rectTransform;
                    Assert.That(rect.sizeDelta, Is.EqualTo(new Vector2(entry.sizeDelta[0], entry.sizeDelta[1])));
                    Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(entry.anchorMin[0], entry.anchorMin[1])));
                    Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(entry.anchorMax[0], entry.anchorMax[1])));
                    Assert.That(rect.pivot, Is.EqualTo(new Vector2(entry.pivot[0], entry.pivot[1])));
                    Assert.That(image.color, Is.EqualTo(entry.migrate
                        ? new Color(1, 1, 1, entry.color[3])
                        : new Color(entry.color[0], entry.color[1], entry.color[2], entry.color[3])));
                    if (!entry.migrate) continue;
                    Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(Atlas));
                    Assert.That(image.type, Is.EqualTo(entry.target.StartsWith("scroll_arrow_") || entry.target == "dialogue_continue" ? Image.Type.Simple : Image.Type.Sliced));
                    Assert.That(image.pixelsPerUnitMultiplier, Is.EqualTo(1), entry.asset + ":" + entry.fileId);
                }
            }
        }

        [Test]
        public void ButtonAndInputTargetsHaveCompleteNativeSpriteStatesAndScrollbarsHaveCorrectAxis()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/Editor/CharcoalUiMigrationManifest.json"));
            foreach (var group in manifest.images.Where(e => e.migrate).GroupBy(e => e.asset))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(group.Key);
                var ids = group.ToDictionary(e => e.fileId);
                foreach (var selectable in prefab.GetComponentsInChildren<Selectable>(true))
                {
                    if (selectable.targetGraphic == null) continue;
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(selectable.targetGraphic, out string _, out long id);
                    if (!ids.TryGetValue(id.ToString(), out var entry)) continue;
                    if (entry.target == "slot_empty" || entry.target == "tab_inactive") continue;
                    if (entry.target.StartsWith("scroll_thumb_"))
                    {
                        Assert.That(((Scrollbar)selectable).direction, Is.EqualTo(Scrollbar.Direction.BottomToTop).Or.EqualTo(Scrollbar.Direction.TopToBottom));
                        continue;
                    }
                    Assert.That(selectable.transition, Is.EqualTo(Selectable.Transition.SpriteSwap));
                    var states = selectable.spriteState;
                    bool input = entry.target == "input_normal";
                    Assert.That(states.highlightedSprite.name, Is.EqualTo(input ? "input_focused" : "button_highlighted"));
                    Assert.That(states.pressedSprite.name, Is.EqualTo(input ? "input_focused" : "button_pressed"));
                    Assert.That(states.selectedSprite.name, Is.EqualTo(input ? "input_focused" : "button_selected"));
                    Assert.That(states.disabledSprite.name, Is.EqualTo(input ? "input_normal" : "button_disabled"));
                }
            }
        }

        [Test]
        public void PersistentStateViewsHaveAuthoredSpriteBindingsWithoutRemovingLegacyColors()
        {
            var slot = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RaidInventorySlot.prefab").GetComponent<RaidInventorySlotView>();
            var serializedSlot = new SerializedObject(slot);
            foreach (string field in new[] { "_emptySprite", "_occupiedSprite", "_selectedSprite", "_invalidSprite" })
            {
                Assert.That(serializedSlot.FindProperty(field), Is.Not.Null, field);
                Assert.That(serializedSlot.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            }
            Assert.That(serializedSlot.FindProperty("_normalColor"), Is.Not.Null);
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerUI/TownPlayerMenu.prefab").GetComponent<TownPlayerMenuView>();
            var serializedMenu = new SerializedObject(menu);
            foreach (string field in new[] { "_activeTabSprite", "_inactiveTabSprite", "_disabledTabSprite" })
            {
                Assert.That(serializedMenu.FindProperty(field), Is.Not.Null, field);
                Assert.That(serializedMenu.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            }
        }

        [Test]
        public void EquipmentSlotBackgroundOwnershipTakesPrecedenceOverIconLikeSlotNames()
        {
            foreach (string path in new[] { "Assets/Prefabs/UI/PlayerUI/EquipmentPanel.prefab", "Assets/Prefabs/UI/StashInventory.prefab", "Assets/Prefabs/UI/PlayerUI/RaidInventoryUI.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var view in prefab.GetComponentsInChildren<RaidInventorySlotView>(true))
                {
                    var so = new SerializedObject(view);
                    var image = (Image)so.FindProperty("_background").objectReferenceValue;
                    Assert.That(image, Is.Not.Null, path + ":" + view.name);
                    Assert.That(AssetDatabase.GetAssetPath(image.sprite), Is.EqualTo(Atlas), path + ":" + view.name);
                    Assert.That(so.FindProperty("_emptySprite").objectReferenceValue, Is.Not.Null, path + ":" + view.name);
                }
            }
        }
    }
}
#endif
