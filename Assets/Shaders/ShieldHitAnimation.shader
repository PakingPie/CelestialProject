Shader "Unlit/ShieldHitAnimation"
{
    Properties
    {
        _HitEffectRT ("Hit Effect RT", 2D) = "Black" {}
    }
    SubShader
    {
        Tags {"RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"  }

        Pass
        {
            Cull Back
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma target 4.5 
            #pragma vertex vert
            #pragma fragment frag

            TEXTURE2D(_HitEffectRT);
            SAMPLER(sampler_HitEffectRT);
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
                v2f o;
                o.PosHCS = TransformObjectToHClip(v.vertex);
                o.UV = TRANSFORM_TEX(v.uv, _HitEffectRT);
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float mask = SAMPLE_TEXTURE2D(_HitEffectRT, sampler_HitEffectRT, i.UV).r;
                return float4(mask, mask, mask, 1);
            }
            ENDHLSL
        }
    }
}
