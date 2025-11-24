Shader "Custom/ShieldHitCumulative"
{
    Properties
    {
        _MainTex ("Cumulative Texture", 2D) = "black" {}
        _HitTex ("Hit Effect Texture", 2D) = "black" {}
        _Decay ("Decay Factor", Range(0, 1)) = 0.9
        _DecaySpeed ("Decay Speed", Range(0, 10)) = 2.0  // 新增
        _MinThreshold ("Min Threshold", Range(0, 0.1)) = 0.01  // 新增
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
                float _Decay;
                float _DecaySpeed;
                float _MinThreshold;
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
                float dynamicDecay = lerp(0.5, _Decay, brightness);
                
                float4 decayed = oldCumulative * dynamicDecay;
                float4 newHit = SAMPLE_TEXTURE2D(_HitTex, sampler_HitTex, input.uv);
                
                float4 result = max(decayed, newHit);
                
                if (result.r < 0.01 && result.g < 0.01 && result.b < 0.01)
                result = float4(0, 0, 0, 0);
                
                return result;
            }
            ENDHLSL
        }
    }
}