Shader "AgainstTheMist/AuthorFog"
{
    Properties
    {
        _FogColorA ("Fog Color A", Color) = (0.64, 0.72, 0.78, 1)
        _FogColorB ("Fog Color B", Color) = (0.40, 0.48, 0.56, 1)
        _Opacity ("Opacity", Range(0, 1)) = 0.65

        _NoiseScale ("Noise Scale", Range(0.2, 8.0)) = 2.2
        _NoiseStrength ("Noise Strength", Range(0, 1)) = 0.55
        _FlowSpeedX ("Flow Speed X", Range(-2, 2)) = 0.20
        _FlowSpeedY ("Flow Speed Y", Range(-2, 2)) = 0.07

        _HeightStart ("Height Start", Float) = -10.0
        _HeightEnd ("Height End", Float) = 30.0
        _EdgeSoftness ("Edge Softness", Range(0.1, 4.0)) = 1.4
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline"
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

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
                float3 positionWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _FogColorA;
                half4 _FogColorB;
                half _Opacity;
                half _NoiseScale;
                half _NoiseStrength;
                half _FlowSpeedX;
                half _FlowSpeedY;
                half _HeightStart;
                half _HeightEnd;
                half _EdgeSoftness;
                half _MinAlpha;
            CBUFFER_END

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));

                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                float freq = 1.0;

                [unroll(4)]
                for (int i = 0; i < 4; i++)
                {
                    v += valueNoise(p * freq) * amp;
                    freq *= 2.0;
                    amp *= 0.5;
                }

                return saturate(v);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positionInputs.positionCS;
                OUT.positionWS = positionInputs.positionWS;
                OUT.viewDirWS = GetCameraPositionWS() - positionInputs.positionWS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 flow = float2(_FlowSpeedX, _FlowSpeedY) * _Time.y;
                float2 uv = IN.uv * _NoiseScale + flow;

                float n1 = fbm(uv);
                float n2 = fbm(uv * 1.8 + float2(13.7, 4.9) - flow * 0.75);
                float noiseMix = saturate(lerp(n1, n2, 0.45));

                float heightT = saturate((IN.positionWS.y - _HeightStart) / max(0.001, _HeightEnd - _HeightStart));
                float heightFade = 1.0 - pow(heightT, 1.2);

                float3 viewDir = normalize(IN.viewDirWS);
                float facing = 1.0 - saturate(abs(viewDir.y));
                float edge = pow(facing, _EdgeSoftness);

                float fogShape = saturate(heightFade * (0.65 + edge * 0.35));
                float density = saturate(fogShape * lerp(1.0 - _NoiseStrength, 1.0, noiseMix));

                half3 color = lerp(_FogColorA.rgb, _FogColorB.rgb, noiseMix);
                half alpha = saturate(_MinAlpha + density * _Opacity);

                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
