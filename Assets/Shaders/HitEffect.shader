Shader "Unlit/Test"
{
    Properties
    {
        _Center ("Center", Vector) = (0,0,0,0)
        _Radius ("Radius", Float) = 0.5
        _Hardness ("Hardness", Float) = 0.5
    }
    SubShader
    {
        Tags {"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"  }

        Pass
        {
            ZTest Always
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag

            float4 _Center;
            float _Radius;
            float _Hardness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 PosHCS : SV_POSITION;
                float2 UV : TEXCOORD0;
                float3 PosWS : TEXCOORD1;
            };


            v2f vert (appdata v)
            {
                v2f o;
                o.PosHCS = TransformObjectToHClip(v.vertex);
                o.UV = v.uv;
                o.PosWS = TransformObjectToWorld(v.vertex).xyz;
                return o;
            }

            float SphereMask(float3 Coords, float3 Center, float Radius, float Hardness)
            {
                return 1 - saturate((distance(Coords, Center) - Radius) / (1 - Hardness));
            }

            half4 frag (v2f i) : SV_Target
            {
                float mask1 = SphereMask(i.PosWS, _Center.xyz, _Radius, _Hardness);
                float mask2 = SphereMask(i.PosWS, _Center.xyz, _Radius * 0.5, _Hardness);
                return saturate(mask1 - mask2);
            }
            ENDHLSL
        }
    }
}
