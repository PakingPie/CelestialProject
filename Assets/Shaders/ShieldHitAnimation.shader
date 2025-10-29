Shader "Unlit/ShieldHitAnimation"
{
    Properties
    {
        _HitEffectRT ("Hit Effect RT", 2D) = "Black" {}
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

            sampler2D _HitEffectRT;
            float4 _HitEffectRT_ST;

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

            float4 frag (v2f i) : SV_Target
            {
                float4 mask = tex2D(_HitEffectRT, i.UV);
                return mask;
            }
            ENDHLSL
        }

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
