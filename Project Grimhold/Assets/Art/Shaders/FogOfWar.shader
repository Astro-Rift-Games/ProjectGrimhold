Shader "Grimhold/Visibility/FogOfWar"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0, 0, 0, 0.85)
        _MaskBlurTexels ("Mask Blur Texels", Range(0, 4)) = 1.5
        [HideInInspector] _ProcessedMask ("Visibility Mask", 2D) = "white" {}
        [Toggle] _FalloffEnabled ("Enable Falloff", Float) = 1
        _FalloffStart ("Falloff Start Distance", Float) = 5.0
        _FalloffEnd ("Falloff End Distance", Float) = 10.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "FogOfWarPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _Color;
            float _MaskBlurTexels;
            float _FalloffEnabled;
            float _FalloffStart;
            float _FalloffEnd;
            
            // Textura global inyectada localmente por el Render Graph
            TEXTURE2D(_ProcessedMask);
            SAMPLER(sampler_ProcessedMask);
            float4 _ProcessedMask_TexelSize; // Poblado automaticamente por Unity
            
            // .xy = MaskCamera World Pos, .z = MaskCamera Ortho Size
            float4 _GlobalVisibilityParams; 
            
            // Origen real de la visión (jugador) expuesto por VisibilityMeshBuilder
            float4 _GlobalVisibilityOrigin;

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. Leemos el color original de la escena
                half4 screenColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                // 2. Reconstruimos la posicion de mundo exacta para este pixel
                // Z es irrelevante (0.5) porque estamos en una proyeccion ortografica plana (2D)
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, 0.5, UNITY_MATRIX_I_VP);
                
                // 3. Proyectamos World Space hacia el UV de la Mask Camera
                float maskDiameter = _GlobalVisibilityParams.z * 2.0;
                float2 maskUV = (worldPos.xy - _GlobalVisibilityParams.xy) / maskDiameter + 0.5;
                
                // 4. Logica de Bounds Anti-Smearing
                // Retorna 1.0 si maskUV esta dentro de [0, 1], o 0.0 si esta fuera.
                half bounds = step(0.0, maskUV.x) * step(maskUV.x, 1.0) * step(0.0, maskUV.y) * step(maskUV.y, 1.0);
                
                // 5. Muestreamos la mascara con suavizado configurable 3x3 box filter
                float2 stepSize = _ProcessedMask_TexelSize.xy * _MaskBlurTexels;
                
                half maskValue = 0.0;
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2(-stepSize.x, -stepSize.y)).r;
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2( 0.0,        -stepSize.y)).r;
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2( stepSize.x, -stepSize.y)).r;
                
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2(-stepSize.x,  0.0)).r;
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2( 0.0,         0.0)).r;
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2( stepSize.x,  0.0)).r;
                
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2(-stepSize.x,  stepSize.y)).r;
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2( 0.0,         stepSize.y)).r;
                maskValue += SAMPLE_TEXTURE2D(_ProcessedMask, sampler_ProcessedMask, maskUV + float2( stepSize.x,  stepSize.y)).r;
                
                maskValue /= 9.0;
                
                maskValue *= bounds; // Forzamos 0 absoluto si salimos del area capturada
                
                // 5.5. Aplicamos Falloff radial (independiente de la mascara)
                float dist = length(worldPos.xy - _GlobalVisibilityOrigin.xy);
                float falloffValue = _FalloffEnabled > 0.5 ? smoothstep(_FalloffEnd, _FalloffStart, dist) : 1.0;
                
                float finalVisibility = maskValue * falloffValue;
                
                // 6. Mezclamos el color base (escena) con la oscuridad (niebla)
                // Si finalVisibility es 1, fogAmount sera 0. Si finalVisibility es 0, fogAmount sera el Alpha del Fog Color.
                half fogAmount = (1.0 - finalVisibility) * _Color.a;
                
                return lerp(screenColor, _Color, fogAmount);
            }
            ENDHLSL
        }
    }
}
