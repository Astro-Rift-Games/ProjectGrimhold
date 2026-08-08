using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace ProjectGrimhold.Gameplay.Visibility
{
    public class VisibilityPostProcessPass : ScriptableRenderPass
    {
        private Material _material;

        public VisibilityPostProcessPass(Material material)
        {
            _material = material;
            // Indicamos que necesitamos acceso a los buffers intermedios
            requiresIntermediateTexture = true;
        }

        private class PassData
        {
            public Material material;
            public TextureHandle srcTexture;
        }

        // Implementación exclusiva para URP 17 / RenderGraph API
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (_material == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            TextureHandle activeColor = resourceData.activeColorTexture;
            if (!activeColor.IsValid()) return;

            RTHandle globalMaskHandle = VisibilityMaskRenderer.GlobalMaskRTHandle;
            if (globalMaskHandle == null) return;
            // IMPORTANTE: NO importamos la textura a Render Graph con ImportTexture.
            // Al ser una textura externa generada por otra cámara con un depth buffer de 24 bits,
            // ImportTexture crashearía (no soporta texturas combinadas color/depth).
            // Como solo la leemos (no la escribimos) y su cámara ya terminó de dibujar, podemos consumirla directamente.

            RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0; // Solo post-proceso de color
            
            // Asignamos la textura al material en la fase de setup (CPU).
            _material.SetTexture("_ProcessedMask", globalMaskHandle);

            // Creamos textura temporal dentro del grafo de render
            TextureHandle tempTexture = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_VisibilityTempRT", false);

            // Primer pase: Dibujamos desde ActiveColor hacia TempTexture aplicando el Material
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Visibility Fog Of War", out var passData))
            {
                passData.material = _material;
                passData.srcTexture = activeColor;
                
                builder.UseTexture(activeColor, AccessFlags.Read);
                builder.SetRenderAttachment(tempTexture, 0);

                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    TextureHandle src = data.srcTexture;
                    Blitter.BlitTexture(context.cmd, src, new Vector4(1, 1, 0, 0), data.material, 0);
                });
            }

            // Segundo pase: Copiamos de vuelta desde TempTexture hacia ActiveColor
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Visibility Fog Copy Back", out var passData))
            {
                passData.srcTexture = tempTexture;
                builder.UseTexture(tempTexture, AccessFlags.Read);
                builder.SetRenderAttachment(activeColor, 0);
                
                builder.SetRenderFunc(static (PassData data, RasterGraphContext context) =>
                {
                    RTHandle src = data.srcTexture;
                    Blitter.BlitTexture(context.cmd, src, new Vector4(1, 1, 0, 0), 0.0f, false);
                });
            }
        }

        public void Dispose()
        {
            // Los recursos de RenderGraph se manejan automáticamente
        }
    }
}
