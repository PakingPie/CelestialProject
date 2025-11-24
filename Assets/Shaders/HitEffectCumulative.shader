Shader "Custom/HitEffectCumulative"
{
    Properties
    {
        _MainTex ("Current Texture", 2D) = "black" {}
        _HitTex ("Hit Texture", 2D) = "black" {}
        _Decay ("Decay", Range(0,1)) = 0.5
        _MinThreshold ("Minimum Threshold", Range(0,1)) = 0.02
        // _PrevTex ("Previous Texture", 2D) = "black" {}
        // _HitTexScale ("Scale", Float) = 0.5
        // _HitUV ("Center", Vector) = (0,0,0,0)
        // _AlphaControl ("Alpha Control", Range(0,1)) = 1.0
        // _Size ("Size", Float) = 0.5s
        // _EdgeMin ("Edge Min", Float) = 0.0
        // _EdgeMax ("Edge Max", Float) = 0.15
        // _Thickness ("Thickness", Float) = 0.01
        // _Fade ("Fade", Float) = 1.0

    }
    SubShader
    {
        Tags {"RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent"}

        Pass
        {
            Cull Off
            ZWrite Off        
            ZTest Always       
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            sampler2D _MainTex;
            sampler2D _HitTex;
            float _Decay;
            float _MinThreshold;

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f 
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v) 
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target 
            {
                float4 oldCumulative = tex2D(_MainTex, i.uv);
                
                // 衰减
                float4 decayed = oldCumulative * _Decay;
                
                // 新击中
                float4 newHit = tex2D(_HitTex, i.uv);
                
                // Max混合
                float4 result = max(decayed, newHit);
                
                // 阈值裁剪 - 这是关键！
                float maxChannel = max(max(result.r, result.g), result.b);
                if (maxChannel < _MinThreshold)
                {
                    result = float4(0, 0, 0, 0);
                }
                
                return result;
            }
            ENDHLSL
        }
        // Pass
        // {
            //     Cull Off
            //     ZWrite On
            //     ZTest LEqual
            //     Blend SrcAlpha OneMinusSrcAlpha
            //     HLSLPROGRAM

            //     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //     #pragma vertex vert
            //     #pragma fragment frag
            //     #pragma target 4.5

            //     sampler2D _MainTex;
            //     sampler2D _PrevTex;
            //     sampler2D _HitTex;

            //     float4 _HitUV;
            //     float _HitTexScale;
            //     float _AlphaControl;

            //     float4 _MainTex_ST;

            //     float _Size;
            //     float _EdgeMin;
            //     float _EdgeMax;
            //     float _Thickness;
            //     float _Fade;

            //     struct appdata
            //     {
                //         float4 vertex : POSITION;
                //         float2 uv : TEXCOORD0;
            //     };

            //     struct v2f
            //     {
                //         float4 PosHCS : SV_POSITION;
                //         float2 UV : TEXCOORD0;
            //     };

            //     float inverseLerp(float a, float b, float value)
            //     {
                //         return saturate((value - a) / (b - a));
            //     }

            //     float Stroke(float2 uv, float size, float edgeMin, float edgeMax, float strokeThickness, bool strokeRelative)
            //     {
                //         uv = uv * 2.0 - 1;
                //         float sdf = distance(uv, float2(0.0, 0.0));
                //         strokeThickness = strokeRelative ? size * strokeThickness : strokeThickness;
                //         float size1 = size - strokeThickness;
                //         float size2 = size + strokeThickness;
                //         float edgeThickness = lerp(size1, size2, 0.5);

                //         float sdfFill = sdf - edgeThickness;
                //         float fill = saturate(1 - inverseLerp(edgeMin, edgeMax, sdfFill));

                //         float fillAbs = abs(sdfFill);
                //         float sdfStroke = fillAbs - strokeThickness;
                //         return saturate(1 - inverseLerp(edgeMin, edgeMax, sdfStroke));
            //     }

            //     float2 Rotate(float2 p, float deg)
            //     {
                //         float rad = radians(deg);
                //         float x = p.x * cos(rad) - p.y * sin(rad);
                //         float y = p.x * sin(rad) + p.y * cos(rad);
                //         return float2(x, y);
            //     }

            //     bool ExistPointInTriangle(float3 p, float3 t1, float3 t2, float3 t3)
            //     {
                //         const float TOLERANCE = 1 - 0.1;

                //         float3 a = normalize(cross(t1 - t3, p - t1));
                //         float3 b = normalize(cross(t2 - t1, p - t2));
                //         float3 c = normalize(cross(t3 - t2, p - t3));

                //         float d_ab = dot(a, b);
                //         float d_bc = dot(b, c);

                //         return (d_ab > TOLERANCE) && (d_bc > TOLERANCE);
            //     }

            //     bool IsInRange(float2 uv, float2 hitUV, float scale, float deg)
            //     {
                //         float3 p = float3(uv, 0);
                //         float3 v1 = float3(Rotate(float2(-scale, scale), deg) + hitUV, 0);
                //         float3 v2 = float3(Rotate(float2(-scale, -scale), deg) + hitUV, 0);
                //         float3 v3 = float3(Rotate(float2(scale, -scale), deg) + hitUV, 0);
                //         float3 v4 = float3(Rotate(float2(scale, scale), deg) + hitUV, 0);
                //         return ExistPointInTriangle(p, v1, v2, v3) || ExistPointInTriangle(p, v1, v3, v4);
            //     }

            //     float2 CalcHitUV(float2 uv, float2 hitUV, float scale, float deg)
            //     {
                //         #if UNITY_UV_STARTS_AT_TOP
                //             return Rotate((uv - hitUV) / scale, -deg) * 0.5 + 0.5;
                //         #else
                //             return Rotate((uv - hitUV) / scale, deg) * 0.5 + 0.5;
                //         #endif
            //     }


            //     v2f vert (appdata v)
            //     {
                //         v2f o = (v2f)0;
                //         o.PosHCS = TransformObjectToHClip(v.vertex);
                //         o.UV = v.uv;
                //         return o;
            //     }

            //     // half4 frag (v2f i) : SV_Target
            //     // {
                //         //     float4 baseColor = tex2Dlod(_MainTex, float4(i.UV, 0, 0));
                //         //     float4 lastFrameColor = tex2Dlod(_PrevTex, float4(i.UV, 0, 0));
                //         //     float4 hitColor = 0;
                //         //     if (IsInRange(i.UV, _HitUV.xy, _HitTexScale, 0))
                //         //     {
                    //             //         float2 hitUV = CalcHitUV(i.UV, _HitUV.xy, _HitTexScale, 0);
                    //             //         hitColor = tex2Dlod(_HitTex, float4(hitUV, 0, 0));
                    //             //         return lerp(baseColor, hitColor, hitColor.a);
                //         //     }
                //         //     return baseColor;
            //     // }
            //     float4 frag(v2f i) : SV_Target
            //     {
                //         float baseColor = tex2D(_MainTex, float4(i.UV, 0, 0)).r * 0.5;
                //         float2 hitUV = CalcHitUV(i.UV, _HitUV.xy, _HitTexScale, 0);
                //         float hitColor = tex2D(_HitTex, float4(hitUV, 0, 0)).r;
                //         return lerp(baseColor, hitColor, hitColor);
            //     }
            //     ENDHLSL
        // }

        
        // Pass 
        // {
            //     Name "DepthOnly"
            //     Tags { "LightMode" = "DepthOnly" }

            //     ZWrite On
            //     ColorMask 0
            //     Cull Off

            //     HLSLPROGRAM
            //     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            //     #pragma vertex vert
            //     #pragma fragment frag

            //     struct appdata
            //     {
                //         float4 vertex : POSITION;
                //         float3 normal : NORMAL;
                //         float2 uv : TEXCOORD0;
                //         UNITY_VERTEX_INPUT_INSTANCE_ID
            //     };

            //     struct v2f
            //     {
                //         float4 PosHCS : SV_POSITION;
                //         float2 UV : TEXCOORD0;
                //         float3 NormalWS : TEXCOORD1;
                //         UNITY_VERTEX_OUTPUT_STEREO
            //     };

            //     v2f vert (appdata v)
            //     {
                //        return v;
            //     }

            //     float frag (v2f i) : SV_DEPTH
            //     {
                //         return i.PosHCS.z / i.PosHCS.w;
            //     }
            //     ENDHLSL
        // }

        // Pass 
        // {
            //     Name "DepthNormals"
            //     Tags { "LightMode" = "DepthNormals" }
            //     ZWrite On
            //     HLSLPROGRAM
            //     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            //     #pragma vertex vert
            //     #pragma fragment frag

            //     struct appdata
            //     {
                //         float4 vertex : POSITION;
                //         float3 normal : NORMAL;
                //         UNITY_VERTEX_INPUT_INSTANCE_ID
            //     };

            //     struct v2f
            //     {
                //         float4 PosHCS : SV_POSITION;
                //         float3 NormalWS : TEXCOORD0;
                //         UNITY_VERTEX_OUTPUT_STEREO
            //     };

            //     v2f vert (appdata v)
            //     {
                //         v2f o = (v2f)0;
                //         o.PosHCS = TransformObjectToHClip(v.vertex);
                //         o.NormalWS = TransformObjectToWorldNormal(v.normal);
                //         return o;
            //     }

            //     float4 frag (v2f i) : SV_Target
            //     {
                //         return float4(NormalizeNormalPerPixel(i.NormalWS), 0.0);
            //     }
            //     ENDHLSL
        // }
    }
}
