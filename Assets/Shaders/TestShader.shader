Shader "Custom/EvenSphereMapping"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0; // Pass normal to fragment
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            // --- THE MAGIC FUNCTION ---
            // Converts a 3D Normal vector into 2D Octahedral UVs
            float2 GetOctahedralUV(float3 N)
            {
                // Project to Octahedron
                N /= dot(float3(1, 1, 1), abs(N));
                
                // Unfold the back face
                if (N.z < 0)
                {
                    float2 temp = (1.0 - abs(N.yx)) * (N.xy >= 0 ? 1.0 : -1.0);
                    N.x = temp.x;
                    N.y = temp.y;
                }
                
                // Map from [-1,1] to [0,1]
                return N.xy * 0.5 + 0.5;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                
                // 2. Pass the Object Space Normal
                // We use object space so the texture sticks to the sphere when it rotates
                o.normal = v.normal; 
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // 3. Normalize the interpolated normal
                float3 normal = normalize(i.normal);

                // 4. Generate the new Even UVs
                float2 evenUV = GetOctahedralUV(normal);

                // Apply texture tiling/offset if needed
                evenUV = evenUV * _MainTex_ST.xy + _MainTex_ST.zw;

                // Sample texture
                return tex2D(_MainTex, evenUV);
            }
            ENDHLSL
        }
    }
}