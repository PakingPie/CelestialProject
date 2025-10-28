Shader "Unlit/ShieldHitEffect"
{
    Properties
    {
        
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

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
            };

            struct v2f
            {
                float2 UV : TEXCOORD0;
                float4 HCS : SV_POSITION;
            };

            sampler2D _PreviousTex;
            sampler2D _CurrentTex;
            float4 _CurrentTex_TexelSize;

            v2f vert (appdata v)
            {
                v2f o;
                o.HCS = TransformObjectToHClip(v.vertex);
                o.UV = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 e = float3(_CurrentTex_TexelSize.xy,0);
                float2 uv = i.UV;
                float speed = 1.0f;

                float p10 = tex2D(_CurrentTex, uv - e.zy * speed).x;
                float p01 = tex2D(_CurrentTex, uv - e.xz * speed).x;
                float p21 = tex2D(_CurrentTex, uv + e.xz * speed).x;
                float p12 = tex2D(_CurrentTex, uv + e.zy * speed).x;

                float p11 = tex2D(_PreviousTex, uv).x;

                float d = (p10 + p01 + p21 + p12)/2 - p11;
                d *= 0.99f;
                return d;
            }
            ENDHLSL
        }
    }
}
