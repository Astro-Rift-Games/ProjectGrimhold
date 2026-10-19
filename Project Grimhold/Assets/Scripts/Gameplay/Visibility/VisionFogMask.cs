using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>Renders the viewer's occluded clear polygon into a low-resolution mask texture for the vision fog; presentation only.</summary>
    public sealed class VisionFogMask : IDisposable
    {
        private static readonly int MaskWorldRectId = Shader.PropertyToID("_MaskWorldRect");
        private static readonly int MaskSourceId = Shader.PropertyToID("_VisionFogMaskSource");
        private static readonly int BlurStepId = Shader.PropertyToID("_VisionFogBlurStep");
        private static readonly int MaskSourceLodId = Shader.PropertyToID("_VisionFogMaskSourceLod");
        private RenderTexture _polygonTexture;
        private RenderTexture _blurA;
        private RenderTexture _blurB;
        private readonly Material _material;
        private readonly Mesh _mesh;
        private readonly CommandBuffer _commandBuffer;
        private readonly MaterialPropertyBlock _block;
        private float[] _angles;
        private bool[] _inCone;
        private Vector3[] _vertices;
        private int[] _triangles;
        private int _rayCount;
        private bool _disposed;

        public RenderTexture Texture { get; private set; }
        public Vector4 WorldRect { get; private set; }

        public VisionFogMask(Material maskMaterial)
        {
            if (maskMaterial == null)
            {
                throw new ArgumentNullException(nameof(maskMaterial));
            }

            _material = maskMaterial;
            _mesh = new Mesh { name = "VisionFogMask", hideFlags = HideFlags.HideAndDontSave };
            _mesh.MarkDynamic();
            _commandBuffer = new CommandBuffer { name = "VisionFogMask" };
            _block = new MaterialPropertyBlock();
        }

        public void Render(in VisionQuery query, Vector2 facing, float coneAngleDegrees,
            Rect worldRect, int textureWidth, int textureHeight, VisionFogSettings settings)
        {
            int capacity = VisionFogFanRules.MaxRayCount(settings.ConeRayStepDegrees, settings.OuterRayStepDegrees);
            if (_angles == null || capacity > _angles.Length)
            {
                _angles = new float[capacity];
                _inCone = new bool[capacity];
                _vertices = new Vector3[capacity + 1];
                _triangles = new int[capacity * 3];
            }

            textureWidth = Mathf.Max(16, textureWidth);
            textureHeight = Mathf.Max(16, textureHeight);
            if (_polygonTexture == null || _polygonTexture.width != textureWidth || _polygonTexture.height != textureHeight)
            {
                ReleaseTexture();
                RenderTextureFormat format = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                    ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
                _polygonTexture = new RenderTexture(textureWidth, textureHeight, 0, format, RenderTextureReadWrite.Linear)
                {
                    name = "VisionFogMask",
                    useMipMap = true,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                _polygonTexture.Create();
            }

            int count = VisionFogFanRules.BuildRayAngles(Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg,
                coneAngleDegrees, settings.ConeRayStepDegrees, settings.OuterRayStepDegrees, _angles, _inCone);
            Vector2 origin = query.Origin;
            float far = Mathf.Max(
                Vector2.Distance(origin, new Vector2(worldRect.xMin, worldRect.yMin)),
                Vector2.Distance(origin, new Vector2(worldRect.xMax, worldRect.yMin)));
            far = Mathf.Max(far, Vector2.Distance(origin, new Vector2(worldRect.xMin, worldRect.yMax)));
            far = Mathf.Max(far, Vector2.Distance(origin, new Vector2(worldRect.xMax, worldRect.yMax)));
            _vertices[0] = new Vector3(origin.x, origin.y, 0f);
            for (int i = 0; i < count; i++)
            {
                float radians = _angles[i] * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                float maxLength = _inCone[i] ? Mathf.Max(far, query.NearRadius) : query.NearRadius;
                float distance = query.CastOcclusion(direction, maxLength);
                if (distance < maxLength)
                {
                    distance = Mathf.Min(distance + settings.WallRevealDepth, maxLength);
                }

                Vector2 point = origin + direction * distance;
                _vertices[i + 1] = new Vector3(point.x, point.y, 0f);
            }

            if (_rayCount != count)
            {
                _mesh.Clear();
                _mesh.SetVertices(_vertices, 0, count + 1);
                for (int i = 0; i < count; i++)
                {
                    _triangles[i * 3] = 0;
                    _triangles[i * 3 + 1] = i + 1;
                    _triangles[i * 3 + 2] = (i + 1) % count + 1;
                }

                _mesh.SetTriangles(_triangles, 0, count * 3, 0, false);
                _rayCount = count;
            }
            else
            {
                _mesh.SetVertices(_vertices, 0, count + 1);
            }

            WorldRect = new Vector4(worldRect.xMin, worldRect.yMin, worldRect.width, worldRect.height);
            _commandBuffer.Clear();
            _commandBuffer.SetRenderTarget(_polygonTexture);
            _commandBuffer.ClearRenderTarget(false, true, Color.clear);
            _block.SetVector(MaskWorldRectId, WorldRect);
            _commandBuffer.DrawMesh(_mesh, Matrix4x4.identity, _material, 0, 0, _block);
            _commandBuffer.GenerateMips(_polygonTexture);
            Texture = _polygonTexture;
            if (settings.EdgeSoftness > 0f)
            {
                float radiusTexelsX = settings.EdgeSoftness / worldRect.width * textureWidth;
                float radiusTexelsY = settings.EdgeSoftness / worldRect.height * textureHeight;
                float radiusTexels = Mathf.Max(radiusTexelsX, radiusTexelsY);
                int level = radiusTexels < 4f ? 0 : Mathf.Clamp(
                    Mathf.FloorToInt(Mathf.Log(radiusTexels / 4f, 2f)), 0, _polygonTexture.mipmapCount - 1);
                int lowWidth = Mathf.Max(1, textureWidth >> level);
                int lowHeight = Mathf.Max(1, textureHeight >> level);
                if (_blurA == null || _blurA.width != lowWidth || _blurA.height != lowHeight)
                {
                    ReleaseTexture(ref _blurA);
                    ReleaseTexture(ref _blurB);
                    _blurA = CreateBlurTexture(lowWidth, lowHeight, "VisionFogMaskBlurA");
                    _blurB = CreateBlurTexture(lowWidth, lowHeight, "VisionFogMaskBlurB");
                }

                float mipScale = 1 << level;
                _commandBuffer.SetRenderTarget(_blurA);
                _commandBuffer.SetGlobalTexture(MaskSourceId, _polygonTexture);
                _commandBuffer.SetGlobalFloat(MaskSourceLodId, level);
                _commandBuffer.SetGlobalVector(BlurStepId, new Vector4(radiusTexelsX / mipScale / 4f / lowWidth, 0f, 0f, 0f));
                _commandBuffer.DrawProcedural(Matrix4x4.identity, _material, 1, MeshTopology.Triangles, 3);
                _commandBuffer.SetRenderTarget(_blurB);
                _commandBuffer.SetGlobalTexture(MaskSourceId, _blurA);
                _commandBuffer.SetGlobalFloat(MaskSourceLodId, 0f);
                _commandBuffer.SetGlobalVector(BlurStepId, new Vector4(0f, radiusTexelsY / mipScale / 4f / lowHeight, 0f, 0f));
                _commandBuffer.DrawProcedural(Matrix4x4.identity, _material, 1, MeshTopology.Triangles, 3);
                Texture = _blurB;
            }
            Graphics.ExecuteCommandBuffer(_commandBuffer);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _commandBuffer.Release();
            ReleaseTexture();
            DestroyObject(_mesh);
        }

        private RenderTexture CreateBlurTexture(int width, int height, string name)
        {
            var texture = new RenderTexture(width, height, 0, _polygonTexture.format, RenderTextureReadWrite.Linear)
            {
                name = name,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        private void ReleaseTexture()
        {
            Texture = null;
            ReleaseTexture(ref _polygonTexture);
            ReleaseTexture(ref _blurA);
            ReleaseTexture(ref _blurB);
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            DestroyObject(texture);
            texture = null;
        }

        private static void DestroyObject(Object target)
        {
            if (Application.isPlaying)
            {
                Object.Destroy(target);
            }
            else
            {
                Object.DestroyImmediate(target);
            }
        }
    }
}
