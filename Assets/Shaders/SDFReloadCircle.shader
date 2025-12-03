Shader "UI/SDFReloadCircle"
{
    Properties
    {
        _Color ("Color", Color) = (0, 1, 0, 1)
        _BackgroundColor ("Background Color", Color) = (0.2, 0.2, 0.2, 0.8)
        _FillAmount ("Fill Amount", Range(0, 1)) = 1
        _Radius ("Radius", Range(0, 0.5)) = 0.4
        _Thickness ("Thickness", Range(0, 0.5)) = 0.1
        _Smoothness ("Edge Smoothness", Range(0, 0.1)) = 0.01
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent" 
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }
        
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            float4 _Color;
            float4 _BackgroundColor;
            float _FillAmount;
            float _Radius;
            float _Thickness;
            float _Smoothness;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            
            float sdCircle(float2 p, float r)
            {
                return length(p) - r;
            }
            
            float sdRing(float2 p, float radius, float thickness)
            {
                return abs(sdCircle(p, radius)) - thickness * 0.5;
            }
            
            float4 frag (v2f i) : SV_Target
            {
                // Center UV
                float2 uv = i.uv - 0.5;
                
                // Calculate angle (0 to 1, starting from top, clockwise)
                float angle = atan2(uv.x, uv.y);
                float normalizedAngle = (angle / 3.14159265) * 0.5 + 0.5;
                
                // Ring SDF
                float ring = sdRing(uv, _Radius, _Thickness);
                
                // Anti-aliased edge
                float alpha = 1.0 - smoothstep(-_Smoothness, _Smoothness, ring);
                
                // Fill mask
                float fillMask = step(normalizedAngle, _FillAmount);
                
                // Combine colors
                float4 fillColor = _Color;
                float4 bgColor = _BackgroundColor;
                
                float4 finalColor = lerp(bgColor, fillColor, fillMask);
                finalColor.a *= alpha * i.color.a;
                
                return finalColor;
            }
            ENDHLSL
        }
    }
}