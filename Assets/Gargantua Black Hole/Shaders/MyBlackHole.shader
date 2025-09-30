Shader "Custom/MyBlackHole"
{
    Properties
    {
        _DiscTex ("Disc texture", 2D) = "white" {}
        _DiscWidth ("Width of the accretion disc", float) = 0.1
        _DiscInnerRadius ("Object relative disc inner radius", Range(0,1)) = 0.25
        _DiscOuterRadius ("Object relative outer disc radius", Range(0,1)) = 1
    }
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
            float3 samplePos = float3(maxFloat, 0, 0);

            Texture2D _DiscTex;

            SAMPLER(sampler_DiscTex);

            CBUFFER_START(UnityPerMaterial)
                float _DiscWidth;
                float _DiscOuterRadius;
                float _DiscInnerRadius;
            CBUFFER_END

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

            // Based upon https://mrl.cs.nyu.edu/~dzorin/rend05/lecture2.pdf
            float2 intersectInfiniteCylinder(float3 rayOrigin, float3 rayDir, float3 cylinderOrigin, float3 cylinderDir, float cylinderRadius)
            {
                float3 a0 = rayDir - dot(rayDir, cylinderDir) * cylinderDir;
                float a = dot(a0,a0);

                float3 dP = rayOrigin - cylinderOrigin;
                float3 c0 = dP - dot(dP, cylinderDir) * cylinderDir;
                float c = dot(c0,c0) - cylinderRadius * cylinderRadius;

                float b = 2 * dot(a0, c0);

                float discriminant = b * b - 4 * a * c;

                if (discriminant > 0) {
                    float s = sqrt(discriminant);
                    float dstToNear = max(0, (-b - s) / (2 * a));
                    float dstToFar = (-b + s) / (2 * a);

                    if (dstToFar >= 0) {
                        return float2(dstToNear, dstToFar - dstToNear);
                    }
                }
                return float2(maxFloat, 0);
            }

            // Based upon https://mrl.cs.nyu.edu/~dzorin/rend05/lecture2.pdf
            float intersectInfinitePlane(float3 rayOrigin, float3 rayDir, float3 planeOrigin, float3 planeDir)
            {
                float a = 0;
                float b = dot(rayDir, planeDir);
                float c = dot(rayOrigin, planeDir) - dot(planeDir, planeOrigin);

                return -c/b;
            }

            // Based upon https://mrl.cs.nyu.edu/~dzorin/rend05/lecture2.pdf
            float intersectDisc(float3 rayOrigin, float3 rayDir, float3 p1, float3 p2, float3 discDir, float discRadius, float innerRadius)
            {
                float discDst = maxFloat;
                float2 cylinderIntersection = intersectInfiniteCylinder(rayOrigin, rayDir, p1, discDir, discRadius);
                float cylinderDst = cylinderIntersection.x;

                if(cylinderDst < maxFloat)
                {
                    float finiteC1 = dot(discDir, rayOrigin + rayDir * cylinderDst - p1);
                    float finiteC2 = dot(discDir, rayOrigin + rayDir * cylinderDst - p2);

                    // Ray intersects with edges of the cylinder/disc
                    if(finiteC1 > 0 && finiteC2 < 0 && cylinderDst > 0)
                    {
                        discDst = cylinderDst;
                    }
                    else
                    {
                        float radiusSqr = discRadius * discRadius;
                        float innerRadiusSqr = innerRadius * innerRadius;

                        float p1Dst = max(intersectInfinitePlane(rayOrigin, rayDir, p1, discDir), 0);
                        float3 q1 = rayOrigin + rayDir * p1Dst;
                        float p1q1DstSqr = dot(q1 - p1, q1 - p1);

                        // Ray intersects with lower plane of cylinder/disc
                        if(p1Dst > 0 && p1q1DstSqr < radiusSqr && p1q1DstSqr > innerRadiusSqr)
                        {
                            if(p1Dst < discDst)
                            {
                                discDst = p1Dst;
                            }
                        }

                        float p2Dst = max(intersectInfinitePlane(rayOrigin, rayDir, p2, discDir), 0);
                        float3 q2 = rayOrigin + rayDir * p2Dst;
                        float p2q2DstSqr = dot(q2 - p2, q2 - p2);

                        // Ray intersects with upper plane of cylinder/disc
                        if(p2Dst > 0 && p2q2DstSqr < radiusSqr && p2q2DstSqr > innerRadiusSqr)
                        {
                            if(p2Dst < discDst)
                            {
                                discDst = p2Dst;
                            }
                        }
                    }
                }

                return discDst;
            }

            float remap(float v, float minOld, float maxOld, float minNew, float maxNew) {
                return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
            }

            float2 discUV(float3 planarDiscPos, float3 discDir, float3 centre, float radius)
            {
                float3 planarDiscPosNorm = normalize(planarDiscPos);
                float sampleDist01 = length(planarDiscPos) / radius;

                float3 tangentTestVector = float3(1,0,0);
                if(abs(dot(discDir, tangentTestVector)) >= 1)
                tangentTestVector = float3(0,1,0);

                float3 tangent = normalize(cross(discDir, tangentTestVector));
                float3 biTangent = cross(tangent, discDir);
                float phi = atan2(dot(planarDiscPosNorm, tangent), dot(planarDiscPosNorm, biTangent)) / PI;
                phi = remap(phi, -1, 1, 0, 1);

                // Radial distance
                float u = sampleDist01;
                // Angular distance
                float v = phi;

                return float2(u,v);
            }

            float4 frag (v2f i) : SV_Target
            {
                // Initial ray information
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.positionWS - _WorldSpaceCameraPos);

                float sphereRadius = 0.5 * min(min(i.objectScale.x, i.objectScale.y), i.objectScale.z);
                float2 outerSphereIntersection = intersectSphere(rayOrigin, rayDir, i.centerPosOS, sphereRadius);

                // Disc information, direction is objects rotation
                float3 discDir = normalize(mul(unity_ObjectToWorld, float4(0,1,0,0)).xyz);
                float3 p1 = i.centerPosOS - 0.5 * _DiscWidth * discDir;
                float3 p2 = i.centerPosOS + 0.5 * _DiscWidth * discDir;
                float discRadius = sphereRadius * _DiscOuterRadius;
                float innerRadius = sphereRadius * _DiscInnerRadius;

                // Raymarching information
                float4 transmittance = float4(0,0,0,0);

                // // Ray intersects with the outer sphere
                // if(outerSphereIntersection.x < maxFloat)
                // transmittance = 1;

                // Ray intersects with the outer sphere
                if(outerSphereIntersection.x < maxFloat)
                {
                    float discDst = intersectDisc(rayOrigin, rayDir, p1, p2, discDir, discRadius, innerRadius);
                    if(discDst < maxFloat)
                    {
                        transmittance = float4(1,1,1,1);
                        samplePos = rayOrigin + rayDir * discDst;
                    }
                }

                float2 uv = float2(0,0);
                float3 planarDiscPos = float3(0,0,0);
                if(samplePos.x < maxFloat)
                {
                    planarDiscPos = samplePos - dot(samplePos - i.centerPosOS, discDir) * discDir - i.centerPosOS;
                    uv = discUV(planarDiscPos, discDir, i.centerPosOS, discRadius);
                }

                float4 discCol = SAMPLE_TEXTURE2D(_DiscTex, sampler_DiscTex, uv);

                float2 screenUV = i.positionCS.xy / _ScreenParams.xy;
                float3 backgroundCol = SampleSceneColor(screenUV);

                float4 col = lerp(float4(backgroundCol, 1), discCol, transmittance * discCol.a);
                return col;
            }
            ENDHLSL
        }
    }
}