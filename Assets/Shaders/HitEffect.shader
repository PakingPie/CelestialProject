Shader "Custom/HitEffect"
{
    Properties
    {
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
                o.UV = v.uv;
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
                // float mask1 = SphereMask(i.PosWS, _Center.xyz, _Radius, _Hardness);
                // return mask1;
                // float dist = distance(i.UV, _Center.xy);
                // return float4(dist, dist, dist, 1);
            }
            ENDHLSL
        }

        Pass 
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 PosHCS : SV_POSITION;
                float2 UV : TEXCOORD0;
                float3 NormalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
               return v;
            }

            float frag (v2f i) : SV_DEPTH
            {
                return i.PosHCS.z / i.PosHCS.w;
            }
            ENDHLSL
        }

        Pass 
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 PosHCS : SV_POSITION;
                float3 NormalWS : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o = (v2f)0;
                o.PosHCS = TransformObjectToHClip(v.vertex);
                o.NormalWS = TransformObjectToWorldNormal(v.normal);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return float4(NormalizeNormalPerPixel(i.NormalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
