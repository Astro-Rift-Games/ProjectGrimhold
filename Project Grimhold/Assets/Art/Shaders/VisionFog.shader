// Local presentation only: blurred occlusion mask with a smooth, inward-only clear fade and noise erosion.
Shader "Grimhold/Visibility/VisionFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.16, 0.17, 0.2, 0.93)
        _InsideDensity ("Inside Density", Range(0, 1)) = 0.06
        _NoiseScale ("Noise Scale", Float) = 0.35
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.3
        _WindVelocity ("Wind Velocity", Vector) = (0.15, 0.05, 0, 0)
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16
        _VisionMask ("Vision Mask", 2D) = "black" {}
        _MaskWorldRect ("Mask World Rect", Vector) = (0, 0, 1, 1)
        _EdgeBreakup ("Edge Breakup", Range(0, 0.45)) = 0.25
        _FarFogStart ("Far Fog Start", Float) = 8
        _FarFogEnd ("Far Fog End", Float) = 16
        _FarFogDensity ("Far Fog Density", Range(0, 1)) = 0.35
        _VisionOrigin ("Vision Origin", Vector) = (0, 0, 0, 0)
        _VisionClearStrength ("Vision Clear Strength", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            // Sorting layers place this 2D overlay above the world, irrespective of world depth.
            ZTest Always
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_VisionMask);
            SAMPLER(sampler_VisionMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _WindVelocity;
                float4 _VisionOrigin;
                float4 _MaskWorldRect;
                float _InsideDensity;
                float _NoiseScale;
                float _NoiseStrength;
                float _PixelsPerUnit;
                float _EdgeBreakup;
                float _FarFogStart;
                float _FarFogEnd;
                float _FarFogDensity;
                float _VisionClearStrength;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 positionWS : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(world);
                output.positionWS = world.xy;
                return output;
            }

            float Hash(float2 p)
            {
                // Sine-free hash: sin() of large arguments loses GPU precision and bands the noise.
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), u.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                return (ValueNoise(p) * 0.5 + ValueNoise(p * 2.03 + float2(17.2, 9.1)) * 0.25
                    + ValueNoise(p * 4.11 + float2(31.7, 47.3)) * 0.125) / 0.875;
            }

            float SampleMask(float2 uv)
            {
                if (any(uv < 0.0) || any(uv > 1.0))
                    return 0.0;
                return SAMPLE_TEXTURE2D(_VisionMask, sampler_VisionMask, uv).r;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS;
                if (_PixelsPerUnit > 0.0)
                    p = (floor(p * _PixelsPerUnit) + 0.5) / _PixelsPerUnit;

                float2 noisePosition = p * _NoiseScale + _Time.y * _WindVelocity.xy * _NoiseScale;
                float noise1 = Fbm(noisePosition);
                float noise2 = Fbm(noisePosition + float2(83.4, 129.7));
                // Guard the initial no-viewer state, before a mask has been rendered.
                float2 rectSize = max(_MaskWorldRect.zw, float2(1e-4, 1e-4));
                float2 uv = (p - _MaskWorldRect.xy) / rectSize;
                float mask = SampleMask(uv);
                float threshold = 0.5 + noise2 * _EdgeBreakup;
                float clear = saturate((mask - threshold) / max(1.0 - threshold, 1e-4));
                clear *= (1.0 - _FarFogDensity * smoothstep(_FarFogStart,
                    max(_FarFogEnd, _FarFogStart + 1e-4), distance(p, _VisionOrigin.xy))) * _VisionClearStrength;
                float alpha = _FogColor.a * lerp(1.0 - _NoiseStrength, 1.0, noise1)
                    * lerp(1.0, _InsideDensity, clear);
                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
