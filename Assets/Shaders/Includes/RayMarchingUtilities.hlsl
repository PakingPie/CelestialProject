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
    float2 inters = IntersectAABB(ro, rd, float3(0,0,0), float3(1,1,1));
    float3 farPos = ro + rd * inters.y - 0.5;
    float4 clipPos = TransformObjectToHClip(farPos);
    inters += min(clipPos.w, 0.0);
    float3 rend = ro + rd * inters.y;

    // rd = -rd;
    // float3 temp = ro;
    // ro = rend;
    // rend = temp;

    float stepSize = 1.732 / rayMarchSteps;
    uint numSteps = (uint)clamp(abs(inters.y - inters.x) / stepSize, 1, rayMarchSteps);

    ro += rd * stepSize;

    color = 0;

    UNITY_LOOP
    for(int iStep = 0; iStep < numSteps; iStep++) 
    {
        const float t = iStep * rcp(rayMarchSteps);
        float3 samplePos = lerp(ro, rend, t);
        samplePos = RotateAboutAxis(samplePos - 0.5, float3(0,1,0), _Time.y * 0.2) + 0.5;
        float sampleVal = tex3D(dataTex, float4(samplePos, 0)).r;

        color += float4(sampleVal, sampleVal, sampleVal, sampleVal) * (1.0 / rayMarchSteps);
    }
}

#endif