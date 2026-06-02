Shader "AgainstTheMist/MistAtmosphericFog"
{
    Properties
    {
        [Header(Colors)]
        _FogColor ("Fog Color", Color) = (0.72, 0.82, 0.86, 1.0)
        _DeepColor ("Deep Fog Color", Color) = (0.25, 0.34, 0.40, 1.0)
        _GlowColor ("Atmosphere Glow", Color) = (0.86, 0.94, 0.92, 1.0)

        [Header(Visibility)]
        _Opacity ("Opacity", Range(0.0, 1.0)) = 0.68
        _Density ("Density", Range(0.0, 2.5)) = 1.20
        _Presence ("Minimum Presence", Range(0.0, 0.25)) = 0.03
        _EdgeFade ("Edge Fade", Range(0.0, 0.45)) = 0.09

        [Header(Layers)]
        _MainScale ("Main Layer Scale", Range(0.4, 7.0)) = 3.0
        _SecondScale ("Second Layer Scale", Range(1.0, 12.0)) = 7.6
        _SecondStrength ("Second Layer Strength", Range(0.0, 1.0)) = 0.56
        _WarpStrength ("Layer Warp", Range(0.0, 1.0)) = 0.28

        [Header(Motion)]
        _FlowDirection ("Flow Direction", Vector) = (0.72, 0.24, 0.0, 0.0)
        _FlowSpeed ("Flow Speed", Range(0.0, 1.0)) = 0.10
        _BreathAmount ("Breathing", Range(0.0, 0.35)) = 0.05
        _Atmosphere ("Atmosphere", Range(0.0, 1.0)) = 0.24

        [Header(Fog Mask)]
        _FogMask ("Fog Mask", 2D) = "black" {}
        _MaskSoftness ("Mask Softness", Range(0.0, 1.0)) = 0.25
        _MaskContrast ("Mask Contrast", Range(0.5, 3.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "AtmosphericFog2D"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColor;
                half4 _DeepColor;
                half4 _GlowColor;
                half _Opacity;
                half _Density;
                half _Presence;
                half _EdgeFade;
                half _MainScale;
                half _SecondScale;
                half _SecondStrength;
                half _WarpStrength;
                half4 _FlowDirection;
                half _FlowSpeed;
                half _BreathAmount;
                half _Atmosphere;
                half _MaskSoftness;
                half _MaskContrast;
            CBUFFER_END

            TEXTURE2D(_FogMask);
            SAMPLER(sampler_FogMask);
            float4 _FogMask_ST;

            float hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float mainLayerNoise(float2 p)
            {
                float n = valueNoise(p) * 0.56;
                n += valueNoise(p * 2.01 + 11.7) * 0.30;
                n += valueNoise(p * 4.09 + 3.3) * 0.14;
                return saturate(n);
            }

            float secondLayerNoise(float2 p)
            {
                float n = valueNoise(p) * 0.67;
                n += valueNoise(p * 2.27 + 7.9) * 0.33;
                n = 1.0 - abs(n * 2.0 - 1.0);
                return saturate(n);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positions.positionCS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 direction = normalize(_FlowDirection.xy + float2(0.0001, 0.0001));
                float2 sideDirection = float2(-direction.y, direction.x);
                float2 uv = IN.uv;
                float time = _Time.y;

                float2 mainUv = uv * _MainScale + direction * time * _FlowSpeed;
                float mainLayer = mainLayerNoise(mainUv);

                float2 warp = float2(
                    valueNoise(mainUv * 1.31 + 13.2),
                    valueNoise(mainUv * 1.47 + 4.8)
                );
                warp = (warp - 0.5) * (2.0 * _WarpStrength);

                float2 warpedUv = uv + warp * 0.18;
                float2 secondUv = float2(
                    dot(warpedUv, direction) * (_SecondScale * 0.52),
                    dot(warpedUv, sideDirection) * _SecondScale
                );
                secondUv += sideDirection * time * (_FlowSpeed * 1.65);
                float secondLayer = secondLayerNoise(secondUv);

                float breath = 1.0 + sin(time * 0.55) * _BreathAmount;
                float body = smoothstep(0.14, 0.90, mainLayer);
                body = saturate(pow(body, 1.2) * breath);
                float wisps = smoothstep(0.38, 0.97, secondLayer) * _SecondStrength;
                float mist = saturate(body * 0.78 + wisps * (0.32 + body * 0.68));

                float2 edgeUv = abs(IN.uv * 2.0 - 1.0);
                float edgeDistance = saturate(1.0 - max(edgeUv.x, edgeUv.y));
                float edgeFade = smoothstep(0.0, max(0.001, _EdgeFade), edgeDistance);

                float density = saturate(pow(mist, 1.05) * _Density);
                float alpha = saturate(_Presence + density * _Opacity) * edgeFade;

                float2 maskUv = IN.uv * _FogMask_ST.xy + _FogMask_ST.zw;
                float maskSample = SAMPLE_TEXTURE2D(_FogMask, sampler_FogMask, maskUv).r;
                float contrastedMask = pow(saturate(maskSample), max(0.001, _MaskContrast));
                float softenedMask = pow(contrastedMask, lerp(2.5, 0.75, saturate(_MaskSoftness)));
                float clearAmount = saturate(softenedMask);
                alpha *= (1.0 - clearAmount);

                float thinMist = saturate(1.0 - density);
                float glow = smoothstep(0.08, 0.95, thinMist) * _Atmosphere * (0.65 + wisps * 0.35);
                half3 color = lerp(_DeepColor.rgb, _FogColor.rgb, saturate(pow(mist, 0.86)));
                color = lerp(color, _GlowColor.rgb, saturate(glow));

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
