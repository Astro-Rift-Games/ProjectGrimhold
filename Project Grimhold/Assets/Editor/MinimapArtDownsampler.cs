using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Bakes alpha-weighted atlas texels at the minimap's display resolution.</summary>
public static class MinimapArtDownsampler
{
    public const int Factor = 2;

    public static bool TryDownsample(Texture2D source, out Texture2D result, out string error)
    {
        result = null;
        Texture2D original = null;
        try
        {
            if (source == null)
            {
                error = "Minimap source texture is missing.";
                return false;
            }

            Texture2D readable = source;
            string path = AssetDatabase.GetAssetPath(source);
            if (File.Exists(path))
            {
                original = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                if (!ImageConversion.LoadImage(original, File.ReadAllBytes(path)))
                {
                    error = $"Could not decode minimap source texture: {path}.";
                    return false;
                }
                readable = original;
            }
            else if (!source.isReadable)
            {
                error = $"Minimap source texture {source.name} is not readable and has no source file.";
                return false;
            }

            if (readable.width % Factor != 0 || readable.height % Factor != 0)
            {
                error = $"Minimap source texture {source.name} dimensions must be divisible by {Factor}.";
                return false;
            }

            Color32[] pixels = readable.GetPixels32();
            int width = readable.width / Factor;
            int height = readable.height / Factor;
            var averaged = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int alpha = 0, red = 0, green = 0, blue = 0;
                    for (int dy = 0; dy < Factor; dy++)
                    {
                        for (int dx = 0; dx < Factor; dx++)
                        {
                            Color32 pixel = pixels[(y * Factor + dy) * readable.width + x * Factor + dx];
                            alpha += pixel.a;
                            red += pixel.r * pixel.a;
                            green += pixel.g * pixel.a;
                            blue += pixel.b * pixel.a;
                        }
                    }
                    averaged[y * width + x] = new Color32(
                        alpha == 0 ? (byte)0 : RoundByte(red, alpha),
                        alpha == 0 ? (byte)0 : RoundByte(green, alpha),
                        alpha == 0 ? (byte)0 : RoundByte(blue, alpha),
                        RoundByte(alpha, Factor * Factor));
                }
            }

            result = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = source.name + " (Minimap)",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            result.SetPixels32(averaged);
            result.Apply(false, false);
            error = null;
            return true;
        }
        catch (Exception exception)
        {
            UnityEngine.Object.DestroyImmediate(result);
            result = null;
            error = $"Could not downsample minimap texture: {exception.Message}";
            return false;
        }
        finally { UnityEngine.Object.DestroyImmediate(original); }
    }

    private static byte RoundByte(int numerator, int denominator)
    {
        return (byte)((numerator + denominator / 2) / denominator);
    }
}
