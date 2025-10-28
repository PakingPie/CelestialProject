Shader "Custom/HitEffect"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Center ("Center", Vector) = (0,0,0,0)
        _Radius ("Radius", Float) = 0.5
        _Hardness ("Hardness", Float) = 0.5
    }
    SubShader
    {
        Tags {"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

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
                v2f o = (v2f)0;
                o.PosHCS = TransformObjectToHClip(v.vertex);
                o.UV = TRANSFORM_TEX(v.uv, _MainTex);
                o.PosWS = mul(unity_ObjectToWorld, v.vertex).xyz;
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
                return mask1 - mask2;
            }
            ENDHLSL
        }
    }
}
