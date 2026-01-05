Shader "Custom/BulletHitCumlative"
{
    Properties
    {
        _MainTex ("Cumulative Texture", 2D) = "black" {}
        _HitTex ("Hit Effect Texture", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "CumulativeBlend"
            Cull Off
            ZWrite Off
            ZTest Always
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            TEXTURE2D(_HitTex);
            SAMPLER(sampler_HitTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _HitTex_ST;
            CBUFFER_END

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 oldCumulative = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                
                float brightness = max(max(oldCumulative.r, oldCumulative.g), oldCumulative.b);
                
                float4 newHit = SAMPLE_TEXTURE2D(_HitTex, sampler_HitTex, input.uv);
                
                float4 result = max(oldCumulative, newHit);
                
                return result;
            }

            ENDHLSL
        }
    }
}