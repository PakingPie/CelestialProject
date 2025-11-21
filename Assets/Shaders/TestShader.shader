Shader "Custom/EvenSphereMapping"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TextureScale ("Texture Scale", Float) = 1.0
        _BlendSharpness ("Blend Sharpness", Range(1, 20)) = 4.0
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
                float3 normal : NORMAL; // 1. We need the mesh normal
                float4 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _TextureScale;
            float _BlendSharpness;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                o.uv = v.texcoord.xy;
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 3. Calculate Blending Weights
                // We take the absolute value of the normal to determine 
                // if the face is pointing mostly X, Y, or Z.
                float3 weights = abs(i.worldNormal);
                
                // Make the blend sharper (higher values = less fading between sides)
                weights = pow(weights, _BlendSharpness);
                
                // Normalize weights so they add up to 1.0
                weights = weights / (weights.x + weights.y + weights.z);

                // 4. Calculate UVs based on World Position
                // We divide by scale to adjust texture size
                float2 uvX = i.worldPos.zy / _TextureScale; // Side projection
                float2 uvY = i.worldPos.xz / _TextureScale; // Top projection
                float2 uvZ = i.worldPos.xy / _TextureScale; // Front projection

                // 5. Sample the texture 3 times
                half4 colX = tex2D(_MainTex, uvX);
                half4 colY = tex2D(_MainTex, uvY);
                half4 colZ = tex2D(_MainTex, uvZ);

                // 6. Blend them together
                half4 finalColor = colX * weights.x + colY * weights.y + colZ * weights.z;

                return finalColor;
            }
            ENDHLSL
        }
    }
}