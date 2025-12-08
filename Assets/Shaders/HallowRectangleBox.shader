Shader "UI/Custom/RectangleBox"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR]_BaseColor ("Tint", Color) = (1,1,1,1)
        _StrokeThickness ("Stroke Thickness", float) = 0.2
        _StrokeAlpha ("Stroke Alpha", Range(0, 1)) = 0.9
        _ContenteAlpha ("Content Alpha", Range(0, 0.5)) = 0.5
        _CanvasSize ("Canvas Size", Vector) = (1, 1, 0, 0)
        _CornerRadius ("Corner Radius", Vector) = (0, 0, 0, 0)
        _EdgeMinMax ("Edge Min Max", Vector) = (0, 4, 0, 0)

        // --- REQUIRED FOR UI MASKING ---
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        // Standard UI Blending
        Blend SrcAlpha OneMinusSrcAlpha
        
        // UI usually turns off ZWrite and Culling
        Cull Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        ColorMask [_ColorMask]

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR; // Receive Vertex Color from Canvas
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _BaseColor;
            float _StrokeThickness;
            float _StrokeAlpha;
            float _ContenteAlpha;
            float2 _CanvasSize;
            float4 _CornerRadius;
            float2 _EdgeMinMax;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color; 
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float SDFRoundBox(in float2 p, in float2 b, in float4 r)
            {
                float radius = (p.x > 0.0) ? ((p.y > 0.0) ? r.y : r.z) : ((p.y > 0.0) ? r.x : r.w);

                radius = min(radius, min(b.x, b.y));

                float2 q = abs(p) - b + radius;

                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            float inverseLerp(float a, float b, float value)
            {
                return (value - a) / (b - a);
            }


            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv - float2(0.5, 0.5);
                uv *= _CanvasSize;

                float dist = SDFRoundBox(uv, _CanvasSize * 0.5, _CornerRadius);

                // fwidth creates a smooth edge based on screen resolution (Anti-aliasing)
                float delta = fwidth(dist); 
                
                // Calculate Alpha for the main shape
                float fillSDF = abs(dist) - _StrokeThickness * 0.5;

                float strokeSDF = step(0.8, 1 - inverseLerp(_EdgeMinMax.x, _EdgeMinMax.y, abs(fillSDF)));

                float alpha = smoothstep(-delta, delta, fillSDF) * _ContenteAlpha + strokeSDF * _StrokeAlpha;

                float3 color = tex2D(_MainTex, IN.uv) * strokeSDF + strokeSDF * _BaseColor;

                return float4(color, alpha) * IN.color;
            }


            ENDHLSL
        }
    }
}