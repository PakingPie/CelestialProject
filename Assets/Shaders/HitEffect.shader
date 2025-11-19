Shader "Custom/HitEffect"
{
    Properties
    {
        [KeywordEnum(FILL, SDF, STROKE, STROKE_SDF)] _CIRCLE ("Mode", Float) = 0
        _Size ("Size", Float) = 0.5
        _EdgeMin ("Edge Min", Float) = 0.0
        _EdgeMax ("Edge Max", Float) = 0.15
        _Thickness ("Thickness", Float) = 0.01
        _Fade ("Fade", Float) = 1.0

        _HitUV ("Hit UV", Vector) = (0.5, 0.5, 0, 0)
    }
    SubShader
    {
        Tags {"RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent"}
        Pass
        {
            Cull Off
            ZWrite Off          // No need to write to depth
            ZTest Always        // Ensure it always renders
            Blend SrcAlpha One // Use additive blending
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma shader_feature _CIRCLE_FILL _CIRCLE_SDF _CIRCLE_STROKE _CIRCLE_STROKE_SDF

            float _Size;
            float _EdgeMin;
            float _EdgeMax;
            float _Thickness;
            float _Fade;
            float4 _HitUV;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 PosHCS : SV_POSITION;
                float2 UV : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                o.PosHCS = TransformObjectToHClip(v.vertex);
                o.UV = v.uv;
                return o;
            }

            float inverseLerp(float a, float b, float value)
            {
                return saturate((value - a) / (b - a));
            }

            void Circle(float2 uv, float2 centerUV, float size, float edgeMin, float edgeMax, float strokeThickness, bool strokeRelative,
            out float fill, out float sdfFill, out float stroke, out float sdfStroke)
            {
                float dist = distance(uv, centerUV);
                float sdf = dist * 2.0;

                strokeThickness = strokeRelative ? size * strokeThickness : strokeThickness;

                float size1 = size - strokeThickness;
                float size2 = size + strokeThickness;
                float edgeThickness = lerp(size1, size2, 0.5);

                sdfFill = sdf - edgeThickness;
                fill = saturate(1 - inverseLerp(edgeMin, edgeMax, sdfFill));

                float fillAbs = abs(sdfFill);
                sdfStroke = fillAbs - strokeThickness;
                stroke = saturate(1 - inverseLerp(edgeMin, edgeMax, sdfStroke));
            }

            float4 frag (v2f i) : SV_Target
            {
                float fill = 0, sdfFill = 0, stroke = 0, sdfStroke = 0;
                Circle(i.UV, _HitUV.xy, _Size, _EdgeMin, _EdgeMax, _Thickness, false, fill, sdfFill, stroke, sdfStroke);
                
                #ifdef _CIRCLE_FILL
                    return fill * _Fade;
                #elif defined(_CIRCLE_SDF)
                    return sdfFill * _Fade;
                #elif defined(_CIRCLE_STROKE)
                    return stroke * _Fade;
                #elif defined(_CIRCLE_STROKE_SDF)
                    return sdfStroke * _Fade;
                #else
                    return 0;
                #endif
            }
            ENDHLSL
        }

        // Pass
        // {
            //     Cull Off
            //     ZWrite On
            //     ZTest LEqual
            //     HLSLPROGRAM

            //     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //     #pragma vertex vert
            //     #pragma fragment frag
            //     #pragma target 4.5

            //     float4 _Center;
            //     float _Radius;
            //     float _Hardness;

            //     struct appdata
            //     {
                //         float4 vertex : POSITION;
                //         float2 uv : TEXCOORD0;
            //     };

            //     struct v2f
            //     {
                //         float4 PosHCS : SV_POSITION;
                //         float2 UV : TEXCOORD0;
                //         float3 PosWS : TEXCOORD1;
            //     };


            //     v2f vert (appdata v)
            //     {
                //         v2f o = (v2f)0;
                //         o.PosHCS = TransformObjectToHClip(v.vertex);
                //         o.UV = v.uv;
                //         o.PosWS = mul(unity_ObjectToWorld, v.vertex).xyz;
                //         return o;
            //     }

            //     float SphereMask(float3 Coords, float3 Center, float Radius, float Hardness)
            //     {
                //         return 1 - saturate((distance(Coords, Center) - Radius) / (1 - Hardness));
            //     }

            //     half4 frag (v2f i) : SV_Target
            //     {
                //         float mask1 = SphereMask(i.PosWS, _Center.xyz, _Radius, _Hardness);
                //         float mask2 = SphereMask(i.PosWS, _Center.xyz, _Radius * 0.5, _Hardness);
                //         return mask1 - mask2;
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
