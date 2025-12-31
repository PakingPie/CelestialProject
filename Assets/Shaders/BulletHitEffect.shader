
Shader "Custom/BulletHitEffect"
{
    Properties
    {
        _HitUV ("Hit UV", Vector) = (0.5, 0.5, 0, 0)
        _Edge ("Edge", Vector) = (0.2, 0.4, 0, 0)
        _Scale ("Scale", Range(0.001, 1.0)) = 1.0
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

            float4 _HitUV;
            float4 _Edge;
            float _Scale;

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
                o.PosHCS = TransformObjectToHClip(v.vertex.xyz);
                o.UV = v.uv;
                return o;
            }

            float inverseLerp(float a, float b, float value)
            {
                return saturate((value - a) / (b - a));
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 hitUV = _HitUV.xy;
                float dist = saturate(1 - distance(i.UV, hitUV));
                dist = pow(dist, 1 / _Scale);

                float edge = saturate(pow(smoothstep(_Edge.x, _Edge.y, dist), 4.0));
                float4 color = dist - edge;
                return color;
            }
            ENDHLSL
        }
    }
}