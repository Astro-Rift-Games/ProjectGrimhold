using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace ProjectGrimhold.Gameplay.Visibility
{
    public class VisibilityPostProcessFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class FeatureSettings
        {
            [Tooltip("Material que contiene el shader FogOfWar.")]
            public Material Material;
            
            [Tooltip("Momento en el que se inyecta el shader. BeforeRenderingPostProcessing es recomendado para juegos 2D.")]
            public RenderPassEvent PassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        public FeatureSettings Settings = new FeatureSettings();
        private VisibilityPostProcessPass _pass;

        public override void Create()
        {
            if (Settings.Material == null) return;
            _pass = new VisibilityPostProcessPass(Settings.Material);
            _pass.renderPassEvent = Settings.PassEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (Settings.Material == null || _pass == null) return;
            
            // Evitamos ejecutar el efecto en cámaras de preview (como el Inspector)
            if (renderingData.cameraData.cameraType == CameraType.Preview || 
                renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            _pass?.Dispose();
        }
    }
}
