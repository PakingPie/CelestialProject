Shader "Custom/HitEffectCumulative"
{
    Properties
    {
        _MainTex ("Current Texture", 2D) = "black" {}
        _HitTex ("Hit Texture", 2D) = "black" {}
        _Decay ("Decay", Range(0,1)) = 0.5
        _MinThreshold ("Minimum Threshold", Range(0,1)) = 0.02
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
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            sampler2D _MainTex;
            sampler2D _HitTex;
            float _Decay;
            float _MinThreshold;

            struct appdata 
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f 
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v) 
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target 
            {
                float4 oldCumulative = tex2D(_MainTex, i.uv);
                
                // fade
                float4 decayed = oldCumulative * _Decay;
                
                // new hit
                float4 newHit = tex2D(_HitTex, i.uv);
                
                // Max blend
                float4 result = max(decayed, newHit);
                
                // Threshold clipping - this is key!
                float maxChannel = max(max(result.r, result.g), result.b);
                if (maxChannel < _MinThreshold)
                {
                    result = float4(0, 0, 0, 0);
                }
                
                return result;
            }
            ENDHLSL
        }
    }
}
