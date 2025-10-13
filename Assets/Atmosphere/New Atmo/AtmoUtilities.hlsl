static const float maxFloat = 3.402823466e+38;

float2 RaySphereIntersect(
    float3 center,
    float radius,  // and the sphere radius
    float3 start, // starting position of the ray
    float3 dir   // the direction of the ray
)
{
    // ray-sphere intersection that assumes
    // the sphere is centered at the origin.
    // No intersection when result.x > result.y
    float3 offset = start - center;
    float a = 1;
    float b = 2.0 * dot(dir, offset);
    float c = dot(offset, offset) - (radius * radius);
    float d = (b * b) - 4.0 * a * c;
    if (d >= 0.0)
    {
        float s = sqrt(d);
        float dstToSphereNear = max(0, (-b - s) / (2 * a));
        float dstToSphereFar = (-b + s) / (2 * a);
        if (dstToSphereFar >= 0)
        {
            return float2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
        }
    }
    return float2(maxFloat, 0.0);
}

float3 CalculateScattering(
    float3 start,
    float3 dir,
    float maxDistance,
    float3 sceneColor,
    float3 lightDir,
    float3 planetPos,
    float planetRadius,
    float atmosphereRadius,
    float stepsI,
    float stepsL)
{
    // // subtract planet position(object world position) to get object local space
    start -= unity_ObjectToWorld._m03_m13_m23;
    float a = dot(dir, dir);
    float b = 2.0 * dot(dir, start);
    float c = dot(start, start) - (atmosphereRadius * atmosphereRadius);
    float d = b * b - 4.0 * a * c;
    // If not hit planet, return scene color
    if (d < 0.0)
    {
        return sceneColor;
    }

    float2 rayLength = float2(
        max((-b - sqrt(d)) / (2.0 * a), 0.0),
        min((-b + sqrt(d)) / (2.0 * a), maxDistance));

    if (rayLength.x > rayLength.y)
    {
        return sceneColor;
    }

    bool allowMie = maxDistance > rayLength.y;
    rayLength.y = min(rayLength.y, maxDistance);// Far
    rayLength.x = max(rayLength.x, 0);          // Near

    float stepSizeI = (rayLength.y - rayLength.x) / float(stepsI);

    float rayPosI = rayLength.x + stepSizeI * 0.5;

    float3 totalRayleigh = 0;
    float3 totalMie = 0;

    float3 opticalDepthI = 0;

    float2 scaleHeight = float2(_HeightRayleigh, _HeightMie);

    float mu = dot(dir, lightDir);
    float mumu = mu * mu;
    float gg = _G * _G;
    float phaseRayleigh = 3 / 50.2654824574 * (1 + mumu);
    float phaseMie = allowMie ? 3 / 25.1327412287 * ((1 - gg) * (1 + mumu)) / (pow(1 + gg - 2 * _G * mu, 1.5) * (2.0 + gg)) : 0;

    for (int i = 0; i < stepsI; ++i)
    {
        float3 posI = start + dir * rayPosI;
        // get object scaled radius

        float heightI = length(posI) - _PlanetRadius;

        float3 density = float3(exp(-heightI / scaleHeight), 0);

        float denom = (_HeightAbsorption - heightI) / _AbsorptionFalloff;
        density.z = (1.0 / (denom * denom + 1.0)) * density.x;

        density *= stepSizeI;

        opticalDepthI += density;

        a = dot(lightDir, lightDir);
        b = 2.0 * dot(lightDir, posI);
        c = dot(posI, posI) - (atmosphereRadius * atmosphereRadius);
        d = (b * b) - 4.0 * a * c;

        float stepSizeL = (-b + sqrt(d)) / (2 * a * float(stepsL));

        float rayPosL = stepSizeL * 0.5;

        float3 opticalDepthL = 0;

        for (int l = 0; l < stepsL; ++l)
        {
            float3 posL = posI + lightDir * rayPosL;

            float heightL = length(posL) - _PlanetRadius;

            float3 densityL = float3(exp(-heightL / scaleHeight), 0);

            float denomL = (_HeightAbsorption - heightL) / _AbsorptionFalloff;
            densityL.z = (1.0 / (denomL * denomL + 1.0)) * densityL.x;

            densityL *= stepSizeL;

            opticalDepthL += densityL;

            rayPosL += stepSizeL;
        }

        float3 attenuation = exp(-(_RayleighBeta * (opticalDepthI.x + opticalDepthL.x) - _MieBeta * (opticalDepthI.y + opticalDepthL.y) - _AbsorptionBeta * (opticalDepthI.z + opticalDepthL.z)));
        totalRayleigh += density.x * attenuation;
        totalMie += density.y * attenuation;

        rayPosI += stepSizeI;
    }

    float3 opacity = exp(-(_MieBeta * opticalDepthI.y + _RayleighBeta * opticalDepthI.x + _AbsorptionBeta * opticalDepthI.z));
    return (phaseRayleigh * _RayleighBeta * totalRayleigh +
            phaseMie * _MieBeta * totalMie +
            opticalDepthI.x * _AmbientBeta) *
           _LightIntensity;
}

