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
            
            // Textura global inyectada en la Etapa 2
            sampler2D _GlobalVisibilityMask;
            // .xy = MaskCamera World Pos, .z = MaskCamera Ortho Size
            float4 _GlobalVisibilityParams; 

            half4 Frag(Varyings input) : SV_Target
            {
                // 1. Leemos el color original de la escena
                half4 screenColor = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                
                // 2. Reconstruimos la posición de mundo exacta para este píxel
                // Z es irrelevante (0.5) porque estamos en una proyección ortográfica plana (2D)
                float3 worldPos = ComputeWorldSpacePosition(input.texcoord, 0.5, UNITY_MATRIX_I_VP);
                
                // 3. Proyectamos World Space hacia el UV de la Mask Camera
                float maskDiameter = _GlobalVisibilityParams.z * 2.0;
                float2 maskUV = (worldPos.xy - _GlobalVisibilityParams.xy) / maskDiameter + 0.5;
                
                // 4. Lógica de Bounds Anti-Smearing
                // Retorna 1.0 si maskUV está dentro de [0, 1], o 0.0 si está fuera.
                half bounds = step(0.0, maskUV.x) * step(maskUV.x, 1.0) * step(0.0, maskUV.y) * step(maskUV.y, 1.0);
                
                // 5. Muestreamos la máscara (Visibilidad: 1.0 = visible, 0.0 = oscuro)
                half maskValue = tex2D(_GlobalVisibilityMask, maskUV).r;
                maskValue *= bounds; // Forzamos 0 absoluto si salimos del área capturada
                
                // 6. Mezclamos el color base (escena) con la oscuridad (niebla)
                // Si maskValue es 1, fogAmount será 0. Si maskValue es 0, fogAmount será el Alpha del Fog Color.
                half fogAmount = (1.0 - maskValue) * _Color.a;
                
                return lerp(screenColor, _Color, fogAmount);
            }
            ENDHLSL
        }
    }
}
