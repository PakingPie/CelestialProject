#ifndef MY_BLACK_HOLE_UTILITIES_HLSL
#define MY_BLACK_HOLE_UTILITIES_HLSL

// Utility functions for black hole shader effects
// Based upon UnityCG.cginc, used in hdrIntensity
float3 LinearToGammaSpace(float3 linRGB)
{
    linRGB = max(linRGB, float3(0.f, 0.f, 0.f));
    // An almost-perfect approximation from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return max(1.055h * pow(linRGB, 0.416666667h) - 0.055h, 0.h);
}

// Based upon UnityCG.cginc, used in hdrIntensity
float3 GammaToLinearSpace(float3 sRGB)
{
    // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return sRGB * (sRGB * (sRGB * 0.305306011f + 0.682171111f) + 0.012522878f);
}

// Based upon https://forum.unity.com/threads/how-to-change-hdr-colors-intensity-via-shader.531861/
float3 hdrIntensity(float3 emissiveColor, float intensity)
{
// if not using gamma color space, convert from linear to gamma
#ifndef UNITY_COLORSPACE_GAMMA
    emissiveColor.rgb = LinearToGammaSpace(emissiveColor.rgb);
#endif
    // apply intensity exposure
    emissiveColor.rgb *= pow(2.0, intensity);
// if not using gamma color space, convert back to linear
#ifndef UNITY_COLORSPACE_GAMMA
    emissiveColor.rgb = GammaToLinearSpace(emissiveColor.rgb);
#endif

    return emissiveColor;
}

// Based upon Unity's shadergraph library functions
float3 RGBToHSV(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

// Based upon Unity's shadergraph library functions
float3 HSVToRGB(float3 c)
{
    float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
}

// Based upon Unity's shadergraph library functions
float3 RotateAboutAxis(float3 In, float3 Axis, float Rotation)
{
    float s = sin(Rotation);
    float c = cos(Rotation);
    float one_minus_c = 1.0 - c;

    Axis = normalize(Axis);
    float3x3 rot_mat =
        {one_minus_c * Axis.x * Axis.x + c, one_minus_c * Axis.x * Axis.y - Axis.z * s, one_minus_c * Axis.z * Axis.x + Axis.y * s,
         one_minus_c * Axis.x * Axis.y + Axis.z * s, one_minus_c * Axis.y * Axis.y + c, one_minus_c * Axis.y * Axis.z - Axis.x * s,
         one_minus_c * Axis.z * Axis.x - Axis.y * s, one_minus_c * Axis.y * Axis.z + Axis.x * s, one_minus_c * Axis.z * Axis.z + c};
    return mul(rot_mat, In);
}

// Based upon https://viclw17.github.io/2018/07/16/raytracing-ray-sphere-intersection/#:~:text=When the ray and sphere,equations and solving for t.
// Returns dstToSphere, dstThroughSphere
// If inside sphere, dstToSphere will be 0
// If ray misses sphere, dstToSphere = max float value, dstThroughSphere = 0
// Given rayDir must be normalized
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
    return float2(maxFloat, 0);
}

// Based upon https://mrl.cs.nyu.edu/~dzorin/rend05/lecture2.pdf
float2 intersectInfiniteCylinder(float3 rayOrigin, float3 rayDir, float3 cylinderOrigin, float3 cylinderDir, float cylinderRadius)
{
    float3 a0 = rayDir - dot(rayDir, cylinderDir) * cylinderDir;
    float a = dot(a0, a0);

    float3 dP = rayOrigin - cylinderOrigin;
    float3 c0 = dP - dot(dP, cylinderDir) * cylinderDir;
    float c = dot(c0, c0) - cylinderRadius * cylinderRadius;

    float b = 2 * dot(a0, c0);

    float discriminant = b * b - 4 * a * c;

    if (discriminant > 0)
    {
        float s = sqrt(discriminant);
        float dstToNear = max(0, (-b - s) / (2 * a));
        float dstToFar = (-b + s) / (2 * a);

        if (dstToFar >= 0)
        {
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

    return -c / b;
}

// Based upon https://mrl.cs.nyu.edu/~dzorin/rend05/lecture2.pdf
float intersectDisc(float3 rayOrigin, float3 rayDir, float3 p1, float3 p2, float3 discDir, float discRadius, float innerRadius)
{
    float discDst = maxFloat;
    float2 cylinderIntersection = intersectInfiniteCylinder(rayOrigin, rayDir, p1, discDir, discRadius);
    float cylinderDst = cylinderIntersection.x;

    if (cylinderDst < maxFloat)
    {
        float finiteC1 = dot(discDir, rayOrigin + rayDir * cylinderDst - p1);
        float finiteC2 = dot(discDir, rayOrigin + rayDir * cylinderDst - p2);

        // Ray intersects with edges of the cylinder/disc
        if (finiteC1 > 0 && finiteC2 < 0 && cylinderDst > 0)
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
            if (p1Dst > 0 && p1q1DstSqr < radiusSqr && p1q1DstSqr > innerRadiusSqr)
            {
                if (p1Dst < discDst)
                {
                    discDst = p1Dst;
                }
            }

            float p2Dst = max(intersectInfinitePlane(rayOrigin, rayDir, p2, discDir), 0);
            float3 q2 = rayOrigin + rayDir * p2Dst;
            float p2q2DstSqr = dot(q2 - p2, q2 - p2);

            // Ray intersects with upper plane of cylinder/disc
            if (p2Dst > 0 && p2q2DstSqr < radiusSqr && p2q2DstSqr > innerRadiusSqr)
            {
                if (p2Dst < discDst)
                {
                    discDst = p2Dst;
                }
            }
        }
    }

    return discDst;
}

float remap(float v, float minOld, float maxOld, float minNew, float maxNew)
{
    return minNew + (v - minOld) * (maxNew - minNew) / (maxOld - minOld);
}

float2 discUV(float3 planarDiscPos, float3 discDir, float3 centre, float radius)
{
    float3 planarDiscPosNorm = normalize(planarDiscPos);
    float sampleDist01 = length(planarDiscPos) / radius;

    float3 tangentTestVector = float3(1, 0, 0);
    if (abs(dot(discDir, tangentTestVector)) >= 1)
        tangentTestVector = float3(0, 1, 0);

    float3 tangent = normalize(cross(discDir, tangentTestVector));
    float3 biTangent = cross(tangent, discDir);
    float phi = atan2(dot(planarDiscPosNorm, tangent), dot(planarDiscPosNorm, biTangent)) / PI;
    phi = remap(phi, -1, 1, 0, 1);

    // Radial distance
    float u = sampleDist01;
    // Angular distance
    float v = phi;

    return float2(u, v);
}

float3 discColor(float3 baseColor, float3 planarDiscPos, float3 discDir, float3 cameraPos, float u, float radius)
{
    float3 newColor = baseColor;

    // Distance intensity fall-off
    float intensity = remap(u, 0, 1, 0.5, -1.2);
    intensity *= abs(intensity);

    // Doppler beaming intensity change
    float3 rotatePos = RotateAboutAxis(planarDiscPos, discDir, 0.01);
    float dopplerDistance = (length(rotatePos - cameraPos) - length(planarDiscPos - cameraPos)) / radius;
    intensity += dopplerDistance * _DiscSpeed * _DopplerBeamingFactor;

    newColor = hdrIntensity(baseColor, intensity);

    // Distance hue shift
    float3 hueColor = RGBToHSV(newColor);
    float hueShift = saturate(remap(u, _HueRadius, 1, 0, 1));
    hueColor.r += hueShift * _HueShiftFactor;
    newColor = HSVToRGB(hueColor);

    return newColor;
}

#endif // MY_BLACK_HOLE_UTILITIES_HLSL