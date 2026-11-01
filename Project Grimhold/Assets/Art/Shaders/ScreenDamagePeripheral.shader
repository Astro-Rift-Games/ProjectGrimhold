Shader "Grimhold/Screen Damage Peripheral"
{
    Properties
    {
        _EffectWeight ("Effect Weight", Range(0, 1)) = 0
        _BlurPixels ("Blur Pixels", Float) = 1.1
        _ChromaticPixels ("Chromatic Pixels", Float) = 0.65
        _ClearCenter ("Clear Center", Float) = 0.6
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Peripheral Blur And Chromatic Separation"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _EffectWeight, _BlurPixels, _ChromaticPixels, _ClearCenter, _ViewportAspect;
            float4 _AvatarClear, _CursorClear;

            float OutsideProtectedArea(float2 uv, float4 area)
            {
                float aspect = max(_ViewportAspect, 0.001);
                float2 shortAxisScale = float2(max(aspect, 1), max(1 / aspect, 1));
                float distanceFromArea = length((uv - area.xy) * shortAxisScale);
                return smoothstep(area.z, area.z + area.w, distanceFromArea);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // Rectangular periphery, never a circular vignette or global image blur.
                float edge = max(abs(uv.x - 0.5), abs(uv.y - 0.5)) * 2;
                // URP's blit vertex already supplies the normalized viewport convention.
                // A second platform Y flip would mirror the avatar/cursor exclusion zones.
                float2 viewportUV = uv;
                float mask = smoothstep(_ClearCenter, 1, edge) * saturate(_EffectWeight);
                mask *= OutsideProtectedArea(viewportUV, _AvatarClear);
                mask *= OutsideProtectedArea(viewportUV, _CursorClear);
                if (mask <= 0) return original;

                float2 texel = abs(_BlitTexture_TexelSize.xy);
                float2 radius = texel * _BlurPixels * mask;
                half3 blurred = original.rgb * 0.4;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(radius.x, 0)).rgb * 0.15;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(radius.x, 0)).rgb * 0.15;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0, radius.y)).rgb * 0.15;
                blurred += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - float2(0, radius.y)).rgb * 0.15;
                float2 direction = uv - 0.5;
                direction /= max(length(direction), 0.001);
                float2 split = direction * texel * _ChromaticPixels * mask;
                half3 chromatic = half3(
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + split).r,
                    original.g,
                    SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv - split).b);
                // Both samples retain HDR range without subtractive dark halos at contrast edges.
                return half4(lerp(original.rgb, lerp(blurred, chromatic, 0.5), mask), original.a);
            }
            ENDHLSL
        }
    }
}
