#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.EditMode.Presentation
{
    /// <summary>
    /// Structure guards for the visual-fidelity pass of the Raid HUD: framed minimap, pixel-font pressure
    /// readout, framed interaction prompt and consistent block headers. Presentation structure only.
    /// </summary>
    public sealed class RaidHudStylePassPrefabTests
    {
        private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerUI/LocalGameplayHud.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";
        private const float Tolerance = 0.01f;

        [Test]
        public void MinimapIsWrappedByTheSameOrnateFrameSpriteAsTheBlocks()
        {
            Transform minimap = LoadMinimap();
            Image frame = minimap.Find("Frame")?.GetComponent<Image>();
            Assert.That(frame, Is.Not.Null, "Frame under RaidMinimap");
            Image blockFrame = LoadHud().transform.Find("RaidRightPanel/ObjectivesBlock/Frame").GetComponent<Image>();

            Assert.That(frame.sprite, Is.SameAs(blockFrame.sprite));
            Assert.That(frame.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(frame.raycastTarget, Is.False);
            Assert.That(frame.GetComponent<LayoutElement>().ignoreLayout, Is.True);
            // Drawn after the viewport so the ornate border overlaps the clipped map edge.
            Assert.That(
                frame.transform.GetSiblingIndex(),
                Is.GreaterThan(minimap.Find("Viewport").GetSiblingIndex()));

            RectTransform rect = (RectTransform)frame.transform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void MinimapTitleIsAStaticGoldHeaderBecauseNoZoneNameExistsInTheProjectData()
        {
            Transform minimap = LoadMinimap();
            TMP_Text title = minimap.Find("Title")?.GetComponent<TMP_Text>();
            Assert.That(title, Is.Not.Null, "Title under RaidMinimap");
            TMP_Text header = LoadHud().transform.Find("RaidRightPanel/ObjectivesBlock/Header").GetComponent<TMP_Text>();

            Assert.That(title.text, Is.EqualTo("Mapa"));
            Assert.That(title.font, Is.SameAs(header.font));
            Assert.That(title.color, Is.EqualTo(header.color));
            Assert.That(title.fontSize, Is.EqualTo(header.fontSize));
            Assert.That(title.raycastTarget, Is.False);
        }

        [Test]
        public void MinimapHasAStaticNorthCompassBetweenTheTitleAndTheViewport()
        {
            Transform minimap = LoadMinimap();
            TMP_Text compass = minimap.Find("Compass")?.GetComponent<TMP_Text>();
            Assert.That(compass, Is.Not.Null, "Compass under RaidMinimap");
            TMP_Text header = LoadHud().transform.Find("RaidRightPanel/ObjectivesBlock/Header").GetComponent<TMP_Text>();

            Assert.That(compass.text, Is.EqualTo("N"));
            Assert.That(compass.font, Is.SameAs(header.font));
            Assert.That(compass.raycastTarget, Is.False);
            Assert.That(compass.GetComponent<RectTransform>().localEulerAngles, Is.EqualTo(Vector3.zero));

            // The minimap never rotates (north-up), so the compass must stay out of the clipped viewport
            // where the Sanctuary arrow can sit on the edge.
            Rect root = ResolveRoot(minimap);
            Rect title = ResolveRectIn((RectTransform)minimap.Find("Title"), root);
            Rect compassRect = ResolveRectIn((RectTransform)compass.transform, root);
            Rect viewport = ResolveRectIn((RectTransform)minimap.Find("Viewport"), root);
            Assert.That(compassRect.Overlaps(viewport), Is.False, "compass vs viewport");
            Assert.That(compassRect.Overlaps(title), Is.False, "compass vs title");
            Assert.That(title.Overlaps(viewport), Is.False, "title vs viewport");
        }

        [Test]
        public void MinimapContentFitsInsideItsOwnLayoutSlotSoItNeverOverlapsTheBlocksBelow()
        {
            Transform minimap = LoadMinimap();
            RectTransform rect = (RectTransform)minimap;
            LayoutElement slot = minimap.GetComponent<LayoutElement>();
            Assert.That(slot.preferredHeight, Is.EqualTo(rect.sizeDelta.y).Within(Tolerance));
            Assert.That(slot.minHeight, Is.EqualTo(rect.sizeDelta.y).Within(Tolerance));

            Rect root = ResolveRoot(minimap);
            Rect viewport = ResolveRectIn((RectTransform)minimap.Find("Viewport"), root);
            Rect title = ResolveRectIn((RectTransform)minimap.Find("Title"), root);
            Assert.That(root.Contains(viewport.min) && root.Contains(viewport.max), Is.True, "viewport inside slot");
            Assert.That(root.Contains(title.min) && root.Contains(title.max), Is.True, "title inside slot");
        }

        [Test]
        public void MinimapKeepsItsConfigurationValidAndHasNoZoomOrHelpControls()
        {
            GameObject instance = Object.Instantiate(LoadHud());
            try
            {
                RaidMinimapView view = instance.GetComponentInChildren<RaidMinimapView>(true);
                Assert.That(view.TryValidateConfiguration(out string error), Is.True, error);
                Assert.That(view.ViewportSize.x, Is.GreaterThan(0f));
                Assert.That(view.GetComponentsInChildren<Selectable>(true), Is.Empty);
                Assert.That(view.GetComponentsInChildren<Button>(true), Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void NetworkPlayerResolvesTheFramedMinimap()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            Transform minimap = player.transform.Find("LocalGameplayHud/RaidRightPanel/RaidMinimap");
            Assert.That(minimap, Is.Not.Null);
            Assert.That(minimap.Find("Frame"), Is.Not.Null);
            Assert.That(minimap.Find("Title"), Is.Not.Null);
            Assert.That(minimap.Find("Compass"), Is.Not.Null);
        }

        [Test]
        public void PressureTimerAndPhaseUseTheHudPixelFont()
        {
            GameObject hud = LoadHud();
            TMP_Text header = hud.transform.Find("RaidRightPanel/ObjectivesBlock/Header").GetComponent<TMP_Text>();
            DungeonPressureHudView view = hud.GetComponentInChildren<DungeonPressureHudView>(true);

            Assert.That(view.TimerText.font, Is.SameAs(header.font));
            Assert.That(view.PhaseText.font, Is.SameAs(header.font));
            // The phase colour is driven at runtime by the presenter; the outline keeps it readable.
            Assert.That(view.TimerText.fontSharedMaterial.name, Does.Contain("Outline"));
            Assert.That(view.PhaseText.fontSharedMaterial.name, Does.Contain("Outline"));
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1440f, 1080f)]
        public void PressureReadoutKeepsItsOwnBandAboveTheRightPanelColumn(float width, float height)
        {
            GameObject hud = LoadHud();
            RectTransform root = (RectTransform)hud.transform;
            Vector2 canvas = new Vector2(width, height);
            Rect timer = ResolveRect((RectTransform)hud.transform.Find("DungeonPressureHUD/Timer"), root, canvas);
            Rect phase = ResolveRect((RectTransform)hud.transform.Find("DungeonPressureHUD/Phase"), root, canvas);
            Rect column = ResolveRect((RectTransform)hud.transform.Find("RaidRightPanel"), root, canvas);

            Assert.That(timer.Overlaps(phase), Is.False, "timer vs phase");
            Assert.That(timer.xMax, Is.LessThan(column.xMin), "timer vs right column");
            Assert.That(phase.xMax, Is.LessThan(column.xMin), "phase vs right column");
        }

        [Test]
        public void InteractionPromptHasAFramedBackgroundBehindItsText()
        {
            GameObject hud = LoadHud();
            Transform root = hud.transform.Find("InteractionPrompt");
            Assert.That(root, Is.Not.Null);
            InteractionHudPresenter presenter = hud.GetComponentInChildren<InteractionHudPresenter>(true);
            SerializedObject serialized = new SerializedObject(presenter);
            Assert.That(
                serialized.FindProperty("_promptRoot").objectReferenceValue,
                Is.SameAs(root.gameObject),
                "visibility toggles the strip, not only the text");

            TMP_Text text = (TMP_Text)serialized.FindProperty("_promptText").objectReferenceValue;
            Assert.That(text, Is.Not.Null);
            Assert.That(text.transform.parent, Is.SameAs(root));

            Image background = root.GetComponent<Image>();
            Assert.That(background, Is.Not.Null, "background Image on the prompt root");
            Assert.That(background.sprite, Is.Not.Null);
            Assert.That(background.raycastTarget, Is.False);
            Assert.That(background.color.a, Is.GreaterThan(0.5f));
            Assert.That(background.type, Is.EqualTo(Image.Type.Sliced));
            Assert.That(background.sprite, Is.SameAs(hud.transform.Find("RaidRightPanel/ObjectivesBlock/Frame").GetComponent<Image>().sprite));

            TMP_Text header = hud.transform.Find("RaidRightPanel/ObjectivesBlock/Header").GetComponent<TMP_Text>();
            Assert.That(text.font, Is.SameAs(header.font));
            Assert.That(text.raycastTarget, Is.False);
            Assert.That(root.gameObject.activeSelf, Is.False, "prompt starts hidden");
        }

        [Test]
        public void BlockHeadersAndBodiesShareOneStyle()
        {
            GameObject hud = LoadHud();
            RaidHudView view = hud.GetComponentInChildren<RaidHudView>(true);
            string[] blocks = { "ObjectivesBlock", "SanctuaryBlock", "RitualBlock" };
            TMP_Text[] bodies = { view.QuotaText, view.SanctuaryText, view.ExtractionText };

            TMP_Text first = hud.transform.Find("RaidRightPanel/" + blocks[0] + "/Header").GetComponent<TMP_Text>();
            for (int i = 0; i < blocks.Length; i++)
            {
                TMP_Text header = hud.transform.Find("RaidRightPanel/" + blocks[i] + "/Header").GetComponent<TMP_Text>();
                Assert.That(header.color, Is.EqualTo(first.color), blocks[i] + " header colour");
                Assert.That(header.fontSize, Is.EqualTo(first.fontSize), blocks[i] + " header size");
                Assert.That(header.alignment, Is.EqualTo(first.alignment), blocks[i] + " header alignment");
                Assert.That(bodies[i].color, Is.EqualTo(bodies[0].color), blocks[i] + " body colour");
                Assert.That(bodies[i].fontSize, Is.EqualTo(bodies[0].fontSize), blocks[i] + " body size");
                Assert.That(bodies[i].alignment, Is.EqualTo(bodies[0].alignment), blocks[i] + " body alignment");
            }
        }

        [TestCase("ObjectivesBlock")]
        [TestCase("SanctuaryBlock")]
        [TestCase("RitualBlock")]
        public void BlockHeadersAreCenteredLikeTheConceptAndTheMinimapTitle(string blockName)
        {
            TMP_Text header = LoadHud().transform.Find("RaidRightPanel/" + blockName + "/Header").GetComponent<TMP_Text>();
            TMP_Text title = LoadMinimap().Find("Title").GetComponent<TMP_Text>();

            Assert.That(header.horizontalAlignment, Is.EqualTo(HorizontalAlignmentOptions.Center));
            Assert.That(header.horizontalAlignment, Is.EqualTo(title.horizontalAlignment));
        }

        private static Transform LoadMinimap()
        {
            Transform minimap = LoadHud().transform.Find("RaidRightPanel/RaidMinimap");
            Assert.That(minimap, Is.Not.Null);
            return minimap;
        }

        private static GameObject LoadHud()
        {
            GameObject hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            Assert.That(hud, Is.Not.Null);
            return hud;
        }

        private static Rect ResolveRoot(Transform minimap)
        {
            return new Rect(Vector2.zero, ((RectTransform)minimap).sizeDelta);
        }

        private static Rect ResolveRectIn(RectTransform target, Rect parentRect)
        {
            Vector2 anchorMin = parentRect.min + Vector2.Scale(target.anchorMin, parentRect.size);
            Vector2 anchorMax = parentRect.min + Vector2.Scale(target.anchorMax, parentRect.size);
            Vector2 size = (anchorMax - anchorMin) + target.sizeDelta;
            Vector2 pivotPoint = anchorMin + Vector2.Scale(anchorMax - anchorMin, target.pivot) + target.anchoredPosition;
            // For a stretched rect the pivot offset above is relative to the anchor box, which is what Unity does.
            Vector2 min = pivotPoint - Vector2.Scale(size, target.pivot);
            return new Rect(min, size);
        }

        private static Rect ResolveRect(RectTransform target, RectTransform root, Vector2 rootSize)
        {
            Rect parent = target.parent == root || target.parent == null
                ? new Rect(Vector2.zero, rootSize)
                : ResolveRect((RectTransform)target.parent, root, rootSize);
            return ResolveRectIn(target, parent);
        }
    }
}
#endif
