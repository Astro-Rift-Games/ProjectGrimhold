using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public sealed class RaidMinimapMotionTests
{
    private GameObject _root;
    private Canvas _canvas;
    private RectTransform _viewport;
    private RectTransform _map;
    private Image _localMarker;
    private RaidMinimapView _view;

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
    }

    [TestCase(1f, 0.25f, -0.25f)]
    [TestCase(0.73f, 0.25f, -0.25f)]
    [TestCase(1.37f, -2.75f, 3.25f)]
    public void MapPivotSnapsToPhysicalPixelsIncludingFractionalViewportOrigin(
        float canvasScale, float x, float y)
    {
        CreateView(canvasScale);
        Vector2 requested = new Vector2(x, y);
        Vector2 desiredScreenPosition = ScreenPositionFor(requested);

        _view.PresentMapPosition(requested);

        Vector2 actualScreenPosition = ScreenPosition();
        AssertIntegerPixel(actualScreenPosition.x);
        AssertIntegerPixel(actualScreenPosition.y);
        Assert.That(Mathf.Abs(actualScreenPosition.x - desiredScreenPosition.x), Is.LessThanOrEqualTo(0.5001f));
        Assert.That(Mathf.Abs(actualScreenPosition.y - desiredScreenPosition.y), Is.LessThanOrEqualTo(0.5001f));
        Assert.That(_map.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(_map.localScale, Is.EqualTo(Vector3.one));
        Assert.That(_canvas.pixelPerfect, Is.False, "Snapping this map must not change the entire HUD Canvas.");
    }

    [TestCase(0.73f)]
    [TestCase(1f)]
    [TestCase(1.37f)]
    public void FractionalPanKeepsTheSameSamplingPhaseAndUsesIntegerScreenSteps(float canvasScale)
    {
        CreateView(canvasScale);
        Vector2 origin = ScreenPositionFor(Vector2.zero);
        Vector2 integerOrigin = new Vector2(Mathf.Round(origin.x), Mathf.Round(origin.y));
        Vector2 firstRequest = (integerOrigin + new Vector2(0.1f, -0.1f) - origin) / canvasScale;
        Vector2 tinyPan = new Vector2(0.2f, -0.2f) / canvasScale;
        _view.PresentMapPosition(firstRequest);
        Vector2 first = ScreenPosition();
        _view.PresentMapPosition(firstRequest + tinyPan);
        Vector2 second = ScreenPosition();
        Assert.That(Vector2.Distance(first, second), Is.LessThan(0.0001f));

        Vector2 previous = second;
        for (int i = 1; i <= 12; i++)
        {
            Vector2 request = firstRequest + new Vector2(i * 0.25f, -i * 0.25f) / canvasScale;
            Vector2 desired = ScreenPositionFor(request);
            _view.PresentMapPosition(request);
            Vector2 current = ScreenPosition();
            AssertIntegerPixel(current.x);
            AssertIntegerPixel(current.y);
            AssertIntegerPixel(current.x - previous.x);
            AssertIntegerPixel(current.y - previous.y);
            Assert.That(Mathf.Abs(current.x - desired.x), Is.LessThanOrEqualTo(0.5001f));
            Assert.That(Mathf.Abs(current.y - desired.y), Is.LessThanOrEqualTo(0.5001f));
            previous = current;
        }
    }

    [Test]
    public void MapSnappingLeavesLocalPlayerCenteredAndNorthUp()
    {
        CreateView(0.73f);
        _map.localRotation = Quaternion.Euler(0, 0, 38);
        _view.PresentMapPosition(new Vector2(3.25f, -7.75f));
        _view.PresentLocalMarker();

        Assert.That(_localMarker.rectTransform.anchoredPosition, Is.EqualTo(Vector2.zero));
        Assert.That(_localMarker.rectTransform.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(_map.localRotation, Is.EqualTo(Quaternion.identity));
        Assert.That(_localMarker.enabled, Is.True);
    }

    [Test]
    public void ViewWithoutCanvasPreservesRequestedLocalPosition()
    {
        CreateView(1f, false);
        Vector2 requested = new Vector2(0.25f, -4.75f);
        _view.PresentMapPosition(requested);

        Assert.That(_map.anchoredPosition, Is.EqualTo(requested));
        Assert.That(_map.localRotation, Is.EqualTo(Quaternion.identity));
    }

    [TestCase(0.73f)]
    [TestCase(1f)]
    [TestCase(1.37f)]
    public void OriginalTileArtKeepsIdenticalRasterForSubpixelPanAndTranslatesByWholePixels(float canvasScale)
    {
        CreateView(canvasScale);
        MinimapLayout layout = AssetDatabase.LoadAssetAtPath<MinimapLayout>(MinimapLayoutGenerator.LayoutAssetPath);
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout.TryValidate(out string error), Is.True, error);
        Shader shader = Shader.Find("UI/Default");
        Assert.That(shader, Is.Not.Null);
        var material = new Material(shader);
        var meshes = new List<Mesh>();
        var target = new RenderTexture(256, 256, 0, RenderTextureFormat.ARGB32);
        var readback = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        RenderTexture previousTarget = RenderTexture.active;
        try
        {
            target.Create();
            for (int i = 0; i < layout.ArtBatchCount; i++)
                meshes.Add(CreateProductionGeometry(layout.GetArtBatch(i)));

            Vector2 origin = ScreenPositionFor(Vector2.zero);
            Vector2 integerOrigin = new Vector2(Mathf.Round(origin.x), Mathf.Round(origin.y));
            Vector2 firstRequest = (integerOrigin + new Vector2(0.1f, -0.1f) - origin) / canvasScale;
            _view.PresentMapPosition(firstRequest);
            Vector2 referencePivot = ScreenPosition();
            Color32[] first = Rasterize(layout, meshes, material, target, readback, referencePivot);
            long light = 0;
            foreach (Color32 pixel in first)
                light += pixel.r + pixel.g + pixel.b;
            Assert.That(light, Is.GreaterThan(1000), "The raster comparison must contain actual authored tile art.");

            _view.PresentMapPosition(firstRequest + new Vector2(0.2f, -0.2f) / canvasScale);
            Color32[] tinyPan = Rasterize(layout, meshes, material, target, readback, referencePivot);
            Assert.That(tinyPan, Is.EqualTo(first), "A pan inside the same physical-pixel bucket must not change tile samples.");

            _view.PresentMapPosition(firstRequest + Vector2.right / canvasScale);
            Color32[] onePixelPan = Rasterize(layout, meshes, material, target, readback, referencePivot);
            var expectedInterior = new Color32[(target.width - 1) * target.height];
            var actualInterior = new Color32[expectedInterior.Length];
            for (int y = 0; y < target.height; y++)
            for (int x = 1; x < target.width; x++)
            {
                int interiorIndex = y * (target.width - 1) + x - 1;
                expectedInterior[interiorIndex] = first[y * target.width + x - 1];
                actualInterior[interiorIndex] = onePixelPan[y * target.width + x];
            }
            Assert.That(actualInterior, Is.EqualTo(expectedInterior),
                "A one-pixel pan must translate the original tile samples without changing their phase.");
        }
        finally
        {
            RenderTexture.active = previousTarget;
            foreach (Mesh mesh in meshes)
                Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(readback);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }

    private static Mesh CreateProductionGeometry(MinimapMeshBatch batch)
    {
        var vertices = new Vector3[batch.VertexCount];
        var uvs = new Vector2[batch.VertexCount];
        var colors = new Color32[batch.VertexCount];
        var indices = new int[batch.IndexCount];
        for (int i = 0; i < batch.VertexCount; i++)
        {
            // Same scale, UVs, colours and painter order as RaidMinimapBatchGraphic.
            vertices[i] = batch.GetPosition(i) * 4f;
            uvs[i] = batch.GetUv(i);
            colors[i] = batch.GetColor(i);
        }
        for (int i = 0; i < batch.IndexCount; i++)
            indices[i] = batch.GetIndex(i);
        return new Mesh { vertices = vertices, uv = uvs, colors32 = colors, triangles = indices };
    }

    private Color32[] Rasterize(MinimapLayout layout, List<Mesh> meshes, Material material,
        RenderTexture target, Texture2D readback, Vector2 referencePivot)
    {
        Vector2 pixelOffset = ScreenPosition() - referencePivot + Vector2.one * 128f;
        Matrix4x4 model = Matrix4x4.TRS(pixelOffset, Quaternion.identity, Vector3.one * _canvas.scaleFactor);
        using (var commands = new CommandBuffer { name = "IsolatedMinimapRasterRegression" })
        {
            Matrix4x4 previousView = Shader.GetGlobalMatrix("unity_MatrixV");
            Matrix4x4 previousProjection = Shader.GetGlobalMatrix("glstate_matrix_projection");
            commands.SetRenderTarget(target);
            commands.ClearRenderTarget(false, true, Color.clear);
            commands.SetViewProjectionMatrices(Matrix4x4.identity,
                GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0, 256, 0, 256, -1, 1), true));
            var properties = new MaterialPropertyBlock();
            for (int i = 0; i < meshes.Count; i++)
            {
                properties.SetTexture("_MainTex", layout.GetArtBatch(i).Texture);
                commands.DrawMesh(meshes[i], model, material, 0, -1, properties);
            }
            commands.SetViewProjectionMatrices(previousView, previousProjection);
            Graphics.ExecuteCommandBuffer(commands);
        }
        RenderTexture.active = target;
        readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        readback.Apply(false, false);
        return readback.GetPixels32();
    }

    private void CreateView(float canvasScale, bool withCanvas = true)
    {
        _root = new GameObject("IsolatedMinimapMotion", typeof(RectTransform));
        if (withCanvas)
        {
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.pixelPerfect = false;
            _canvas.scaleFactor = canvasScale;
        }

        _viewport = CreateRect("Viewport", _root.transform);
        _viewport.sizeDelta = new Vector2(124f, 124f);
        // Includes the phase inherited from HUD anchors/layout, not just map offset.
        _viewport.anchoredPosition = new Vector2(37.37f, -23.29f);
        _map = CreateRect("Map", _viewport);
        _map.sizeDelta = new Vector2(800f, 600f);
        _localMarker = CreateRect("LocalMarker", _viewport).gameObject.AddComponent<Image>();
        _view = _root.AddComponent<RaidMinimapView>();
        SetField("_mapRect", _map);
        SetField("_localMarker", _localMarker);
        Canvas.ForceUpdateCanvases();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var owner = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)owner.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
        return rect;
    }

    private Vector2 ScreenPositionFor(Vector2 requested)
    {
        _map.anchoredPosition = requested;
        return ScreenPosition();
    }

    private Vector2 ScreenPosition()
    {
        return RectTransformUtility.WorldToScreenPoint(null, _map.position);
    }

    private static void AssertIntegerPixel(float value)
    {
        Assert.That(value, Is.EqualTo(Mathf.Round(value)).Within(0.0001f));
    }

    private void SetField(string name, object value)
    {
        typeof(RaidMinimapView).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(_view, value);
    }
}
