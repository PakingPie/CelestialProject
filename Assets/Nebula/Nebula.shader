// Volumetric nebula — ported from BufferB.glsl (al-ro, shadertoy.com)
// Apply on a Cube mesh (default Unity cube = local space ±0.5).
// The shader internally works in "noise space" (±10) by scaling local coords ×20.
// Assign a 1024×1024 blue noise texture to _BlueNoise for best results.
Shader "Custom/Nebula"
{
    Properties
    {
        [Header(Lighting)]
        _Power       ("Light Power",   Float)  = 200.0
        [HDR] _NebulaColor ("Nebula Color Tint", Color) = (1, 1, 1, 1)

        [Header(Shape Fade breaks cube silhouette)]
        _FadeInnerRadius   ("Fade Inner Radius",    Range(0, 1)) = 0.55
        _FadeOuterRadius   ("Fade Outer Radius",    Range(0, 1)) = 0.95
        _FadeNoiseStrength ("Fade Noise Strength",  Range(0, 1)) = 0.35
        // Stretch the nebula shape along each world axis.
        // (1,1,1) = sphere. Uneven values = ellipsoid / irregular blob.
        _AxisStretch       ("Axis Stretch (X Y Z)", Vector)      = (1.0, 0.6, 1.4, 0)

        [Header(Quality lower steps for better performance)]
        _StepsPrimary ("Primary Ray Steps", Int) = 32
        _StepsLight   ("Light Ray Steps",   Int) = 8

        [Header(Stars)]
        [Toggle(_STARS_ON)] _StarsOn       ("Enable Stars",     Float)        = 1
        _StarBrightness                    ("Star Brightness",  Range(0, 0.5)) = 0.05

        [Header(Dithering assign 1024x1024 blue noise)]
        _BlueNoise   ("Blue Noise Texture",  2D)          = "white" {}
        _DitherSpeed ("Dither Animation Speed", Range(0, 1)) = 0.1
    }

    SubShader
    {
        // Render after opaque, no depth write, additive blend so nebula glows over scene
        Tags { "Queue" = "Transparent+1" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Cull Front
        ZWrite On
        ZTest LEqual
        Blend One One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma shader_feature_local _STARS_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ─────────────────────────────── Constants ────────────────────────────────

            #define PI     3.14159265359
            #define TWO_PI (2.0 * PI)

            // Noise / lighting functions work in "noise space" ±CLOUD_EXTENT (matches BufferB).
            // Unity default cube local space is ±0.5, so we scale ×20 to reach ±10.
            #define CLOUD_EXTENT   10.0
            #define LOCAL_TO_NOISE 20.0

            static const float3 minCorner = float3(-CLOUD_EXTENT, -CLOUD_EXTENT, -CLOUD_EXTENT);
            static const float3 maxCorner = float3( CLOUD_EXTENT,  CLOUD_EXTENT,  CLOUD_EXTENT);

            // Scattering coefficients (from BufferB, Earth atmosphere tweaked for nebula look)
            static const float3 BETA_RAYLEIGH = 100.0 * float3(0.05802, 0.14558, 0.331);
            static const float3 BETA_OZONE    = float3(0.650, 1.881, 0.085);

            static const float3 sigmaS = 2.0 * BETA_RAYLEIGH;
            static const float3 sigmaE = 4.0 * (BETA_RAYLEIGH + 3.0 * BETA_OZONE); // extinction = absorption

            static const float goldenRatio = 1.61803398875;

            // ──────────────────────────── Material Properties ─────────────────────────

            CBUFFER_START(UnityPerMaterial)
                float4 _BlueNoise_ST;
                float4 _AxisStretch;
                float4 _NebulaColor;
                float  _Power;
                float  _DitherSpeed;
                float  _FadeInnerRadius;
                float  _FadeOuterRadius;
                float  _FadeNoiseStrength;
                float  _StarBrightness;
                int    _StepsPrimary;
                int    _StepsLight;
            CBUFFER_END

            TEXTURE2D(_BlueNoise);
            SAMPLER(sampler_BlueNoise);

            // ──────────────────────────────── Structs ─────────────────────────────────

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 worldPos    : TEXCOORD0;
            };

            // ──────────────────────────── AABB Intersection ───────────────────────────

            // https://gist.github.com/DomNomNom/46bb1ce47f68d255fd5d
            float2 intersectAABB(float3 rayOrigin, float3 rayDir, float3 boxMin, float3 boxMax)
            {
                float3 tMin = (boxMin - rayOrigin) / rayDir;
                float3 tMax = (boxMax - rayOrigin) / rayDir;
                float3 t1 = min(tMin, tMax);
                float3 t2 = max(tMin, tMax);
                float tNear = max(max(t1.x, t1.y), t1.z);
                float tFar  = min(min(t2.x, t2.y), t2.z);
                return float2(tNear, tFar);
            }

            bool insideAABB(float3 p)
            {
                const float eps = 1e-4;
                return (p.x > minCorner.x - eps) && (p.y > minCorner.y - eps) && (p.z > minCorner.z - eps) &&
                       (p.x < maxCorner.x + eps) && (p.y < maxCorner.y + eps) && (p.z < maxCorner.z + eps);
            }

            // Returns true if the ray hits the cloud volume.
            // Sets distToStart to the entry point and totalDistance to the march length.
            bool getCloudIntersection(float3 org, float3 dir,
                                      out float distToStart, out float totalDistance)
            {
                float2 t = intersectAABB(org, dir, minCorner, maxCorner);
                if (insideAABB(org))
                    t.x = 1e-4; // camera is inside the box: start marching immediately
                distToStart   = t.x;
                totalDistance = t.y - t.x;
                return (t.x > 0.0) && (t.x < t.y);
            }

            // ──────────────────────────────── Utility ─────────────────────────────────

            // https://www.shadertoy.com/view/3s3GDn
            float getGlow(float dist, float radius, float intensity)
            {
                return max(0.0, pow(radius / max(dist, 1e-5), intensity));
            }

            // ────────────────────────────── Hash / Noise ──────────────────────────────

            // https://www.shadertoy.com/view/4djSRW
            float3 hash(float3 p3)
            {
                p3 = frac(p3 * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yxz + 33.33);
                return 2.0 * frac((p3.xxy + p3.yxx) * p3.zyx) - 1.0;
            }

            // https://en.wikipedia.org/wiki/Gyroid
            float gyroid(float3 p, float thickness, float bias, float frequency)
            {
                return clamp(
                    abs(dot(sin(p * 0.5), cos(p.zxy * 1.23) * frequency) - bias) - thickness,
                    0.0, 3.0) / 3.0;
            }

            // Gyroid-based FBM noise. 12 octaves.
            // https://www.shadertoy.com/view/3l23Rh
            float fbm(float3 p)
            {
                const int   octaves  = 6;
                const float fbmScale = 1.95;

                // Rotation by PI/12 around Z, scaled — prevents octave alignment.
                // HLSL is row-major: this is the row-major form of the GLSL column-major mat3.
                const float rot_c = 0.96592582628; // cos(PI/12)
                const float rot_s = 0.25881904510; // sin(PI/12)
                const float3x3 rotMat = fbmScale * float3x3(
                     rot_c, -rot_s, 0.0,
                     rot_s,  rot_c, 0.0,
                     0.0,    0.0,   1.0
                );

                float weight    = 0.0;
                float amplitude = 1.0;
                float frequency = 1.0;
                float res       = 0.0;

                [loop]
                for (int i = 0; i < octaves; i++)
                {
                    res += amplitude * gyroid(p, 0.1, 0.0, frequency);
                    p    = mul(rotMat, p);
                    weight    += amplitude;
                    amplitude *= (i < 4) ? 0.9 : 0.7;
                    frequency *= (i < 3) ? 0.65 : 0.78;
                }

                return saturate(res / weight);
            }

            // Cloud density at point p (noise space ±10).
            float clouds(float3 p)
            {
                if (!insideAABB(p))
                    return 0.0;
                float noise     = fbm(0.25 * p);
                float structure = smoothstep(3.0, 5.0,  length(p)) * smoothstep(0.05, 0.1, noise);
                float haze      = smoothstep(2.0, 10.0, length(p)) * smoothstep(0.02, 0.5, noise);
                return 3e-4 + (0.5 * haze + 0.75 * structure);
            }

            // ──────────────────────────────── Stars ───────────────────────────────────

            // https://iquilezles.org/articles/palettes/
            float3 getColour(float t)
            {
                float3 a = float3(0.65, 0.65, 0.65);
                float3 b = 1.0 - a; // float3(0.35, 0.35, 0.35)
                float3 c = float3(1.0, 1.0, 1.0);
                float3 d = float3(0.15, 0.5, 0.75);
                return pow(a + b * cos(TWO_PI * (c * t + d)), float3(2.2, 2.2, 2.2));
            }

            // Procedural stars scattered in the nebula volume.
            float3 getStars(float3 p)
            {
                p *= 0.2;
                float3 bestRand = 0;
                float  bestDist = 1e10;
                float3 bestCell = 0;

                [loop]
                for (int x = -1; x <= 1; x++)
                {
                    [loop]
                    for (int y = -1; y <= 1; y++)
                    {
                        [loop]
                        for (int z = -1; z <= 1; z++)
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
                    }
                }

                bestRand = clamp(0.5 + 0.5 * bestRand, 0.0, 1.0);
                float3 rand2 = clamp(0.5 + 0.5 * hash(bestCell + float3(3.12, 104.9, -9.5)), 0.0, 1.0);

                return float3(_StarBrightness, _StarBrightness, _StarBrightness)
                     * bestRand.z
                     * step(0.45, rand2.z)
                     * lerp(float3(1, 1, 1), getColour(bestRand.y), 0.3)
                     * smoothstep(0.5, 0.0, bestDist)
                     * getGlow(bestDist, 0.25, 2.0);
            }

            // ─────────────────────────────── Lighting ─────────────────────────────────

            // Single-lobe Henyey-Greenstein phase function.
            float HenyeyGreenstein(float g, float costh)
            {
                float denom = abs(1.0 + g * g - 2.0 * g * costh);
                return (1.0 / (4.0 * PI)) * ((1.0 - g * g) / pow(denom, 1.5));
            }

            // Multiple-scattering approximation.
            // https://twitter.com/FewesW/status/1364629939568451587/photo/1
            float3 multipleOctaves(float extinction, float mu, float stepL)
            {
                float3 luminance = 0;
                float a = 1.0, b = 1.0, c = 1.0;

                [loop]
                for (float i = 0.0; i < 6.0; i += 1.0)
                {
                    float phase = lerp(HenyeyGreenstein(-0.1 * c, mu),
                                       HenyeyGreenstein( 0.3 * c, mu), 0.7);
                    luminance += b * phase * exp(-stepL * extinction * sigmaE * a);
                    a *= 0.3;
                    b *= 0.5;
                    c *= 0.5;
                }
                return luminance;
            }

            // Integrate light transmittance from sample point p towards the sun.
            float3 lightRay(float3 p, float mu, float3 sunDirection, int stepsLight)
            {
                // Find how far the light ray travels inside the volume from p.
                float lightRayDistance = CLOUD_EXTENT * 0.25;
                float distToStart      = 0.0;
                getCloudIntersection(p, sunDirection, distToStart, lightRayDistance);

                float stepL           = lightRayDistance / float(stepsLight);
                float lightRayDensity = 0.0;

                [loop]
                for (int j = 0; j < stepsLight; j++)
                    lightRayDensity += clouds(p + sunDirection * float(j) * stepL);

                float3 beersLaw = multipleOctaves(lightRayDensity, mu, stepL);

                // Mix Beer's law and powder effect based on view-sun angle.
                return lerp(
                    beersLaw * 2.0 * (1.0 - exp(-stepL * lightRayDensity * 2.0 * sigmaE)),
                    beersLaw,
                    0.5 + 0.5 * mu);
            }

            // ──────────────────────────── Main Raymarch ───────────────────────────────

            float3 mainRay(float3 org, float3 dir, float3 sunDirection,
                           out float3 totalTransmittance, float offset,
                           int stepsPrimary, int stepsLight)
            {
                totalTransmittance = float3(1, 1, 1);
                float3 colour      = 0;

                float distToStart   = 0.0;
                float totalDistance = 0.0;
                if (!getCloudIntersection(org, dir, distToStart, totalDistance))
                    return colour;

                float stepS       = totalDistance / float(stepsPrimary);
                float stepSCoarse = stepS * 4.0; // skip empty space 4× faster
                distToStart      += stepS * offset; // blue-noise dither

                float  dist    = distToStart;
                float  maxDist = distToStart + totalDistance;
                float  curStep = stepSCoarse;
                float3 p       = org + dist * dir;

                float mu            = dot(dir, sunDirection);
                float phaseFunction = lerp(HenyeyGreenstein(-0.3, mu),
                                           HenyeyGreenstein( 0.3, mu), 0.7);
                float3 sunLight     = _MainLightColor.rgb * _Power;

                // Budget: stepsPrimary * 2.
                // Back-up steps don't advance dist, so fine steps never exceed stepsPrimary.
                // No 'continue' used — avoids burning the loop counter on back-up iterations.
                [loop]
                for (int i = 0; i < stepsPrimary * 2; i++)
                {
                    if (dist > maxDist) break;

                    float density = clouds(p);

                    // ── Ellipsoidal shape fade ──
                    float3 stretchedP    = p * max(_AxisStretch.xyz, 0.01);
                    float  normalizedDist = length(stretchedP) / CLOUD_EXTENT;
                    float  fadeNoise     = fbm(normalize(stretchedP) * 2.5) - 0.5;
                    float  perturbedDist = normalizedDist - fadeNoise * _FadeNoiseStrength;
                    float  shapeFade     = 1.0 - smoothstep(_FadeInnerRadius, _FadeOuterRadius, perturbedDist);
                    density *= shapeFade;

                    if (density > 0.0 && curStep > stepS * 1.1)
                    {
                        // Hit density on a coarse step: back up to start of this interval,
                        // switch to fine. Dist is NOT advanced — next iteration re-samples
                        // this same point at fine resolution.
                        dist    = max(dist - curStep, distToStart);
                        p       = org + dir * dist;
                        curStep = stepS;
                    }
                    else if (density > 0.0)
                    {
                        // ── Fine step hit: integrate ──
                        float3 sampleSigmaS = sigmaS * density;
                        float3 sampleSigmaE = sigmaE * density;

                        #if defined(_STARS_ON)
                        float3 ambient =
                            1.0 * getStars(p)                +
                            2.0 * getStars(1.5 * p + 17.51)  +
                            1.0 * getStars(2.4 * p - 6.2 )   +
                                  getStars(3.7 * p + 109.9);
                        ambient *= smoothstep(1e-3, 2e-3, density);
                        #else
                        float3 ambient = float3(0, 0, 0);
                        #endif

                        float3 luminance = ambient
                            + sunLight * phaseFunction * lightRay(p, mu, sunDirection, stepsLight);
                        luminance *= sampleSigmaS;

                        float3 transmittance = exp(-sampleSigmaE * stepS);

                        colour += totalTransmittance
                               * (luminance - luminance * transmittance)
                               / max(sampleSigmaE, 1e-6);

                        totalTransmittance *= transmittance;

                        if (length(totalTransmittance) <= 0.001)
                        {
                            totalTransmittance = 0;
                            return colour;
                        }

                        dist += curStep;
                        p     = org + dir * dist;
                    }
                    else
                    {
                        // Empty space: advance.
                        // Only use coarse step if still in coarse phase (curStep wasn't
                        // switched to fine yet). Once fine, stay fine — reverting to coarse
                        // after a back-up causes oscillation between the last empty and first
                        // dense positions, burning the iteration budget with no integration.
                        dist += curStep;
                        p     = org + dir * dist;
                    }
                }

                return colour;
            }

            // ─────────────────────────────── Tonemap ──────────────────────────────────

            // https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
            float3 ACESFilm(float3 x)
            {
                return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
            }

            // ──────────────────────────── Vertex Shader ───────────────────────────────

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS);
                o.worldPos    = TransformObjectToWorld(v.positionOS);
                return o;
            }

            // ─────────────────────────── Fragment Shader ──────────────────────────────

            half4 frag(Varyings i) : SV_Target
            {
                // ── Build world-space ray from Unity camera ──
                float3 rayOriginWS = _WorldSpaceCameraPos;
                float3 rayDirWS    = normalize(i.worldPos - rayOriginWS);

                // ── Transform to object local space (±0.5 for default Unity cube) ──
                float3 rayOriginLS = mul(unity_WorldToObject, float4(rayOriginWS, 1.0)).xyz;
                float3 rayDirLS    = normalize(mul((float3x3)unity_WorldToObject, rayDirWS));

                // ── Scale to noise space (±10, matching original BufferB) ──
                float3 org = rayOriginLS * LOCAL_TO_NOISE;
                float3 dir = normalize(rayDirLS); // direction is unaffected by uniform scale

                // ── Sun direction from URP main directional light ──
                float3 sunDirection = normalize(_MainLightPosition.xyz);

                // ── Blue-noise dithering: time-varying offset suppresses banding ──
                float2 screenUV = i.positionHCS.xy / _ScreenParams.xy;
                float blueNoise = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, screenUV).r;
                float offset    = frac(blueNoise + frac(_Time.y * _DitherSpeed * goldenRatio));

                // ── Volumetric raymarch ──
                // Background is intentionally omitted: additive blend means black = no
                // contribution, so the scene skybox shows through in empty regions.
                float3 totalTransmittance;
                float3 col = mainRay(org, dir, sunDirection,
                                     totalTransmittance, offset,
                                     _StepsPrimary, _StepsLight);

                // ── Nebula color tint (applied before tonemapping so HDR values work) ──
                col *= _NebulaColor.rgb;

                // ── Tonemap + gamma ──
                col = ACESFilm(col);
                col = pow(max(col, 0.0), 0.4545); // gamma 2.2

                return half4(col, 1.0);
            }

            ENDHLSL
        }
    }
}