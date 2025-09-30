Shader "Custom/MyBlackHole"
{
    Properties
    {
        _DiscTex ("Disc texture", 2D) = "white" {}
        _DiscWidth ("Width of the accretion disc", float) = 0.1
        _DiscInnerRadius ("Object relative disc inner radius", Range(0,1)) = 0.25
        _DiscOuterRadius ("Object relative outer disc radius", Range(0,1)) = 1
        _DiscSpeed ("Disc rotation speed", float) = 2
        [HDR]_DiscColor ("Disc main color", Color) = (1,0,0,1)
        _DopplerBeamingFactor ("Doppler beaming effect factor", float) = 66
        _HueRadius ("Hue shift start radius", Range(0,1)) = 0.75
        _HueShiftFactor ("Hue shifting factor", float) = -0.03
        _Steps ("Amount of steps", int) = 256
        _StepSize ("Step size", Range(0.001, 1)) = 0.1
        _SSRadius ("Object relative Schwarzschild radius", Range(0,1)) = 0.2
        _GConst ("Gravitational constant", float) = 0.15
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
                float _DiscSpeed;
                float4 _DiscColor;
                float _DopplerBeamingFactor;
                float _HueRadius;
                float _HueShiftFactor;
                int _Steps;
                float _StepSize;
                float _SSRadius;
                float _GConst;
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

            #include "./MyBlackHoleUtilities.hlsl"

            float4 frag (v2f i) : SV_Target
            {
                // Initial ray information
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.positionWS - _WorldSpaceCameraPos);
                float3 center = i.centerPosOS;

                float sphereRadius = 0.5 * min(min(i.objectScale.x, i.objectScale.y), i.objectScale.z);
                float2 outerSphereIntersection = intersectSphere(rayOrigin, rayDir, center, sphereRadius);

                // Disc information, direction is objects rotation
                float3 discDir = normalize(mul(unity_ObjectToWorld, float4(0,1,0,0)).xyz);
                float3 p1 = i.centerPosOS - 0.5 * _DiscWidth * discDir;
                float3 p2 = i.centerPosOS + 0.5 * _DiscWidth * discDir;
                float discRadius = sphereRadius * _DiscOuterRadius;
                float innerRadius = sphereRadius * _DiscInnerRadius;

                // Raymarching information
                float transmittance = 0;
                float blackHoleMask = 0;
                float3 samplePos = float3(maxFloat, 0, 0);
                float3 currentRayPos = rayOrigin + rayDir * outerSphereIntersection.x;
                float3 currentRayDir = rayDir;

                // Ray intersects with the outer sphere
                if(outerSphereIntersection.x < maxFloat)
                {
                    for (int i = 0; i < _Steps; i++)
                    {
                        float3 dirToCentre = center - currentRayPos;
                        float dstToCentre = length(dirToCentre);
                        dirToCentre /= dstToCentre;

                        if(dstToCentre > sphereRadius + _StepSize)
                        {
                            break;
                        }

                        float force = _GConst/(dstToCentre*dstToCentre);
                        currentRayDir = normalize(currentRayDir + dirToCentre * force * _StepSize);

                        // Move ray forward
                        currentRayPos += currentRayDir * _StepSize;

                        float blackHoleDistance = intersectSphere(currentRayPos, currentRayDir, center, _SSRadius * sphereRadius);
                        if(blackHoleDistance <= _StepSize)
                        {
                            blackHoleMask = 1;
                            break;
                        }

                        // Check for disc intersection nearby
                        float discDst = intersectDisc(currentRayPos, currentRayDir, p1, p2, discDir, discRadius, innerRadius);
                        if(transmittance < 1 && discDst < _StepSize)
                        {
                            transmittance = 1;
                            samplePos = currentRayPos + currentRayDir * discDst;
                        }
                    }
                }

                float2 uv = float2(0,0);
                float3 planarDiscPos = float3(0,0,0);
                if(samplePos.x < maxFloat)
                {
                    planarDiscPos = samplePos - dot(samplePos - i.centerPosOS, discDir) * discDir - i.centerPosOS;
                    uv = discUV(planarDiscPos, discDir, i.centerPosOS, discRadius);
                    uv.y += _Time.x * _DiscSpeed;
                }

                float3 discCol = discColor(_DiscColor.rgb, planarDiscPos, discDir, _WorldSpaceCameraPos, uv.x, discRadius);


                float4 texCol = SAMPLE_TEXTURE2D(_DiscTex, sampler_DiscTex, uv);

                float2 screenUV = i.positionCS.xy / _ScreenParams.xy;
                // Ray direction projection
                float3 distortedRayDir = normalize(currentRayPos - rayOrigin);
                float4 rayCameraSpace = mul(unity_WorldToCamera, float4(distortedRayDir,0));
                float4 rayUVProjection = mul(unity_CameraProjection, float4(rayCameraSpace));
                float2 distortedScreenUV = rayUVProjection.xy + 1 * 0.5;

                // Screen and object edge transitions
                float edgeFadex = smoothstep(0, 0.25, 1 - abs(remap(screenUV.x, 0, 1, -1, 1)));
                float edgeFadey = smoothstep(0, 0.25, 1 - abs(remap(screenUV.y, 0, 1, -1, 1)));
                float t = saturate(remap(outerSphereIntersection.y, sphereRadius, 2 * sphereRadius, 0, 1)) * edgeFadex * edgeFadey;
                distortedScreenUV = lerp(screenUV, distortedScreenUV, t);


                float3 backgroundCol = SampleSceneColor(distortedScreenUV) *  (1 - blackHoleMask);

                transmittance *= texCol.r * _DiscColor.a;

                float4 col = float4(0, 0, 0, 1);
                col.rgb = lerp(backgroundCol, discCol, transmittance);
                return col;
            }
            ENDHLSL
        }
    }
}