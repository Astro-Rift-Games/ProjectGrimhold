#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Actual uGUI rendering in an isolated PlayMode fixture; not a networked Raid/gameplay certification.</summary>
public sealed class GrimholdV16RenderTests
{
    private GameObject _canvasRoot;
    private const string Evidence = "Artifacts/Grimhold-UI-V16-Integration/";

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_canvasRoot != null) Object.Destroy(_canvasRoot);
        yield return null;
    }

    [UnityTest]
    public IEnumerator NativeCircularSpritesRenderAtAllFiveVerticalFillLevels()
    {
        CanvasRoot();
        GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NetworkPlayer.prefab");
        GameObject source = player.transform.Find("LocalGameplayHud/RaidMainHud").gameObject;
        GameObject hud = Object.Instantiate(source, _canvasRoot.transform, false);
        hud.SetActive(true);
        // These are siblings of RaidMainHud, not children: include the authored centre row.
        foreach (string name in new[] { "ActionBarFrame", "RaidCooldownHud", "RaidAbilityHud" })
        {
            Transform sibling = source.transform.parent.Find(name);
            Assert.That(sibling, Is.Not.Null, name);
            GameObject row = Object.Instantiate(sibling.gameObject, _canvasRoot.transform, false);
            row.SetActive(true);
            foreach (CanvasGroup group in row.GetComponentsInChildren<CanvasGroup>(true)) group.alpha = 1f;
        }
        hud.transform.Find("DefeatedIndicator").gameObject.SetActive(false);
        Image health = hud.transform.Find("Health/HealthTrack/HealthFill").GetComponent<Image>();
        Image mana = hud.transform.Find("Mana/ManaTrack/ManaFill").GetComponent<Image>();
        Image stamina = hud.transform.Find("Stamina/StaminaTrack/StaminaFill").GetComponent<Image>();
        Image glass = hud.transform.Find("Health/HealthTrack/V16Glass").GetComponent<Image>();
        Assert.That(health.rectTransform.sizeDelta, Is.EqualTo(new Vector2(72, 72)));
        foreach (float value in new[] { 0f, .25f, .5f, .75f, 1f })
        {
            health.fillAmount = value;
            mana.fillAmount = value;
            stamina.fillAmount = .65f;
            foreach (string resource in new[] { "Health", "Mana" })
                hud.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == resource + "TextValue").text = Mathf.RoundToInt(value * 100) + "/100";
            Canvas.ForceUpdateCanvases();
            yield return null;
            yield return new WaitForEndOfFrame();
            Assert.That(health.rectTransform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(glass.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(glass.enabled, Is.True);
            Assert.That(glass.transform.parent, Is.SameAs(health.transform.parent));
            Capture("isolated-hud-" + Mathf.RoundToInt(value * 100));
        }
    }

    private void CanvasRoot()
    {
        _canvasRoot = new GameObject("Owned V16 UI fixture", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = _canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 100;
        var scaler = _canvasRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1;
        scaler.referencePixelsPerUnit = 100;
        // Overlay-only scenes otherwise retain the previous framebuffer between tests.
        var cameraObject = new GameObject("Owned V16 clear camera", typeof(Camera));
        cameraObject.transform.SetParent(_canvasRoot.transform, false);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(16 / 255f, 23 / 255f, 26 / 255f, 1);
        camera.cullingMask = 0;
    }

    private static void Capture(string name)
    {
        Directory.CreateDirectory(Evidence);
        Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
        try
        {
            Color32 background = screenshot.GetPixel(0, 0);
            RectInt region = name.StartsWith("isolated-hud")
                ? new RectInt(screenshot.width / 2 - 256, 24, 512, 128)
                : new RectInt(screenshot.width / 2 - 337, screenshot.height / 2 - 300, 675, 600);
            int visible = 0;
            for (int y = region.yMin; y < region.yMax; y += 4)
                for (int x = region.xMin; x < region.xMax; x += 4)
                {
                    Color32 pixel = screenshot.GetPixel(x, y);
                    if (!pixel.Equals(background)) visible++;
                }
            Assert.That(visible, Is.GreaterThan(20), name + " must contain rendered UI, not an empty framebuffer.");
            File.WriteAllBytes(Evidence + name + ".png", screenshot.EncodeToPNG());
        }
        finally { Object.Destroy(screenshot); }
    }

}
#endif
