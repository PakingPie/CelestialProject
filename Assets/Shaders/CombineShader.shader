Shader "Unlit/Combine"
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex vert
            #pragma fragment frag


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

            sampler2D _ObjectRT;
            sampler2D _CurrentRT;
            float4 _ObjectRT_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.HCS = TransformObjectToHClip(v.vertex);
                o.UV = TRANSFORM_TEX(v.uv, _ObjectRT);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // sample the texture
                half4 tex1 = tex2D(_ObjectRT, i.UV);
                half4 tex2 = tex2D(_CurrentRT, i.UV);
                return tex1 + tex2;
            }
            ENDHLSL
        }
    }
}
