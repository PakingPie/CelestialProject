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
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

struct v2f
{
    float4 vertex : SV_POSITION;
    float3 objPos : TEXCOORD0;
    float3 objNormal : TEXCOORD1;
};

v2f vert (appdata v)
{
    v2f o;
    o.vertex = TransformObjectToHClip(v.vertex);
    o.objPos = v.vertex.xyz;
    o.objNormal = v.normal;
    return o;
}

float HexagonSDF(float2 p, float2 s)
{
    float4 hC = floor(float4(p, p - float2(1, 0.5)) / s.xyxy) + 0.5;
    float4 h = float4(p - hC.xy * s, p - (hC.zw + 0.5) * s);
    
    float2 hexUV;
    if(dot(h.xy, h.xy) < dot(h.zw, h.zw))
        hexUV = h.xy;
    else
        hexUV = h.zw;
    
    return max(dot(abs(hexUV), s * 0.5), abs(hexUV.y));
}

float4 frag(v2f i) : SV_Target
{
    const float2 s = float2(1.732051f, 1.0f);
    
    float3 blend = abs(normalize(i.objNormal));
    blend = pow(blend, 4); // Sharpen the blend
    blend /= (blend.x + blend.y + blend.z);
    
    float3 pos = i.objPos * _HexagonScale;
    
    // Sample hexagon from 3 projections
    float sdfX = HexagonSDF(pos.yz, s);
    float sdfY = HexagonSDF(pos.xz, s);
    float sdfZ = HexagonSDF(pos.xy, s);
    
    // Blend based on normal direction
    float sdf = sdfX * blend.x + sdfY * blend.y + sdfZ * blend.z;
    
    float hexagon = 1 - smoothstep(0.46, 0.5, sdf);
    
    return float4(hexagon, hexagon, hexagon, 1) * _Color;
}

            ENDHLSL
        }
    }
}