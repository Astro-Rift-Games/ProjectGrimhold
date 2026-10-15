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
        Pass
        {
            Name "Blur"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_VisionFogMaskSource);
            SAMPLER(sampler_VisionFogMaskSource);
            float2 _VisionFogBlurStep;
            float _VisionFogMaskSourceLod;

            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(uint id : SV_VertexID)
            {
                Varyings output;
                float2 uv = float2((id << 1) & 2, id & 2);
                output.uv = uv;
                output.positionCS = float4(uv * 2.0 - 1.0, 0.5, 1.0);
                // Match pass 0 so each written UV is read back at the same UV.
                #if UNITY_UV_STARTS_AT_TOP
                    output.positionCS.y = -output.positionCS.y;
                #endif
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float value = SAMPLE_TEXTURE2D_LOD(_VisionFogMaskSource, sampler_VisionFogMaskSource, input.uv, _VisionFogMaskSourceLod).r * 0.2270270270;
                const float weights[4] = { 0.1945945946, 0.1216216216, 0.0540540541, 0.0162162162 };
                [unroll]
                for (int i = 1; i <= 4; i++)
                {
                    float2 offset = _VisionFogBlurStep * i;
                    value += (SAMPLE_TEXTURE2D_LOD(_VisionFogMaskSource, sampler_VisionFogMaskSource, input.uv + offset, _VisionFogMaskSourceLod).r
                        + SAMPLE_TEXTURE2D_LOD(_VisionFogMaskSource, sampler_VisionFogMaskSource, input.uv - offset, _VisionFogMaskSourceLod).r) * weights[i - 1];
                }
                return half4(value, value, value, value);
            }
            ENDHLSL
        }
    }
}
