Shader "Custom/Procedural/UI_Rectangle"
{
    Properties
    {
        [KeywordEnum(FILL, STROKE, FILL_SDF, STROKE_SDF)] _RENDER_MODE ("Render Mode", Float) = 0
        _MainTex ("Texture", 2D) = "white" {}
        _CornerRadius ("Corner Radius", Vector) = (0, 0, 0, 0)
        _StrokeThickness ("Stroke Thickness", Float) = 0.1
        _WidthHeight ("Width Height", Vector) = (1, 1, 0, 0)

        _Size ("Size", Vector) = (1, 1, 0, 0)
        _EdgeThickness ("Edge Thickness", Vector) = (0.0, 0.0, 0.0, 0.0)
        [Toggle] _StrokeThicknessRelative ("Stroke Thickness Relative", Float) = 0
        [KeywordEnum(WIDTH, HEIGHT)] _STROKE_DIMESION ("Stroke Dimension", Float) = 0
        [Toggle] _KeepAspectRatio ("Keep Aspect Ratio", Float) = 0
        _CanvasSize ("Canvas Size", Vector) = (512, 512, 0, 0)
    }
    SubShader
    {
        // Pass
        // {
        //     Name "Fixed Size"
        //     Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        //     Blend SrcAlpha OneMinusSrcAlpha
        //     ZWrite Off

        //     HLSLPROGRAM
        //     #pragma vertex vert
        //     #pragma fragment frag

        //     #pragma multi_compile _RENDER_MODE_FILL _RENDER_MODE_STROKE _RENDER_MODE_FILL_SDF _RENDER_MODE_STROKE_SDF
        //     #pragma multi_compile _ _STROKE_DIMESION_WIDTH _STROKE_DIMESION_HEIGHT

        //     #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
        //     sampler2D _MainTex;
        //     float4 _Size;
        //     float4 _CornerRadius;
        //     float4 _EdgeThickness;
        //     float _StrokeThickness;
        //     float _StrokeThicknessRelative;
        //     float4 _WidthHeight;
        //     float _KeepAspectRatio;
        //     float4 _CanvasSize;

        //     struct Attributes
        //     {
        //         float4 positionOS : POSITION;
        //         float2 uv : TEXCOORD0;
        //     };

        //     struct Varyings
        //     {
        //         float4 positionHCS : SV_POSITION;
        //         float2 uv : TEXCOORD0;
        //     };



        //     Varyings vert(Attributes IN)
        //     {
        //         Varyings OUT;
        //         OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
        //         OUT.uv = IN.uv;
        //         return OUT;
        //     }

        //     float RounderCorners(float2 uv, float4 cornerRadius)
        //     {
        //         float2 localUV = saturate(ceil(uv * 2.0 - 1.0));
        //         float2 cornerOffset = lerp(float2(cornerRadius.x, cornerRadius.y), float2(cornerRadius.z, cornerRadius.w), localUV.x);
        //         return lerp(cornerOffset.x, cornerOffset.y, localUV.y);
        //     }

        //     void KeepAspectRatio(float2 widthHeight, out float2 aspectRatio, out float2 horizontal, out float2 vertical)
        //     {
        //         horizontal = float2(widthHeight.x / widthHeight.y, 1.0);
        //         vertical = float2(1.0, widthHeight.y / widthHeight.x);
        //         aspectRatio = max(horizontal, vertical);
        //     }

        //     float invLerp(float from, float to, float value)
        //     {
        //         return (value - from) / (to - from);
        //     }
            
        //     float4 frag(Varyings IN) : SV_Target
        //     {
        //         float2 centralizedUV = abs(IN.uv * 2.0 - 1.0); // Transform UVs from [0,1] to [-1,1]

        //         float p = centralizedUV * _CanvasSize.xy;
        //         #if _STROKE_DIMESION_WIDTH
        //             float strokeSize = _Size.y;
        //         #else // _STROKE_DIMESION_HEIGHT
        //             float strokeSize = _Size.x;
        //         #endif
                
        //         if(_StrokeThicknessRelative)
        //         {
        //             strokeSize *= _StrokeThickness;
        //         }
        //         else
        //         {
        //             strokeSize = _StrokeThickness;
        //         }

        //         float2 strokeSizeVec = lerp(_Size.xy - float2(strokeSize, strokeSize), _Size.xy + float2(strokeSize, strokeSize), float2(0.5, 0.5));

        //         float2 strokeUV = centralizedUV - strokeSizeVec;

        //         if(_KeepAspectRatio > 0)
        //         {
        //             float2 aspectRatio;
        //             float2 horizontal;
        //             float2 vertical;
        //             KeepAspectRatio(_WidthHeight, aspectRatio, horizontal, vertical);
        //             strokeUV *= aspectRatio;
        //         }

        //         float cornerRadius = RounderCorners(IN.uv, _CornerRadius);

        //         strokeUV += cornerRadius;

        //         float sdf = length(max(strokeUV, 0.0)) + min(max(strokeUV.x, strokeUV.y), 0.0) - cornerRadius;

        //         float sdfStroke = abs(sdf) - strokeSize;

        //         float fill = saturate(1 - invLerp(_EdgeThickness.x, _EdgeThickness.y, sdf));

        //         float stroke = saturate(1 - invLerp(_EdgeThickness.z, _EdgeThickness.w, sdfStroke));

        //         #if defined(_RENDER_MODE_FILL)
        //             return float4(fill, fill, fill, 1);
        //         #elif defined(_RENDER_MODE_FILL_SDF)
        //             return float4(sdf, sdf, sdf, 1);
        //         #elif defined(_RENDER_MODE_STROKE_SDF)
        //             return float4(sdfStroke, sdfStroke, sdfStroke, 1);
        //         #elif defined(_RENDER_MODE_STROKE)
        //             return float4(stroke, stroke, stroke, 1);
        //         #else
        //             return float4(stroke, stroke, stroke, 1);
        //         #endif
        //     }
        //     ENDHLSL
        // }

        Pass
        {
            Name "Scale With Object"
            // Tags { "RenderType"="Transparent" "Queue"="Transparent" }
            // Blend SrcAlpha OneMinusSrcAlpha
            // ZWrite Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _RENDER_MODE_FILL _RENDER_MODE_STROKE _RENDER_MODE_FILL_SDF _RENDER_MODE_STROKE_SDF

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float4 _CornerRadius;
            float _StrokeThickness;
            float4 _WidthHeight;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            // Standard SDF for a rounded box
            float sdRoundedBox(float2 p, float2 b, float4 r)
            {
                // Select corner radius based on quadrant
                // r.x = TL, r.y = TR, r.z = BR, r.w = BL (Adjust order to preference)
                float radius = (p.x > 0.0) ? 
                ((p.y > 0.0) ? r.y : r.z) : 
                ((p.y > 0.0) ? r.x : r.w);
                
                // Clamp radius to ensure it doesn't exceed half the size
                radius = min(radius, min(b.x, b.y));
                
                float2 q = abs(p) - b + radius;
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // 1. SETUP DIMENSIONS
                // We assume _WidthHeight contains the actual pixel size of the rect (e.g., 200, 50)
                float2 size = _WidthHeight.xy;
                float2 halfSize = size * 0.5;

                // 2. COORDINATE SPACE CONVERSION
                // Convert UV (0..1) to Pixel Coordinates centered at (0,0)
                // Range becomes [-Width/2, Width/2] and [-Height/2, Height/2]
                float2 p = (IN.uv - 0.5) * size;

                // 3. CALCULATE SDF (Signed Distance Field)
                // Distance is now in PIXELS, not UV units.
                // Negative = inside, Positive = outside
                float dist = sdRoundedBox(p, halfSize, _CornerRadius);

                // 4. CALCULATE STROKE
                // Since dist is in pixels, we can subtract thickness directly
                float strokeDist = abs(dist) - (_StrokeThickness * 0.5);

                // 5. ANTI-ALIASING
                // fwidth() gives us the change in value over one screen pixel. 
                // This creates a perfectly sharp edge regardless of resolution.
                float aaWidth = fwidth(dist);
                float alphaFill = 1.0 - smoothstep(-aaWidth, aaWidth, dist);
                float alphaStroke = 1.0 - smoothstep(-aaWidth, aaWidth, strokeDist);

                // 6. OUTPUT
                #if defined(_RENDER_MODE_FILL)
                    return float4(1, 1, 1, alphaFill);
                #elif defined(_RENDER_MODE_FILL_SDF)
                    return float4(dist, dist, dist, 1);
                #elif defined(_RENDER_MODE_STROKE_SDF)
                    return float4(strokeDist, strokeDist, strokeDist, 1);
                #elif defined(_RENDER_MODE_STROKE)
                    return float4(1, 1, 1, alphaStroke);
                #else
                    return float4(1, 1, 1, alphaStroke);
                #endif
            }
            ENDHLSL
        }
    }
}