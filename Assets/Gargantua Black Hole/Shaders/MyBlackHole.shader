Shader "Custom/MyBlackHole"
{
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" "Queue" = "Transparent" }
        Cull Front
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"       

            static const float maxFloat = 3.402823466e+38;

            struct Attributes
            {
                float4 vertex	: POSITION;
            };

            struct v2f
            {
                float4 positionCS		: SV_POSITION;
                float3 positionWS		: TEXCOORD0;
                float3 centerPosOS		: TEXCOORD1;
                float3 objectScale		: TEXCOORD2;
            };

            v2f vert(Attributes IN)
            {
                v2f o = (v2f)0;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.vertex.xyz);

                o.positionCS = vertexInput.positionCS;
                o.positionWS = vertexInput.positionWS;

                // Object information, based upon Unity's shadergraph library functions
                o.centerPosOS = UNITY_MATRIX_M._m03_m13_m23;
                o.objectScale = float3(length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x)),
                length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y)),
                length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z)));

                return o;
            }

            // Based upon https://viclw17.github.io/2018/07/16/raytracing-ray-sphere-intersection/#:~:text=When the ray and sphere,equations and solving for t.
            // Returns dstToSphere, dstThroughSphere
            // If inside sphere, dstToSphere will be 0
            // If ray misses sphere, dstToSphere = max float value, dstThroughSphere = 0
            // Given rayDir must be normalized
            float2 intersectSphere(float3 rayOrigin, float3 rayDir, float3 center, float radius) {

                float3 offset = rayOrigin - center;
                const float a = 1;
                float b = 2 * dot(offset, rayDir);
                float c = dot(offset, offset) - radius * radius;

                float discriminant = b * b - 4 * a*c;
                // No intersections: discriminant < 0
                // 1 intersection: discriminant == 0
                // 2 intersections: discriminant > 0
                if (discriminant > 0) {
                    float s = sqrt(discriminant);
                    float dstToSphereNear = max(0, (-b - s) / (2 * a));
                    float dstToSphereFar = (-b + s) / (2 * a);

                    if (dstToSphereFar >= 0) {
                        return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
                    }
                }
                // Ray did not intersect sphere
                return float2(maxFloat, 0);
            }

            float4 frag (v2f i) : SV_Target
            {
                // Initial ray information
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.positionWS - _WorldSpaceCameraPos);

                float sphereRadius = 0.5 * min(min(i.objectScale.x, i.objectScale.y), i.objectScale.z);
                float2 outerSphereIntersection = intersectSphere(rayOrigin, rayDir, i.centerPosOS, sphereRadius);

                // Raymarching information
                float transmittance = 0;

                // Ray intersects with the outer sphere
                if(outerSphereIntersection.x < maxFloat)
                transmittance = 1;

                float2 screenUV = i.positionCS.xy / _ScreenParams.xy;
                float3 backgroundCol = SampleSceneColor(screenUV);

                float3 col = lerp(backgroundCol, float3(1,0,0), transmittance);
                return float4(col,1);
            }
            ENDHLSL
        }
    }
}