// Local presentation only: geometric mask (no occlusion), mirroring VisionConeRules shape.
Shader "Grimhold/Visibility/VisionFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.05, 0.06, 0.08, 0.85)
        _InsideDensity ("Inside Density", Range(0, 1)) = 0.06
        _NoiseScale ("Noise Scale", Float) = 0.35
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.45
        _WindVelocity ("Wind Velocity", Vector) = (0.15, 0.05, 0, 0)
        _PixelsPerUnit ("Pixels Per Unit", Float) = 16
        _RadialSoftness ("Radial Softness", Float) = 1.5
        _AngularSoftness ("Angular Softness (Radians)", Float) = 0.20943951
        _EdgeNoiseAmplitude ("Edge Noise Amplitude", Float) = 0.6
        _FarFogStart ("Far Fog Start", Float) = 8
        _FarFogEnd ("Far Fog End", Float) = 16
        _FarFogDensity ("Far Fog Density", Range(0, 1)) = 0.35
        _VisionOrigin ("Vision Origin", Vector) = (0, 0, 0, 0)
        _VisionFacing ("Vision Facing", Vector) = (0, 1, 0, 0)
        _VisionNearRadius ("Vision Near Radius", Float) = 0
        _VisionHalfAngle ("Vision Half Angle (Radians)", Float) = 0
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

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float4 _WindVelocity;
                float4 _VisionOrigin;
                float4 _VisionFacing;
                float _InsideDensity;
                float _NoiseScale;
                float _NoiseStrength;
                float _PixelsPerUnit;
                float _RadialSoftness;
                float _AngularSoftness;
                float _EdgeNoiseAmplitude;
                float _FarFogStart;
                float _FarFogEnd;
                float _FarFogDensity;
                float _VisionNearRadius;
                float _VisionHalfAngle;
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

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS;
                if (_PixelsPerUnit > 0.0)
                    p = (floor(p * _PixelsPerUnit) + 0.5) / _PixelsPerUnit;

                float2 noisePosition = p * _NoiseScale + _Time.y * _WindVelocity.xy * _NoiseScale;
                float noise1 = Fbm(noisePosition);
                float noise2 = Fbm(noisePosition + float2(83.4, 129.7));
                float2 d = p - _VisionOrigin.xy;
                float dist = length(d);
                float edge = (noise2 - 0.5) * 2.0 * _EdgeNoiseAmplitude;
                float nearClear = 1.0 - smoothstep(_VisionNearRadius,
                    _VisionNearRadius + max(_RadialSoftness, 1e-4), dist + edge);
                float a = acos(clamp(dot(d / max(dist, 1e-4), _VisionFacing.xy), -1.0, 1.0));
                float jitter = edge / max(dist, 0.5);
                float coneClear = 1.0 - smoothstep(_VisionHalfAngle,
                    _VisionHalfAngle + max(_AngularSoftness, 1e-4), a + jitter);
                coneClear *= 1.0 - _FarFogDensity * smoothstep(_FarFogStart,
                    max(_FarFogEnd, _FarFogStart + 1e-4), dist);
                float clear = (dist < 1e-4 ? 1.0 : max(nearClear, coneClear)) * _VisionClearStrength;
                float alpha = _FogColor.a * lerp(1.0 - _NoiseStrength, 1.0, noise1)
                    * lerp(1.0, _InsideDensity, clear);
                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
