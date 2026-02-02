#ifndef CUSTOM_RAY_MARCHING_UTILITIES_HLSL
#define CUSTOM_RAY_MARCHING_UTILITIES_HLSL

half2 IntersectAABB(half3 rayOrigin, half3 rayDir, half3 boxMin, half3 boxMax) 
{
    half3 t0 = (boxMin - rayOrigin) / rayDir;
    half3 t1 = (boxMax - rayOrigin) / rayDir;
    half3 tmin = min(t0, t1);
    half3 tmax = max(t0, t1);
    half tNear = max(max(tmin.x, tmin.y), tmin.z);
    half tFar = min(min(tmax.x, tmax.y), tmax.z);
    return half2(tNear, tFar);
}

// Sphere intersection for arbitrary mesh shapes
half2 IntersectSphere(half3 rayOrigin, half3 rayDir, half3 sphereCenter, half sphereRadius)
{
    half3 oc = rayOrigin - sphereCenter;
    half a = dot(rayDir, rayDir);
    half b = 2.0 * dot(oc, rayDir);
    half c = dot(oc, oc) - sphereRadius * sphereRadius;
    half discriminant = b * b - 4.0 * a * c;
    
    if (discriminant < 0.0)
        return half2(-1, -1);
    
    half sqrtDisc = sqrt(discriminant);
    half t0 = (-b - sqrtDisc) / (2.0 * a);
    half t1 = (-b + sqrtDisc) / (2.0 * a);
    
    return half2(max(0.0, t0), t1);
}

// Based upon Unity's shadergraph library functions
float3 RotateAboutAxis(float3 In, float3 Axis, float Rotation)
{
    float s = sin(Rotation);
    float c = cos(Rotation);
    float one_minus_c = 1.0 - c;

    Axis = normalize(Axis);
    float3x3 rot_mat = 
    {   one_minus_c * Axis.x * Axis.x + c, one_minus_c * Axis.x * Axis.y - Axis.z * s, one_minus_c * Axis.z * Axis.x + Axis.y * s,
        one_minus_c * Axis.x * Axis.y + Axis.z * s, one_minus_c * Axis.y * Axis.y + c, one_minus_c * Axis.y * Axis.z - Axis.x * s,
        one_minus_c * Axis.z * Axis.x - Axis.y * s, one_minus_c * Axis.y * Axis.z + Axis.x * s, one_minus_c * Axis.z * Axis.z + c
    };
    return mul(rot_mat,  In);
}

void RayMarching_float(
    float3 ro, float3 rd, float rayMarchSteps, UnityTexture3D dataTex,
    out float4 color) 
{
    // Use sphere intersection instead of AABB for mesh-independent ray marching
    half2 inters = IntersectSphere(ro, rd, float3(0.5, 0.5, 0.5), 0.866); // sqrt(3)/2 ≈ 0.866 for unit sphere
    
    // Early exit if ray doesn't intersect
    if (inters.x < 0.0 || inters.y <= 0.0)
    {
        color = float4(0, 0, 0, 0);
        return;
    }
    
    float3 rstart = ro + rd * max(0.0, inters.x);
    float3 rend = ro + rd * inters.y;

    float stepSize = length(rend - rstart) / rayMarchSteps;
    uint numSteps = (uint)clamp(rayMarchSteps, 1, 256);

    color = float4(0, 0, 0, 0);

    UNITY_LOOP
    for(int iStep = 0; iStep < numSteps; iStep++) 
    {
        const float t = iStep / rayMarchSteps;
        float3 samplePos = lerp(rstart, rend, t);
        
        // Apply rotation to sample position
        samplePos = RotateAboutAxis(samplePos - 0.5, float3(0, 1, 0), _Time.y * 0.2) + 0.5;
        
        // Sample from 3D texture (ensure coordinates are within [0,1] range)
        float3 texCoord = frac(samplePos * 10.0);
        float sampleVal = tex3D(dataTex, float4(texCoord, 0)).r;

        color += float4(sampleVal, sampleVal, sampleVal, sampleVal) * (1.0 / rayMarchSteps);
    }
}

#endif