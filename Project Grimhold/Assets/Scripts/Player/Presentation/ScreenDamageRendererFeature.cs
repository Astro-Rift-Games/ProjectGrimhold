using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>Uses URP's RenderGraph color-copy pass, only for the bound local gameplay camera.</summary>
public sealed class ScreenDamageRendererFeature : FullScreenPassRendererFeature
{
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        if (camera.cameraType != CameraType.Game ||
            !camera.TryGetComponent(out ScreenDamageCameraEffect effect) ||
            !effect.TryGetMaterial(camera, out Material material)) return;

        // The base pass captures the material per camera. Do not leave a runtime reference in
        // this shared renderer asset, which also serves menu, preview and remote cameras.
        Material previous = passMaterial;
        try
        {
            passMaterial = material;
            base.AddRenderPasses(renderer, ref renderingData);
        }
        finally { passMaterial = previous; }
    }
}
