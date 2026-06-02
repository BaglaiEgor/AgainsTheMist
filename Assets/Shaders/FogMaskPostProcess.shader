Shader "Hidden/FogMaskPostProcess"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Sharpness ("Sharpness", Range(0, 1)) = 0.5
        _Contrast ("Contrast", Range(0, 3)) = 1.5
        _AntiAliasing ("AntiAliasing", Range(0, 5)) = 2
    }
    
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Sharpness;
            float _Contrast;
            float _AntiAliasing;
            
            float4 SampleAA(float2 uv, float offset)
            {
                float4 color = tex2D(_MainTex, uv);
                
                // Мультисэмплинг для антиалиасинга
                if (_AntiAliasing > 0)
                {
                    float2 offsets[4] = {
                        float2(-0.25, -0.25),
                        float2(0.25, -0.25),
                        float2(-0.25, 0.25),
                        float2(0.25, 0.25)
                    };
                    
                    float4 sum = color;
                    for(int i = 0; i < 4; i++)
                    {
                        sum += tex2D(_MainTex, uv + offsets[i] * _MainTex_TexelSize.xy * _AntiAliasing);
                    }
                    color = sum / 5.0;
                }
                
                return color;
            }
            
            float4 ApplyContrast(float4 color, float contrast)
            {
                // Формула контраста
                return pow(color, contrast);
            }
            
            float4 ApplySharpness(float4 color, float sharpness)
            {
                // Усиление краёв
                float edge = 1.0 - sharpness * 0.5;
                color.r = smoothstep(edge - 0.1, edge + 0.1, color.r);
                color.g = smoothstep(edge - 0.1, edge + 0.1, color.g);
                color.b = smoothstep(edge - 0.1, edge + 0.1, color.b);
                
                return color;
            }
            
            float4 frag (v2f i) : SV_Target
            {
                // Берём исходный цвет
                float4 color = SampleAA(i.uv, _AntiAliasing);
                
                // Применяем контраст
                color = ApplyContrast(color, _Contrast);
                
                // Применяем резкость
                color = ApplySharpness(color, _Sharpness);
                
                // Гарантируем что значения в диапазоне 0-1
                color = saturate(color);
                
                return color;
            }
            ENDCG
        }
    }
    
    FallBack Off
}