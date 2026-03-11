#ifndef CUSTOM_VOLUMETRIC_CLOUDS_UTILITIES_HLSL
#define CUSTOM_VOLUMETRIC_CLOUDS_UTILITIES_HLSL

// ============================================================
// [FIX 1] Explicit remap — correct parameter order
// Original code passed args matching (value, min, max, toMin, toMax)
// but Unity's Remap signature is (origFrom, origTo, targetFrom, targetTo, value),
// producing division-by-zero at specific heights → sharp layer boundaries.
// ============================================================
float RemapValue(float value, float fromMin, float fromMax, float toMin, float toMax)
{
    float t = (value - fromMin) / max(fromMax - fromMin, 0.0001);
    return toMin + t * (toMax - toMin);
}

// ============================================================
// Phase functions (unchanged)
// ============================================================
float HenyeyGreenstein(float cosTheta, float g)
{
    float g2 = g * g;
    float denom = 1.0 + g2 - 2.0 * g * cosTheta;
    return (1.0 - g2) / (4.0 * PI * pow(max(denom, 0.0001), 1.5));
}

float DualLobePhase(float cosTheta)
{
    float forward = HenyeyGreenstein(cosTheta, _ScatteringForward);
    float back = HenyeyGreenstein(cosTheta, -_ScatteringBack);
    float phase = lerp(back, forward, _ScatteringBlend);
    float multiScatter = 0.25 / PI;
    phase = lerp(phase, multiScatter, _MultiScatter * 0.5);
    return max(phase, 0.03);
}

// ============================================================
// Ray-sphere intersection (unchanged)
// ============================================================
float2 RaySphereIntersect(float3 rayOrigin, float3 rayDir, float3 sphereCenter, float sphereRadius)
{
    float3 oc = rayOrigin - sphereCenter;
    float b = dot(oc, rayDir);
    float c = dot(oc, oc) - sphereRadius * sphereRadius;
    float discriminant = b * b - c;

    if (discriminant < 0.0)
        return float2(-1.0, -1.0);

    float sqrtDisc = sqrt(discriminant);
    return float2(-b - sqrtDisc, -b + sqrtDisc);
}

// ============================================================
// [FIX 1] Height gradient with corrected remap
// Before: Remap args were swapped, creating hard cutoffs
//         at heightFraction ≈ 0.2 and 0.5 (the visible layers).
// After:  Smooth cumulus/stratus profile across full shell.
// ============================================================
float GetHeightGradient(float heightFraction, float cloudType)
{
    // Cumulus: base ramp 0→0.1, plateau 0.1→0.5, soft top falloff 0.5→1.0
    float cumulus = saturate(RemapValue(heightFraction, 0.0, 0.1, 0.0, 1.0)) * saturate(RemapValue(heightFraction, 0.2, 0.5, 1.0, 0.9)) * saturate(RemapValue(heightFraction, 0.5, 1.0, 0.9, 0.0));

    // Stratus: wider, flatter distribution
    float stratus = saturate(RemapValue(heightFraction, 0.0, 0.1, 0.0, 1.0)) * saturate(RemapValue(heightFraction, 0.3, 0.95, 1.0, 0.0));

    return lerp(stratus, cumulus, cloudType);
}

// ============================================================
// [FIX 4] Spherical UV with radial amplification
// Before: posOS * scale gave only 0.04 * scale radial variation
//         (the shell is 0.04 units thick) — noise was identical
//         at all heights, producing flat sheets.
// After:  Radial dimension is amplified so noise varies
//         meaningfully through the cloud thickness.
// ============================================================
float3 GetCloudUV(float3 posOS, float scale, float heightFraction)
{
    float3 dir = normalize(posOS);

    // Amplify radial variation:
    //   Without: ~0.04 * scale change through shell (negligible)
    //   With:    ~0.5  * scale change through shell (volumetric)
    float amplifiedRadius = scale * (1.0 + heightFraction * 0.5);
    float3 uvw = dir * amplifiedRadius;

    return uvw * _NoiseTiling.xyz + _NoiseOffset.xyz;
}

// ============================================================
// Cloud density sampling
// Changes: uses GetCloudUV with heightFraction,
//          consistent density between cheap/full paths,
//          weather LOD reduced from 3 → 2 for more detail.
// ============================================================
float SampleCloudDensity(float3 positionOS, bool cheap, float blueNoise)
{
    float radius = length(positionOS);

    if (radius < _InnerRadius || radius > _OuterRadius)
        return 0.0;

    float shellThickness = _OuterRadius - _InnerRadius;
    float heightFraction = saturate((radius - _InnerRadius) / max(shellThickness, 0.0001));

    // Wind animation
    float3 windDir = normalize(_WindDirection.xyz + float3(0.0001, 0, 0));
    float time = _Time.y * _WindSpeed;
    float3 windOffset = windDir * time;

    // Weather / coverage map (LOD 2 instead of 3 for more spatial detail)
    float3 weatherUV = GetCloudUV(positionOS, _PatchScale * 0.5, heightFraction) + windOffset * 0.2;
    float4 weatherNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, weatherUV, 2);
    float weatherValue = weatherNoise.r * 0.6 + weatherNoise.g * 0.25 + weatherNoise.a * 0.15;

    float weatherMask = smoothstep(0.35, 0.65, weatherValue);
    weatherMask = lerp(1.0, weatherMask, _Patchiness);

    if (weatherMask < 0.05)
        return 0.0;

    // Cloud type variation → height gradient
    float cloudType = saturate(weatherNoise.b * 0.7 + 0.3);
    float heightGradient = GetHeightGradient(heightFraction, lerp(0.3, 0.8, cloudType));

    // Base shape noise
    float3 baseUV = GetCloudUV(positionOS, _CloudScale, heightFraction) + windOffset;
    float4 baseNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, baseUV, 0);
    float baseShape = baseNoise.r;

    // Low frequency variation
    float3 lowFreqUV = GetCloudUV(positionOS, _CloudScale * 0.4, heightFraction) + windOffset * 0.5;
    float4 lowFreqNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, lowFreqUV, 1);
    float lowFreq = lowFreqNoise.r * 0.5 + lowFreqNoise.g * 0.3 + lowFreqNoise.b * 0.2;

    baseShape = lerp(baseShape, baseShape * lowFreq * 2.0, _Billowness * 0.5);
    baseShape = saturate(baseShape);

    // Apply height gradient and weather
    float shapedDensity = baseShape * heightGradient * weatherMask;

    // Coverage threshold
    float coverageMin = 1.0 - _CloudCoverage;
    float coverageMax = coverageMin + 0.2;
    float baseDensity = smoothstep(coverageMin, coverageMax, shapedDensity);

    // Height-based density falloff — applied before the cheap branch
    // so both paths return consistent values
    float densityFalloff = lerp(1.0, 0.3, pow(heightFraction, 1.5));
    baseDensity *= densityFalloff;

    if (cheap)
        return baseDensity * _CloudDensity * _CloudLayerDensity;

    // Detail erosion
    if (baseDensity > 0.01)
    {
        float3 detailUV = GetCloudUV(positionOS, _CloudScale * _DetailScale, heightFraction) + windOffset * _DetailWindMultiplier;
        float4 detailNoise = SAMPLE_TEXTURE3D_LOD(_NoiseTexture, sampler_NoiseTexture, detailUV, 0);

        float detailFBM = detailNoise.g * 0.5 + detailNoise.b * 0.35 + detailNoise.a * 0.15;

        float edgeFactor = 1.0 - pow(baseDensity, 0.5);
        float heightErosion = lerp(0.3, 1.0, heightFraction);
        float erosion = _ErosionStrength * detailFBM * edgeFactor * heightErosion;

        baseDensity = saturate(baseDensity - erosion);

        float detailMod = lerp(1.0, 0.75 + 0.5 * detailFBM, _DetailStrength);
        baseDensity *= detailMod;
    }

    return baseDensity * _CloudDensity * _CloudLayerDensity;
}

// ============================================================
// Beer-Powder lighting model (unchanged)
// ============================================================
float BeerPowder(float density, float cosTheta, float heightFraction)
{
    float beer = exp(-density * _CloudAbsorption);
    float powder = 1.0 - exp(-density * _CloudAbsorption * 2.0);
    powder = lerp(powder, 1.0, saturate(heightFraction * 0.8));
    float powderBlend = _PowderStrength * saturate(-cosTheta * 0.5 + 0.5);
    return beer * lerp(1.0, powder, powderBlend);
}

// ============================================================
// Fire emission — noise-driven incandescent glow
// Returns HDR emission color at a cloud sample point.
// ============================================================
float3 SampleFireEmission(float3 positionOS, float heightFraction, float cloudDensity)
{
    float3 windDir = normalize(_WindDirection.xyz + float3(0.0001, 0, 0));
    float time = _Time.y * _FireAnimSpeed;
    float3 normal = normalize(positionOS);

    // ---- Ridged noise → vein / crack-like fire structure ----
    float3 fireBaseUV = GetCloudUV(positionOS, _FireScale, heightFraction);
    fireBaseUV += windDir * time * 0.3;
    float4 fireBase = SAMPLE_TEXTURE3D_LOD(
        _NoiseTexture, sampler_NoiseTexture, fireBaseUV, 1);

    // Ridged: fold noise around 0.5, square to sharpen peaks into thin veins
    float ridge1 = 1.0 - abs(fireBase.r * 2.0 - 1.0);
    ridge1 *= ridge1;
    float ridge2 = 1.0 - abs(fireBase.g * 2.0 - 1.0);
    ridge2 *= ridge2;
    float firePattern = max(ridge1, ridge2 * 0.6);

    // ---- Detail breakup — erode the pattern so it doesn't fill ----
    float3 fireDetailUV = GetCloudUV(positionOS, _FireScale * _FireDetailScale, heightFraction);
    fireDetailUV += windDir * time * 0.9 + normal * time * 0.15;
    float4 fireDetail = SAMPLE_TEXTURE3D_LOD(
        _NoiseTexture, sampler_NoiseTexture, fireDetailUV, 0);

    float erosion = fireDetail.r * 0.4 + fireDetail.g * 0.3;
    firePattern = saturate(firePattern - erosion * (1.0 - _FireCoverage));

    // ---- Sharpen into isolated hotspots (power curve) ----
    firePattern = pow(firePattern, lerp(4.0, 1.5, _FireCoverage));

    // ---- Height mask — fire stronger at cloud base ----
    float heightMask = pow(saturate(1.0 - heightFraction), _FireHeightFalloff);

    // ---- Tight threshold — only strong peaks survive ----
    float threshold = lerp(0.5, 0.1, _FireCoverage);
    float fireMask = smoothstep(threshold, threshold + 0.1, firePattern);
    fireMask *= heightMask;

    // ---- Cloud presence gate — only where cloud exists, favor edges ----
    float densityPresence = saturate(cloudDensity * 2.0);
    float edgeBoost = 1.0 - saturate(cloudDensity * 0.3);
    fireMask *= densityPresence * (0.4 + 0.6 * edgeBoost);

    if (fireMask < 0.001)
        return float3(0, 0, 0);

    // ---- Temperature color — only bright at peak values ----
    float temperature = pow(saturate(fireMask), 0.7);
    float3 fireColor = lerp(_FireColorDark.rgb, _FireColorBright.rgb, temperature);

    return fireColor * fireMask * _FireIntensity;
}

// ============================================================
// [FIX 8] Light energy — proper ray-sphere step size,
//         robust tangent basis for cone sampling.
// Before: step size = shellThickness * 0.5 / LightSteps
//         → total march distance was half the shell
//         → accumulated density tiny → no self-shadowing.
//         Cone basis degenerate when light ≈ +Y.
// After:  Ray-sphere intersection finds actual path through shell.
//         Tangent basis uses safe reference vector.
// ============================================================
float3 SampleLightEnergy(float3 positionOS, float3 lightDirOS, float heightFraction, float cosTheta)
{
    // ============================================================
    // Planet body occlusion — soft terminator
    //
    // Find closest approach of the light ray to the planet center.
    // If the ray passes through (or close to) the inner sphere,
    // the planet blocks direct sunlight.
    // ============================================================
    float tClosest = -dot(positionOS, lightDirOS);
    float planetShadow = 1.0;

    if (tClosest > 0.0) // planet center is ahead along light ray (dark side)
    {
        float3 closestPoint = positionOS + lightDirOS * tClosest;
        float closestDist = length(closestPoint);
        float shellThickness = _OuterRadius - _InnerRadius;

        // Smooth transition across terminator
        planetShadow = smoothstep(
            _InnerRadius - shellThickness * 0.1,
            _InnerRadius + shellThickness * 0.5,
            closestDist);
    }

    // Early out: planet fully blocks sunlight
    if (planetShadow < 0.01)
    {
        return float3(0.0, 0.0, 0.0);
    }

    // ============================================================
    // Light march (existing code, unchanged)
    // ============================================================
    float2 outerHitL = RaySphereIntersect(positionOS, lightDirOS, float3(0, 0, 0), _OuterRadius);
    float2 innerHitL = RaySphereIntersect(positionOS, lightDirOS, float3(0, 0, 0), _InnerRadius);

    float lightEnd = max(outerHitL.y, 0.001);
    if (innerHitL.x > 0.0)
        lightEnd = min(lightEnd, innerHitL.x);

    float stepSize = lightEnd / float(_LightSteps);

    float3 refDir = abs(dot(lightDirOS, float3(0, 1, 0))) > 0.99
                        ? float3(1, 0, 0)
                        : float3(0, 1, 0);
    float3 tangent1 = normalize(cross(lightDirOS, refDir));
    float3 tangent2 = cross(lightDirOS, tangent1);

    float totalDensity = 0.0;

    for (int i = 0; i < _LightSteps; i++)
    {
        float t = (float(i) + 0.5) * stepSize;

        float coneRadius = t * 0.05 * (1.0 + float(i) * 0.1);
        float angle = float(i) * 2.39996;
        float3 coneOffset = (tangent1 * cos(angle) + tangent2 * sin(angle)) * coneRadius;

        float3 samplePos = positionOS + lightDirOS * t + coneOffset;
        float density = SampleCloudDensity(samplePos, true, 0.5);

        float weight = exp(-float(i) * 0.15);
        totalDensity += density * stepSize * weight;
    }

    float lightEnergy = BeerPowder(totalDensity, cosTheta, heightFraction);

    float multiScatterEnergy = exp(-totalDensity * _CloudAbsorption * 0.25);
    float3 multiScatter = lerp(float3(0.5, 0.6, 0.7), float3(1, 1, 1), multiScatterEnergy) * _MultiScatter;

    // Apply planet shadow to the final result
    return (lightEnergy + multiScatter * 0.15) * planetShadow;
}

#endif