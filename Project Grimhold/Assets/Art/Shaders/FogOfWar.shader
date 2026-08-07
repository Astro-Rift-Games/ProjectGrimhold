Shader "Grimhold/Visibility/FogOfWar"
{
    Properties
    {
        _Color ("Fog Color", Color) = (0, 0, 0, 0.85)
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

            // Variables inyectadas por el VisibilityMaskRenderer
            TEXTURE2D(_GlobalVisibilityMask);
            SAMPLER(sampler_GlobalVisibilityMask);
            float4 _GlobalVisibilityParams; // xy = MaskCameraPos, z = MaskCamera.orthoSize

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. Obtenemos el color original de la pantalla
                half4 screenColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                // 2. Reconstruimos la posición del mundo del pixel actual (Sólo válido para cámara principal Ortográfica)
                // input.texcoord va de 0 a 1. Restando 0.5 lo centramos (-0.5 a 0.5).
                // unity_OrthoParams.x es Width/2. unity_OrthoParams.y es Height/2 (Size).
                float2 worldPos = _WorldSpaceCameraPos.xy + (input.texcoord - 0.5) * 2.0 * float2(unity_OrthoParams.x, unity_OrthoParams.y);

                // 3. Transformamos el World Space a coordenadas UV de la textura de la Máscara
                float maskTotalSize = _GlobalVisibilityParams.z * 2.0;
                float2 maskUV = (worldPos - _GlobalVisibilityParams.xy) / maskTotalSize + 0.5;

                // 4. Leemos el valor de iluminación de la máscara (0 = Oscuridad, 1 = Iluminado)
                half maskValue = SAMPLE_TEXTURE2D(_GlobalVisibilityMask, sampler_GlobalVisibilityMask, maskUV).r;

                // Si las coordenadas UV caen fuera de la textura, forzamos oscuridad
                if (maskUV.x < 0 || maskUV.x > 1 || maskUV.y < 0 || maskUV.y > 1) 
                {
                    maskValue = 0.0;
                }

                // 5. Componemos el resultado. 
                // lerp(Oscuridad, ColorOriginal, NivelDeLuz)
                // Cuando maskValue es 1, se ve el juego normal. Cuando es 0, se aplica _Color (con su propio alpha)
                half4 finalFogColor = lerp(screenColor, _Color, _Color.a);
                return lerp(finalFogColor, screenColor, maskValue);
            }
            ENDHLSL
        }
    }
}
