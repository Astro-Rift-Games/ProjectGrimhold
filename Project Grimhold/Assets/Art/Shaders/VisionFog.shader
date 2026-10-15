// Local presentation only: occlusion-aware clear mask rendered by VisionFogMask.
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
        _EdgeSoftness ("Edge Softness", Float) = 0.5
        _EdgeNoiseAmplitude ("Edge Noise Amplitude", Float) = 0.6
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
                float _EdgeSoftness;
                float _EdgeNoiseAmplitude;
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
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
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
                float noise3 = Fbm(noisePosition + float2(219.8, 37.6));
                float2 jitter = (float2(noise2, noise3) - 0.5) * 2.0 * _EdgeNoiseAmplitude;
                // Guard the initial no-viewer state, before a mask has been rendered.
                float2 rectSize = max(_MaskWorldRect.zw, float2(1e-4, 1e-4));
                float2 uv = (p + jitter - _MaskWorldRect.xy) / rectSize;
                float2 radius = _EdgeSoftness / rectSize;
                float mask = SampleMask(uv) * 0.2;
                [unroll]
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * (6.28318530718 / 8.0);
                    mask += SampleMask(uv + float2(cos(angle), sin(angle)) * radius) * 0.1;
                }
                float clear = mask * (1.0 - _FarFogDensity * smoothstep(_FarFogStart,
                    max(_FarFogEnd, _FarFogStart + 1e-4), distance(p, _VisionOrigin.xy))) * _VisionClearStrength;
                float alpha = _FogColor.a * lerp(1.0 - _NoiseStrength, 1.0, noise1)
                    * lerp(1.0, _InsideDensity, clear);
                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
