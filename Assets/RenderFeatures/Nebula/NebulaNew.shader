// Nebula.shader  (Hidden/Nebula/Raymarch)
// Full-screen volumetric nebula — designed for NebulaRenderFeature.
//
// Pass 0  NebulaRaymarch   – writes HDR nebula colour to a half-res RT (additive)
// Pass 1  TemporalBlend    – blends current frame with reprojected history
// Pass 2  Composite        – bilinear upscale + additive blit onto camera colour

Shader "Hidden/Nebula/Raymarch"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        // ─── Constants ───

        #define PI     3.14159265359
        #define TWO_PI 6.28318530718

        #define CLOUD_EXTENT   10.0
        #define LOCAL_TO_NOISE 20.0

        static const float3 minCorner = float3(-CLOUD_EXTENT, -CLOUD_EXTENT, -CLOUD_EXTENT);
        static const float3 maxCorner = float3( CLOUD_EXTENT,  CLOUD_EXTENT,  CLOUD_EXTENT);

        static const float3 BETA_RAYLEIGH = 100.0 * float3(0.05802, 0.14558, 0.331);
        static const float3 BETA_OZONE    = float3(0.650, 1.881, 0.085);
        static const float3 sigmaS = 2.0 * BETA_RAYLEIGH;
        static const float3 sigmaE = 4.0 * (BETA_RAYLEIGH + 3.0 * BETA_OZONE);

        static const float goldenRatio = 1.61803398875;

        // ─── Per-volume globals ───

        float4x4 _NebulaWorldToLocal;
        float4x4 _NebulaLocalToWorld;
        float4   _AxisStretch;
        float4   _NebulaColor;
        float    _Power;
        float    _FadeInnerRadius;
        float    _FadeOuterRadius;
        float    _FadeNoiseStrength;
        float    _FadeBoxMargin;
        float    _ShapeNoiseScale;
        float    _ShapeTendrilStrength;
        float    _NoiseDomainHalf;
        float    _EnableStars;
        float    _StarDensity;
        float    _StarBrightness;
        float    _DitherSpeed;
        float    _EmissionStrength;
        float4   _ColorLowDensity;
        float4   _ColorMidDensity;
        float4   _ColorHighDensity;
        float    _DetailStrength;
        float    _VoidStrength;
        float    _DensityContrast;
        int      _StepsPrimary;
        int      _StepsLight;

        TEXTURE3D(_NoiseVolume);   SAMPLER(sampler_NoiseVolume);
        TEXTURE2D(_BlueNoise);     SAMPLER(sampler_BlueNoise);

        // Temporal + composite
        TEXTURE2D(_NebulaTexture);  SAMPLER(sampler_NebulaTexture);
        TEXTURE2D(_HistoryTexture); SAMPLER(sampler_HistoryTexture);
        float    _TemporalBlendFactor;

        // *** FIX: declare the previous-frame VP matrix ***
        float4x4 _NebulaPrevVP;

        // ─── Shared structs ───

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv         : TEXCOORD0;
            float2 ndc        : TEXCOORD1;
        };

        Varyings FullscreenVert(Attributes input)
        {
            Varyings o;
            float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
            float2 uv  = GetFullScreenTriangleTexCoord(input.vertexID);

            o.positionCS = pos;
            o.uv         = uv;
            o.ndc        = pos.xy;
            return o;
        }

        // ─── AABB helpers ───

        float2 intersectAABB(float3 ro, float3 rd, float3 bmin, float3 bmax)
        {
            float3 tMin = (bmin - ro) / rd;
            float3 tMax = (bmax - ro) / rd;
            float3 t1   = min(tMin, tMax);
            float3 t2   = max(tMin, tMax);
            return float2(max(max(t1.x, t1.y), t1.z),
                          min(min(t2.x, t2.y), t2.z));
        }

        bool insideAABB(float3 p)
        {
            const float e = 1e-4;
            return all(p > minCorner - e) && all(p < maxCorner + e);
        }

        bool getCloudIntersection(float3 org, float3 dir,
            out float distToStart, out float totalDist)
        {
            float2 t = intersectAABB(org, dir, minCorner, maxCorner);
            if (insideAABB(org)) t.x = 1e-4;
            distToStart = t.x;
            totalDist   = t.y - t.x;
            return (t.x > 0.0) && (t.x < t.y);
        }

        // ─── 3D noise (4-channel) ───

        float4 sampleNoise4(float3 q)
        {
            float3 uvw = q / (2.0 * _NoiseDomainHalf) + 0.5;
            return SAMPLE_TEXTURE3D_LOD(_NoiseVolume, sampler_NoiseVolume, uvw, 0);
        }

        float sampleNoise(float3 q)
        {
            return sampleNoise4(q).r;
        }

        float clouds(float3 p)
        {
            if (!insideAABB(p)) return 0.0;

            float4 n = sampleNoise4(0.25 * p);
            // R = low-freq structure, G = mid-freq detail, B = fine wisps, A = cellular voids

            float base    = n.r;
            float detail  = n.g * 0.5 + n.b * 0.25;
            float voids   = n.a;

            float radial  = length(p) / CLOUD_EXTENT;
            float envelope = smoothstep(0.1, 0.4, radial) * smoothstep(1.0, 0.7, radial);

            // Subtract detail for edge erosion, multiply voids for cavity carving
            float density = saturate(base - detail * _DetailStrength);
            density *= lerp(1.0, 1.0 - voids, _VoidStrength);
            density  = max(density, 0.0);
            density *= envelope;
            density  = pow(density, _DensityContrast);

            return density;
        }

        // Density-driven color gradient
        float3 densityColor(float density)
        {
            // Map density [0,1] through a 3-stop gradient
            float t = saturate(density * 3.0); // remap so we use the full gradient
            float3 col = (t < 0.5)
                ? lerp(_ColorLowDensity.rgb, _ColorMidDensity.rgb, t * 2.0)
                : lerp(_ColorMidDensity.rgb, _ColorHighDensity.rgb, (t - 0.5) * 2.0);
            return col;
        }

        // ─── Utility ───

        float getGlow(float dist, float radius, float intensity)
        {
            return max(0.0, pow(radius / max(dist, 1e-5), intensity));
        }

        float3 hash(float3 p3)
        {
            p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yxz + 33.33);
            return 2.0 * frac((p3.xxy + p3.yxx) * p3.zyx) - 1.0;
        }

        float3 getColour(float t)
        {
            float3 a = float3(0.65, 0.65, 0.65);
            float3 b = 1.0 - a;
            float3 c = float3(1.0, 1.0, 1.0);
            float3 d = float3(0.15, 0.5, 0.75);
            return pow(a + b * cos(TWO_PI * (c * t + d)), 2.2);
        }

        float3 getStars(float3 p)
        {
            p *= 0.2;
            float3 bestRand = 0;
            float  bestDist = 1e10;
            float3 bestCell = 0;

            [loop] for (int x = -1; x <= 1; x++)
            [loop] for (int y = -1; y <= 1; y++)
            [loop] for (int z = -1; z <= 1; z++)
            {
                float3 cellIdx = floor(p) + float3(x, y, z);
                float3 h       = hash(cellIdx);
                float3 f       = cellIdx + 0.5 + 0.5 * h;
                float  dd      = length(p - f);
                if (dd < bestDist)
                {
                    bestDist = dd;
                    bestRand = h;
                    bestCell = cellIdx;
                }
            }

            bestRand     = saturate(0.5 + 0.5 * bestRand);
            float3 rand2 = saturate(0.5 + 0.5 * hash(bestCell + float3(3.12, 104.9, -9.5)));

            return _StarBrightness
                 * bestRand.z
                 * step(1.0 - saturate(_StarDensity), rand2.z)
                 * lerp(1.0, getColour(bestRand.y), 0.3)
                 * smoothstep(0.5, 0.0, bestDist)
                 * getGlow(bestDist, 0.25, 2.0);
        }

        float HenyeyGreenstein(float g, float costh)
        {
            float denom = abs(1.0 + g * g - 2.0 * g * costh);
            return (1.0 / (4.0 * PI)) * ((1.0 - g * g) / pow(denom, 1.5));
        }

        float3 multipleOctaves(float extinction, float mu, float stepL)
        {
            float3 luminance = 0;
            float a = 1.0, b = 1.0, c = 1.0;

            [loop] for (float i = 0.0; i < 6.0; i += 1.0)
            {
                float phase = lerp(HenyeyGreenstein(-0.1 * c, mu),
                                   HenyeyGreenstein( 0.3 * c, mu), 0.7);
                luminance += b * phase * exp(-stepL * extinction * sigmaE * a);
                a *= 0.3; b *= 0.5; c *= 0.5;
            }
            return luminance;
        }

        float3 lightRay(float3 p, float mu, float3 sunDir, int stepsL)
        {
            float lrDist = CLOUD_EXTENT * 0.25;
            float dummy  = 0.0;
            getCloudIntersection(p, sunDir, dummy, lrDist);

            float stepL  = lrDist / float(stepsL);
            float lrDens = 0.0;

            [loop] for (int j = 0; j < stepsL; j++)
                lrDens += clouds(p + sunDir * float(j) * stepL);

            float3 beer = multipleOctaves(lrDens, mu, stepL);
            return lerp(
                beer * 2.0 * (1.0 - exp(-stepL * lrDens * 2.0 * sigmaE)),
                beer,
                0.5 + 0.5 * mu);
        }

        float3 mainRay(float3 org, float3 dir, float3 sunDir,
            out float3 totalTransmittance, float offset,
            int stepsP, int stepsL, float maxDist)
        {
            totalTransmittance = 1.0;
            float3 colour = 0.0;

            float distToStart = 0.0, totalDistance = 0.0;
            if (!getCloudIntersection(org, dir, distToStart, totalDistance))
                return colour;

            totalDistance = min(totalDistance, max(0.0, maxDist - distToStart));

            float stepS       = totalDistance / float(stepsP);
            float stepSCoarse = stepS * 4.0;
            distToStart      += stepS * offset;

            float dist    = distToStart;
            float maxD    = distToStart + totalDistance;
            float curStep = stepSCoarse;
            float3 p      = org + dist * dir;

            float mu    = dot(dir, sunDir);
            float phase = lerp(HenyeyGreenstein(-0.3, mu),
                               HenyeyGreenstein( 0.3, mu), 0.7);
            float3 sun  = _MainLightColor.rgb * _Power;

            [loop] for (int i = 0; i < stepsP * 2; i++)
            {
                if (dist > maxD) break;

                float density = clouds(p);

                float3 sp          = p * max(_AxisStretch.xyz, 0.01);
                float  normDist    = length(sp) / CLOUD_EXTENT;
                float3 normDir     = normalize(sp);
                float  fadeFine    = sampleNoise(normDir * _ShapeNoiseScale) - 0.5;
                float  fadeCoarse  = sampleNoise(normDir.yzx * _ShapeNoiseScale * 0.3) - 0.5;
                float  fadeN       = fadeFine + fadeCoarse * _ShapeTendrilStrength;
                float  pertDist    = normDist - fadeN * _FadeNoiseStrength;
                float  shapeFade   = 1.0 - smoothstep(_FadeInnerRadius, _FadeOuterRadius, pertDist);
                density *= shapeFade * shapeFade;

                float3 faceProx = (CLOUD_EXTENT - abs(p)) / CLOUD_EXTENT;
                float  boxFade  = smoothstep(0.0, _FadeBoxMargin,
                    min(faceProx.x, min(faceProx.y, faceProx.z)));
                density *= boxFade;

                if (density > 0.0 && curStep > stepS * 1.1)
                {
                    dist    = max(dist - curStep, distToStart);
                    p       = org + dir * dist;
                    curStep = stepS;
                }
                else if (density > 0.0)
                {
                    float3 sS = sigmaS * density;
                    float3 sE = sigmaE * density;

                    // Density-driven color
                    float3 gasColor = densityColor(density);

                    float3 ambient = 0.0;
                    if (_EnableStars > 0.5)
                    {
                        ambient = 1.0 * getStars(p)
                                + 2.0 * getStars(1.5 * p + 17.51)
                                + 1.0 * getStars(2.4 * p - 6.2)
                                +       getStars(3.7 * p + 109.9);
                        ambient *= smoothstep(1e-3, 2e-3, density);
                    }

                    // Scattered light (directional + ambient), tinted by gas color
                    float3 Lscatter = (ambient
                                     + sun * phase * lightRay(p, mu, sunDir, stepsL))
                                     * sS * gasColor;

                    // Self-emission: independent of scattering, bypasses sS
                    float3 Lemit = _EmissionStrength * gasColor * density;

                    float3 tr = exp(-sE * stepS);

                    // In-scatter uses standard energy integration; emission adds directly
                    colour += totalTransmittance
                            * ((Lscatter - Lscatter * tr) / max(sE, 1e-6)
                               + Lemit * stepS);
                    totalTransmittance *= tr;

                    if (dot(totalTransmittance, 1.0) <= 0.003)
                    {
                        totalTransmittance = 0.0;
                        return colour;
                    }

                    dist += curStep;
                    p     = org + dir * dist;
                }
                else
                {
                    dist += curStep;
                    p     = org + dir * dist;
                }
            }

            return colour;
        }

        ENDHLSL

        // ═════════════════════════════════════════════════════════════════
        //  Pass 0 — Nebula Raymarch
        // ═════════════════════════════════════════════════════════════════

        Pass
        {
            Name "NebulaRaymarch"
            ZTest Always  ZWrite Off  Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex   FullscreenVert
            #pragma fragment NebulaFrag
            #pragma target   4.5

            half4 NebulaFrag(Varyings i) : SV_Target
            {
                float3 rayOriginWS = _WorldSpaceCameraPos;

                #if UNITY_REVERSED_Z
                    float4 farClip = mul(UNITY_MATRIX_I_VP, float4(i.ndc, 0.0, 1.0));
                #else
                    float4 farClip = mul(UNITY_MATRIX_I_VP, float4(i.ndc, 1.0, 1.0));
                #endif
                float3 farWorld = farClip.xyz / farClip.w;
                float3 rayDirWS = normalize(farWorld - rayOriginWS);

                // Scene depth
                float rawDepth = SampleSceneDepth(i.uv);
                float maxWorldDist = 1e6;

                #if UNITY_REVERSED_Z
                    bool hasGeometry = rawDepth > 0.0;
                #else
                    bool hasGeometry = rawDepth < 1.0;
                #endif

                if (hasGeometry)
                {
                    float3 sceneWorldPos = ComputeWorldSpacePosition(i.uv, rawDepth,
                                                                     UNITY_MATRIX_I_VP);
                    maxWorldDist = length(sceneWorldPos - rayOriginWS);
                }

                // Transform to noise space
                float3 orgLS = mul(_NebulaWorldToLocal, float4(rayOriginWS, 1.0)).xyz;
                float3 dirLS = normalize(mul((float3x3)_NebulaWorldToLocal, rayDirWS));
                float3 orgNS = orgLS * LOCAL_TO_NOISE;
                float3 dirNS = normalize(dirLS);

                // World-to-noise distance scale
                float3 worldStep  = mul((float3x3)_NebulaLocalToWorld, dirNS / LOCAL_TO_NOISE);
                float  worldPerNS = length(worldStep);
                float  maxNoiseDist = maxWorldDist / max(worldPerNS, 1e-8);

                // Sun direction
                float3 sunDir = normalize(_MainLightPosition.xyz);

                // Blue-noise dither
                float bn     = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, i.uv).r;
                float offset = frac(bn + frac(_Time.y * _DitherSpeed * goldenRatio));

                // March
                float3 transmittance;
                float3 col = mainRay(orgNS, dirNS, sunDir,
                                     transmittance, offset,
                                     _StepsPrimary, _StepsLight,
                                     maxNoiseDist);

                col *= _NebulaColor.rgb;

                return half4(max(col, 0.0), 1.0);
            }

            ENDHLSL
        }

        // ═════════════════════════════════════════════════════════════════
        //  Pass 1 — Temporal Blend
        // ═════════════════════════════════════════════════════════════════

        Pass
        {
            Name "TemporalBlend"
            ZTest Always  ZWrite Off  Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex   FullscreenVert
            #pragma fragment TemporalFrag
            #pragma target   3.5

            half4 TemporalFrag(Varyings i) : SV_Target
            {
                float3 current = SAMPLE_TEXTURE2D(_NebulaTexture,
                    sampler_NebulaTexture, i.uv).rgb;

                // Reproject: treat nebula as infinitely far (rotation-only)
                #if UNITY_REVERSED_Z
                    float4 farClip = mul(UNITY_MATRIX_I_VP, float4(i.ndc, 0.0, 1.0));
                #else
                    float4 farClip = mul(UNITY_MATRIX_I_VP, float4(i.ndc, 1.0, 1.0));
                #endif
                float3 farWorld = farClip.xyz / farClip.w;

                float4 prevClip = mul(_NebulaPrevVP, float4(farWorld, 1.0));
                float2 prevUV   = (prevClip.xy / prevClip.w) * 0.5 + 0.5;

                // Platform-independent: GL.GetGPUProjectionMatrix(proj, true)
                // already handles Y convention, so no manual UV flip needed.

                float3 history = 0.0;
                bool valid = (prevClip.w > 0.0)
                           && all(prevUV > 0.0) && all(prevUV < 1.0);

                if (valid)
                {
                    history = SAMPLE_TEXTURE2D(_HistoryTexture,
                        sampler_HistoryTexture, prevUV).rgb;

                    // Neighbourhood clamp (3×3 min/max of current frame)
                    float2 texelSize = 1.0 / _ScreenParams.xy;
                    float3 cMin = current, cMax = current;

                    [unroll] for (int ox = -1; ox <= 1; ox++)
                    [unroll] for (int oy = -1; oy <= 1; oy++)
                    {
                        float3 s = SAMPLE_TEXTURE2D_LOD(_NebulaTexture,
                            sampler_NebulaTexture,
                            i.uv + float2(ox, oy) * texelSize, 0).rgb;
                        cMin = min(cMin, s);
                        cMax = max(cMax, s);
                    }

                    history = clamp(history, cMin, cMax);
                }

                float blend = valid ? _TemporalBlendFactor : 1.0;
                float3 result = lerp(history, current, blend);

                return half4(result, 1.0);
            }

            ENDHLSL
        }

        // ═════════════════════════════════════════════════════════════════
        //  Pass 2 — Composite (additive upscale onto camera colour)
        // ═════════════════════════════════════════════════════════════════

        Pass
        {
            Name "Composite"
            ZTest Always  ZWrite Off  Cull Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex   FullscreenVert
            #pragma fragment CompositeFrag
            #pragma target   3.5

            half4 CompositeFrag(Varyings i) : SV_Target
            {
                float3 col = SAMPLE_TEXTURE2D(_NebulaTexture,
                    sampler_NebulaTexture, i.uv).rgb;
                return half4(max(col, 0.0), 1.0);
            }

            ENDHLSL
        }
    }
}