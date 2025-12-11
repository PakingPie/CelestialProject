Shader "SDFs/Hexagon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _HexagonScale ("Hexagon Scale", float) = 1
    }
    SubShader
    {
        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _Color;
            float _HexagonScale;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // const float2 s = float2(1.7320508, 1.0f);

            

            float4 frag(v2f i) : SV_Target
            {
                const float2 s = float2(1.732051f, 1.0f);
                float2 p = i.uv * _HexagonScale;
                float4 hC = floor(float4(p, p - float2(1, 0.5)) / s.xyxy) + 0.5;
                float4 h = float4(p - hC.xy * s, p - (hC.zw + 0.5) * s);
                float4 hexagonUV = 0;
                if(dot(h.xy, h.xy) < dot(h.zw, h.zw))
                {
                    hexagonUV = float4(h.xy, hC.xy);
                }
                else
                {
                    hexagonUV = float4(h.zw, hC.zw + 0.5);
                }

                float sdf = max(dot(abs(hexagonUV.xy), s * float2(0.5, 0.5)), hexagonUV.g);

                float hexagon = lerp(1, 0, smoothstep(0, 0.03, sdf - 0.5 + 0.04));
                
                return float4(hexagon, hexagon, hexagon, 1) * _Color;
            }   

            ENDHLSL
        }
    }
}