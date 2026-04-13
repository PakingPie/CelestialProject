// Aurora effect for spherical atmosphere
// Adapted from "Auroras" by nimitz 2017 (twitter: @stormoid)
// License Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License

#ifndef AURORAS_INCLUDED
#define AURORAS_INCLUDED

// ============================================
// NOISE HELPERS
// ============================================

float2x2 _AuroraMM2(float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2x2(c, s, -s, c);
}

static const float2x2 _AuroraM2 = float2x2(0.95534, 0.29552, -0.29552, 0.95534);

float _AuroraTri(float x)
{
    return clamp(abs(frac(x) - 0.5), 0.01, 0.49);
}

float2 _AuroraTri2(float2 p)
{
    return float2(
        _AuroraTri(p.x) + _AuroraTri(p.y),
        _AuroraTri(p.y + _AuroraTri(p.x)));
}

float AuroraTriNoise2D(float2 p, float spd, float time)
{
    float z  = 1.8;
    float z2 = 2.5;
    float rz = 0.0;

    p = mul(_AuroraMM2(p.x * 0.06), p);
    float2 bp = p;

    for (int i = 0; i < 5; i++)
    {
        float2 dg = _AuroraTri2(bp * 1.85) * 0.75;
        dg = mul(_AuroraMM2(time * spd), dg);
        p -= dg / z2;

        bp *= 1.3;
        z2 *= 0.45;
        z  *= 0.42;
        p  *= 1.21 + (rz - 1.0) * 0.02;

        rz += _AuroraTri(p.x + _AuroraTri(p.y)) * z;
        p = mul(-_AuroraM2, p);
    }
    return clamp(1.0 / pow(rz * 29.0, 1.3), 0.0, 0.55);
}

float AuroraHash21(float2 n)
{
    return frac(sin(dot(n, float2(12.9898, 4.1414))) * 43758.5453);
}

// ============================================
// SPHERICAL AURORA RAY MARCH
// ============================================

// Computes aurora color + alpha for a ray traversing the atmosphere shell.
//
// rayOrigin      – world-space camera position
// rayDir         – normalized world-space view direction
// planetCenter   – world-space planet center
// planetRadius   – planet surface radius (km)
// poleAxis       – normalized axis pointing toward the magnetic north pole
// auroraAltitude – base altitude of aurora band above surface (km)
// auroraHeight   – vertical extent of aurora volume (km)
// auroraLat      – latitude of aurora band center (radians from pole, e.g. 0.35 ≈ 20°)
// auroraLatWidth – half-width of latitude band (radians)
// auroraSteps    – number of ray-march steps
// auroraSpeed    – animation speed multiplier
// auroraScale    – texture scale
// screenUV       – screen-space UV for dithering
// time           – _Time.y
//
float4 EvaluateAurora(
    float3 rayOrigin,
    float3 rayDir,
    float3 planetCenter,
    float  planetRadius,
    float3 poleAxis,
    float  auroraAltitude,
    float  auroraHeight,
    float  auroraLat,
    float  auroraLatWidth,
    uint   auroraSteps,
    float  auroraSpeed,
    float  auroraScale,
    float2 screenUV,
    float  time)
{
    // Build a stable coordinate frame around the pole axis
    float3 refVec  = abs(dot(poleAxis, float3(0, 1, 0))) > 0.99
                     ? float3(1, 0, 0) : float3(0, 1, 0);
    float3 perpAxis = normalize(cross(poleAxis, refVec));
    float3 biAxis   = cross(poleAxis, perpAxis);

    // Check if the ray can even reach the aurora region
    float rMax = planetRadius + auroraAltitude + auroraHeight * 3.0;
    float2 hitCheck = RaySphereIntersection(rayOrigin, rayDir, planetCenter, rMax);
    if (hitCheck.x > hitCheck.y)
        return 0;

    // Check planet occlusion
    float2 hitPlanet = RaySphereIntersection(rayOrigin, rayDir, planetCenter, planetRadius);
    bool planetBlocks = (hitPlanet.x < hitPlanet.y && hitPlanet.x > 0.0);

    float4 col    = 0;
    float4 avgCol = 0;
    float fn = (float)auroraSteps;

    // Iterate over altitude layers, just like the original Shadertoy
    // iterates over horizontal planes: pt = (height - ro.y) / (rd.y * 2 + 0.4)
    // Each step samples a spherical shell at increasing radius.
    for (uint i = 0; i < auroraSteps; i++)
    {
        float fi = (float)i;

        // Altitude for this layer — polynomial increase like original
        // Original: height = 0.8 + pow(i, 1.4) * 0.002
        // We map this to: auroraAltitude + pow(i, 1.4) * auroraHeight / pow(N, 1.4)
        float layerAlt = auroraAltitude + pow(fi, 1.4) * auroraHeight / pow(fn, 1.4);
        float shellRadius = planetRadius + layerAlt;

        // Intersect ray with this shell
        float2 hit = RaySphereIntersection(rayOrigin, rayDir, planetCenter, shellRadius);

        // No intersection with this shell — skip
        if (hit.x > hit.y)
            continue;

        // Pick the nearest positive intersection
        float t = (hit.x > 0.0) ? hit.x : hit.y;
        if (t < 0.0)
            continue;

        // If planet blocks this intersection, skip
        if (planetBlocks && hitPlanet.x < t)
            continue;

        // Dither to reduce banding
        float dither = 0.006 * AuroraHash21(screenUV * 1000.0 + fi) * smoothstep(0.0, 15.0, fi);
        t -= dither;

        float3 samplePos = rayOrigin + rayDir * t;
        float3 relPos    = samplePos - planetCenter;
        float  r         = length(relPos);
        float3 normal    = relPos / r;

        // Latitude: angle from the pole axis (0 = pole, PI/2 = equator)
        float cosLat   = dot(normal, poleAxis);
        float latitude = acos(clamp(abs(cosLat), 0.0, 1.0)); // abs → both poles

        // Latitude mask — band around the target latitude
        float latDist = abs(latitude - auroraLat);
        float latMask = smoothstep(auroraLatWidth, auroraLatWidth * 0.15, latDist);

        if (latMask < 0.001)
            continue;

        // Longitude around pole axis
        float lon = atan2(dot(normal, biAxis), dot(normal, perpAxis));

        // Noise coordinates: angular position only (no altitude dependence).
        // All shells at the same direction get the same noise → vertical curtains.
        float sinLat = sin(latitude);
        float2 noiseUV = float2(sinLat * cos(lon), sinLat * sin(lon)) * auroraScale;

        float rzt  = AuroraTriNoise2D(noiseUV, 0.06 * auroraSpeed, time);
        float4 col2 = float4(0, 0, 0, rzt);

        // Color: green-dominated palette shifting with step (like the original)
        col2.rgb = (sin(1.0 - float3(2.15, -0.5, 1.2) + fi * 0.043) * 0.5 + 0.5) * rzt;

        // Running average + exponential falloff + smooth fade-in + latitude mask
        // This is the exact accumulation from the original Shadertoy.
        // exp2 falloff naturally fades higher layers — no hard cutoff needed.
        avgCol = lerp(avgCol, col2, 0.5);
        col += avgCol * exp2(-fi * 0.065 - 2.5) * smoothstep(0.0, 5.0, fi) * latMask;
    }

    col *= 1.8;
    col = max(col, 0);

    return col;
}

#endif // AURORAS_INCLUDED
