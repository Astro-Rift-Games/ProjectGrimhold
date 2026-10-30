using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class MinimapArtDownsamplerTests
{
    [Test]
    public void ReadableAtlasIsAveragedWithAlphaWeightingAndPointSampling()
    {
        var source = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Texture2D result = null;
        try
        {
            source.SetPixels32(new[]
            {
                new Color32(10, 20, 30, 255), new Color32(30, 40, 50, 255), new Color32(255, 0, 0, 0), new Color32(0, 255, 0, 0),
                new Color32(50, 60, 70, 255), new Color32(70, 80, 90, 255), new Color32(20, 40, 60, 255), new Color32(60, 80, 100, 255),
                new Color32(255, 100, 80, 0), new Color32(90, 80, 70, 0), new Color32(1, 2, 3, 255), new Color32(1, 2, 3, 255),
                new Color32(60, 50, 40, 0), new Color32(30, 20, 10, 0), new Color32(2, 3, 4, 255), new Color32(2, 3, 4, 255)
            });
            source.Apply();
            Assert.That(MinimapArtDownsampler.TryDownsample(source, out result, out string error), Is.True, error);
            Assert.That(result.width, Is.EqualTo(2));
            Assert.That(result.height, Is.EqualTo(2));
            Assert.That(result.GetPixels32(), Is.EqualTo(new[]
            {
                new Color32(40, 50, 60, 255), new Color32(40, 60, 80, 128),
                new Color32(0, 0, 0, 0), new Color32(2, 3, 4, 255)
            }));
            Assert.That(result.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(result.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(result.mipmapCount, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(result);
            Object.DestroyImmediate(source);
        }
    }

    [TestCase(3, 4)]
    [TestCase(4, 3)]
    public void OddAtlasDimensionsAreRejected(int width, int height)
    {
        var source = new Texture2D(width, height);
        try
        {
            Assert.That(MinimapArtDownsampler.TryDownsample(source, out Texture2D result, out string error), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(error, Does.Contain("divisible"));
        }
        finally { Object.DestroyImmediate(source); }
    }

}
