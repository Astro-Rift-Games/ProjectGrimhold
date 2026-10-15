Shader "Hidden/Grimhold/Visibility/VisionFogMask"
{
    Properties
    {
        _MaskWorldRect ("Mask World Rect", Vector) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MaskWorldRect;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 uv = (input.positionOS.xy - _MaskWorldRect.xy) / _MaskWorldRect.zw;
                output.positionCS = float4(uv * 2.0 - 1.0, 0.5, 1.0);
                // Match render-texture sampling with the same world UV on top-origin platforms.
                #if UNITY_UV_STARTS_AT_TOP
                    output.positionCS.y = -output.positionCS.y;
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }
    }
}
