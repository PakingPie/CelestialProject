Shader "Custom/MyBlackHole_2"
{
    Properties
    {
        [HDR]_MainColor ("Main Color", Color) = (1,1,1,1)
        _NoiseTex ("Noise texture", 2D) = "white" {}
        _DiscTex ("Disc texture", 2D) = "white" {}
        _DiscWidth ("Width of the accretion disc", float) = 0.1
        _DiscInnerRadius ("Object relative disc inner radius", Range(0,10)) = 0.25
        _DiscOuterRadius ("Object relative outer disc radius", Range(0,10)) = 1
        _DiscSpeed ("Disc rotation speed", float) = .05
        _Steps ("Amount of steps", int) = 100
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

            Texture2D _NoiseTex;
            SAMPLER(sampler_NoiseTex);

            Texture2D _DiscTex;
            SAMPLER(sampler_DiscTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainColor;
                float _DiscWidth;
                float _DiscOuterRadius;
                float _DiscInnerRadius;
                float _DiscSpeed;
                int _Steps;
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
                float3 centerOS		: TEXCOORD1;
                float3 objectScale		: TEXCOORD2;
            };
            
            v2f vert(Attributes i)
            {
                v2f o = (v2f)0;

                o.positionCS = TransformObjectToHClip(i.vertex.xyz);
                o.positionWS = TransformObjectToWorld(i.vertex.xyz);

                // Object information, based upon Unity's shadergraph library functions
                o.centerOS = UNITY_MATRIX_M._m03_m13_m23;
                o.objectScale = float3(length(float3(UNITY_MATRIX_M[0].x, UNITY_MATRIX_M[1].x, UNITY_MATRIX_M[2].x)),
                length(float3(UNITY_MATRIX_M[0].y, UNITY_MATRIX_M[1].y, UNITY_MATRIX_M[2].y)),
                length(float3(UNITY_MATRIX_M[0].z, UNITY_MATRIX_M[1].z, UNITY_MATRIX_M[2].z)));

                return o;
            }

            float noise( in float3 x )
            {
                float3 p = floor(x);
                float3 f = frac(x);
                f = f * f * (3 - 2 * f);
                float2 uv = (p.xy + float2(37, 17)*p.z) + f.xy;
                float2 rg = SAMPLE_TEXTURE2D_X_LOD( _NoiseTex, sampler_NoiseTex, (uv + 0.5) / 256, 0).yx;
                return -1 + 2 * lerp( rg.x, rg.y, f.z );
            }

            float rand(float2 coord)
            {
                return saturate(frac(sin(dot(coord, float2(12.9898, 78.223))) * 43758.5453));
            }

            float sdTorus(float3 p, float2 t)
            {
                float2 q = float2(length(p.xz) - t.x, p.y);
                return length(q)-t.y;
            }

            float pcurve( float x, float a, float b )
            {
                float k = pow(a + b, a + b) / (pow(a, a) * pow(b, b));
                return k * pow(x, a) * pow(1.0 - x, b);
            }

            float2 intersectSphere(float3 rayOrigin, float3 rayDir, float3 center, float radius)
            {

                float3 offset = rayOrigin - center;
                const float a = 1;
                float b = 2 * dot(offset, rayDir);
                float c = dot(offset, offset) - radius * radius;

                float discriminant = b * b - 4 * a * c;
                // No intersections: discriminant < 0
                // 1 intersection: discriminant == 0
                // 2 intersections: discriminant > 0
                if (discriminant > 0)
                {
                    float s = sqrt(discriminant);
                    float dstToSphereNear = max(0, (-b - s) / (2 * a));
                    float dstToSphereFar = (-b + s) / (2 * a);

                    if (dstToSphereFar >= 0)
                    {
                        return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
                    }
                }
                // Ray did not intersect sphere
                return float2(3.402823466e+38, 0);
            }

            float remap(float v, float minOld, float maxOld, float minNew, float maxNew)
            {
                return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
            }

            void Haze(float3 center, inout float3 color, float3 pos, float alpha)
            {
                float2 t = float2(1.0, 0.01);

                float torusDist = length(sdTorus(pos - center + float3(0.0, -0.05, 0.0), t));

                float bloomDisc = 1.0 / (pow(torusDist, 2.0) + 0.001);
                float3 col = _MainColor;
                bloomDisc *= length(pos - center) < 0.5 ? 0.0 : 1.0;

                color += col * bloomDisc * (2.9 / float(_Steps)) * (1.0 - alpha * 1.0);
            }

            void WarpSpace(float3 center, inout float3 eyevec, inout float3 currentRayPos)
            {
                float3 origin = center;

                float singularityDist = distance(currentRayPos, origin);
                float warpFactor = 1.0 / (pow(singularityDist, 2.0) + 0.000001);

                float3 singularityVector = normalize(origin - currentRayPos);
                
                float warpAmount = 5.0;

                eyevec = normalize(eyevec + singularityVector * warpFactor * warpAmount / float(_Steps));
            }

            void GasDisc(float3 center, float stepSize, inout float3 color, inout float alpha, float3 pos)
            {
                float discWidth = _DiscWidth;
                float discInner = _DiscInnerRadius;
                float discOuter = _DiscOuterRadius;
                
                float3 origin = center;
                float3 discNormal = normalize(float3(0.0, 1.0, 0.0));
                float discThickness = 0.1;

                float distFromCenter = distance(pos, origin);
                float distFromDisc = dot(discNormal, pos - origin);
                
                float radialGradient = 1.0 - saturate((distFromCenter - discInner) / discWidth * 0.5);

                float coverage = pcurve(radialGradient, 4.0, 0.9);

                discThickness *= radialGradient;
                coverage *= saturate(1.0 - abs(distFromDisc) / discThickness);

                float3 dustColorLit = _MainColor;
                float3 dustColorDark = float3(0.0, 0.0, 0.0);

                float dustGlow = 1.0 / (pow(1.0 - radialGradient, 2.0) * 290.0 + 0.002);
                float3 dustColor = dustColorLit * dustGlow * 8.2;

                coverage = saturate(coverage * 0.7);


                float fade = pow((abs(distFromCenter - discInner) + 0.4), 4.0) * 0.04;
                float bloomFactor = 1.0 / (pow(distFromDisc, 2.0) * 40.0 + fade + 0.00002);
                float3 b = dustColorLit * pow(bloomFactor, 1.5);
                
                b *= lerp(float3(1.7, 1.1, 1.0), float3(0.5, 0.6, 1.0), pow(radialGradient, 2.0));
                b *= lerp(float3(1.7, 0.5, 0.1), 1.0, pow(radialGradient, 0.5));

                dustColor = lerp(dustColor, b * 150.0, saturate(1.0 - coverage * 1.0));
                coverage = saturate(coverage + bloomFactor * bloomFactor * 0.1);
                
                if (coverage < stepSize)
                {
                    return;   
                }
                
                float3 radialCoords;
                radialCoords.x = distFromCenter * 1.5 + 0.55;
                radialCoords.y = atan2(-pos.x, -pos.z) * 1.5;
                radialCoords.z = distFromDisc * 1.5;

                radialCoords *= 0.95;
                
                float speed = _DiscSpeed;
                
                float noise1 = 1.0;
                float3 rc = radialCoords + 0.0;             rc.y += _Time.y * speed;
                noise1 *= noise(rc * 3.0) * 0.5 + 0.5;      rc.y -= _Time.y * speed;
                noise1 *= noise(rc * 6.0) * 0.5 + 0.5;      rc.y += _Time.y * speed;
                noise1 *= noise(rc * 12.0) * 0.5 + 0.5;     rc.y -= _Time.y * speed;
                noise1 *= noise(rc * 24.0) * 0.5 + 0.5;     rc.y += _Time.y * speed;

                float noise2 = 2.0;
                rc = radialCoords + 30.0;
                noise2 *= noise(rc * 3.0) * 0.5 + 0.5;      rc.y += _Time.y * speed;
                noise2 *= noise(rc * 6.0) * 0.5 + 0.5;      rc.y -= _Time.y * speed;
                noise2 *= noise(rc * 12.0) * 0.5 + 0.5;     rc.y += _Time.y * speed;
                noise2 *= noise(rc * 24.0) * 0.5 + 0.5;     rc.y -= _Time.y * speed;
                noise2 *= noise(rc * 48.0) * 0.5 + 0.5;     rc.y += _Time.y * speed;
                noise2 *= noise(rc * 92.0) * 0.5 + 0.5;     rc.y -= _Time.y * speed;

                dustColor *= noise1 * 0.998 + 0.002;
                coverage *= noise2;
                
                radialCoords.y += _Time.y * speed * 0.5;
                
                dustColor *= pow(SAMPLE_TEXTURE2D_X(_DiscTex, sampler_DiscTex, radialCoords.yx * float2(0.15, 0.27)).rgb, 2.0) * 4.0;

                coverage = saturate(coverage * 1200.0 / float(_Steps));
                dustColor = max(0, dustColor);

                coverage *= pcurve(radialGradient, 4.0, 0.9);

                color = (1.0 - alpha) * dustColor * coverage + color;

                alpha = (1.0 - alpha) * coverage + alpha;
            }


            float4 frag(v2f i) : SV_Target
            {
                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(i.positionWS - _WorldSpaceCameraPos);
                float3 center = i.centerOS;
                float2 screenUV = i.positionCS.xy / _ScreenParams.xy;

                float sphereRadius = 0.5 * min(min(i.objectScale.x, i.objectScale.y), i.objectScale.z);
                float2 outerSphereIntersection = intersectSphere(rayOrigin, rayDir, center, sphereRadius);

                float dither = rand(screenUV) * 2.0;

                float blackHoleMask = 0;

                float alpha = 0.0;
                float3 currentRayPos = rayOrigin + rayDir * dither * 10.0 / float(_Steps);

                float3 color = float3(0.0, 0.0, 0.0);

                float3 currentRayDir = rayDir;
                float stepSize = length(rayOrigin - center) * 10.0 / float(_Steps);
                
                UNITY_LOOP
                for (int i = 0; i < _Steps; i++)
                {
                    float3 dirToCentre = center - currentRayPos;
                    float dstToCentre = length(dirToCentre);
                    dirToCentre /= dstToCentre;

                    float force = _GConst/(dstToCentre * dstToCentre);
                    currentRayDir = normalize(currentRayDir + dirToCentre * force * stepSize);

                    float blackHoleDistance = intersectSphere(currentRayPos, rayDir, center, _SSRadius * sphereRadius);
                    if(blackHoleDistance <= stepSize)
                    {
                        blackHoleMask = 1;
                        break;
                    }
                    WarpSpace(center, currentRayDir, currentRayPos);
                    currentRayPos += currentRayDir * stepSize;
                    GasDisc(center, stepSize, color, alpha, currentRayPos);
                    Haze(center, color, currentRayPos, alpha);
                }

                float transmittance = 0;

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
                float3 backgroundCol = SampleSceneColor(distortedScreenUV) * (1 - blackHoleMask);

                


                return float4(lerp(backgroundCol, color, alpha), 1);
            }
            ENDHLSL
        }
    }
}
