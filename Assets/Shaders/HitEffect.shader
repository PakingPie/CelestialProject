// Shader "Custom/HitEffect"
// {
    //     Properties
    //     {
        //         [KeywordEnum(Fill, SDF, Stroke, StrokeSDF)] Circle ("Mode", Float) = 0
        //         _Size ("Size", Float) = 0.5
        //         _EdgeMin ("Edge Min", Float) = 0.0
        //         _EdgeMax ("Edge Max", Float) = 0.15
        //         _Thickness ("Thickness", Float) = 0.01
        //         _Fade ("Fade", Float) = 1.0
    //     }
    //     SubShader
    //     {
        //         Tags {"RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent"}
        //         Pass
        //         {
            //             Cull Off
            //             ZWrite On
            //             ZTest LEqual
            //             Blend SrcAlpha OneMinusSrcAlpha
            //             HLSLPROGRAM

            //             #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            //             #pragma multi_compile Circle_Fill Circle_SDF Circle_Stroke Circle_StrokeSDF

            //             #pragma vertex vert
            //             #pragma fragment frag
            //             #pragma target 4.5

            //             float _Size;
            //             float _EdgeMin;
            //             float _EdgeMax;
            //             float _Thickness;
            //             float _Fade;

            //             struct appdata
            //             {
                //                 float4 vertex : POSITION;
                //                 float2 uv : TEXCOORD0;
            //             };

            //             struct v2f
            //             {
                //                 float4 PosHCS : SV_POSITION;
                //                 float2 UV : TEXCOORD0;
            //             };

            //             v2f vert (appdata v)
            //             {
                //                 v2f o = (v2f)0;
                //                 o.PosHCS = TransformObjectToHClip(v.vertex);
                //                 o.UV = v.uv;
                //                 return o;
            //             }

            //             float inverseLerp(float a, float b, float value)
            //             {
                //                 return saturate((value - a) / (b - a));
            //             }

            //             void Circle(float2 uv, float size, float edgeMin, float edgeMax, float strokeThickness, bool strokeRelative,
            //             out float fill, out float sdfFill, out float stroke, out float sdfStroke)
            //             {
                //                 uv = uv * 2.0 - 1;
                //                 float sdf = distance(uv, float2(0.0, 0.0));
                //                 strokeThickness = strokeRelative ? size * strokeThickness : strokeThickness;
                //                 float size1 = size - strokeThickness;
                //                 float size2 = size + strokeThickness;
                //                 float edgeThickness = lerp(size1, size2, 0.5);

                //                 sdfFill = sdf - edgeThickness;
                //                 fill = saturate(1 - inverseLerp(edgeMin, edgeMax, sdfFill));

                //                 float fillAbs = abs(sdfFill);
                //                 sdfStroke = fillAbs - strokeThickness;
                //                 stroke = saturate(1 - inverseLerp(edgeMin, edgeMax, sdfStroke));
            //             }

            //             float4 frag (v2f i) : SV_Target
            //             {
                //                 float fill = 0, sdfFill = 0, stroke = 0, sdfStroke = 0;
                //                 Circle(i.UV, _Size, _EdgeMin, _EdgeMax, _Thickness, false, fill, sdfFill, stroke, sdfStroke);
                
                //                 return stroke * _Fade;
                
            //             }
            //             ENDHLSL
        //         }
    //     }
// }

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
            ZWrite Off        
            ZTest Always       
            Blend Off
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile _CIRCLE_FILL _CIRCLE_SDF _CIRCLE_STROKE _CIRCLE_STROKE_SDF

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
                
                float intensity = 0;
                #ifdef _CIRCLE_FILL
                    intensity = fill * _Fade;
                #elif defined(_CIRCLE_SDF)
                    intensity = sdfFill * _Fade;
                #elif defined(_CIRCLE_STROKE)
                    intensity = stroke * _Fade;
                #elif defined(_CIRCLE_STROKE_SDF)
                    intensity = sdfStroke * _Fade;
                #endif
                
                return float4(intensity, intensity, intensity, intensity);
            }
            ENDHLSL
        }
    }
}
