Shader "Custom/VolumetricCloudsSphere"
{
    Properties
    {
        [Header(Cloud Shape)]
        _CloudDensity ("Cloud Density", Range(0, 50)) = 8.0
        _CloudCoverage ("Cloud Coverage", Range(0, 1)) = 0.45
        _CloudScale ("Cloud Scale", Range(0.1, 50)) = 8.0
        _DetailScale ("Detail Scale", Range(1, 20)) = 6.0
        _DetailStrength ("Detail Strength", Range(0, 1)) = 0.4
        _ErosionStrength ("Erosion Strength", Range(0, 1)) = 0.25
        _Patchiness ("Patchiness", Range(0, 1)) = 0.7
        _PatchScale ("Patch Scale", Range(0.1, 10)) = 2.0
        _Billowness ("Billowness", Range(0, 1)) = 0.5
        
        [Header(Sphere Settings)]
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.50
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.54
        _CloudLayerDensity ("Layer Density Multiplier", Range(0.1, 5)) = 1.5
        
        [Header(Raymarching)]
        _MaxSteps ("Max Steps", Range(8, 256)) = 96
        _StepSize ("Step Size", Range(0.0001, 0.02)) = 0.002
        _LightSteps ("Light March Steps", Range(3, 12)) = 6
        
        [Header(Lighting)]
        _LightAbsorption ("Light Absorption", Range(0, 10)) = 1.8
        _CloudAbsorption ("Cloud Self Shadow", Range(0, 5)) = 2.5
        _AmbientLight ("Ambient Light", Range(0, 2)) = 0.35
        _ScatteringForward ("Forward Scattering", Range(0, 0.99)) = 0.85
        _ScatteringBack ("Back Scattering", Range(0, 0.99)) = 0.25
        _ScatteringBlend ("Scattering Blend", Range(0, 1)) = 0.7
        _SilverLiningIntensity ("Silver Lining", Range(0, 3)) = 1.2
        _SilverLiningSpread ("Silver Lining Spread", Range(1, 20)) = 6.0
        _PowderStrength ("Powder Effect", Range(0, 1)) = 0.4
        _MultiScatter ("Multi-Scattering", Range(0, 1)) = 0.5
        
        [Header(Color)]
        _CloudColorBright ("Cloud Color Bright", Color) = (1, 0.98, 0.95, 1)
        _CloudColorDark ("Cloud Color Dark", Color) = (0.55, 0.58, 0.65, 1)
        _AmbientColorTop ("Ambient Color Top", Color) = (0.6, 0.75, 1.0, 1)
        _AmbientColorBottom ("Ambient Color Bottom", Color) = (0.4, 0.42, 0.5, 1)
        _SunColor ("Sun Tint", Color) = (1.0, 0.95, 0.85, 1)

        [Header(Fire Effect)]
        [Toggle] _FireEnabled ("Enable Fire", Float) = 0
        _FireIntensity ("Fire Intensity", Range(0, 10)) = 2.0
        _FireColorBright ("Fire Color Bright", Color) = (1.0, 0.85, 0.3, 1)
        _FireColorDark ("Fire Color Dark", Color) = (0.7, 0.1, 0.02, 1)
        _FireScale ("Fire Pattern Scale", Range(0.1, 20)) = 5.0
        _FireDetailScale ("Fire Detail Scale", Range(1, 8)) = 3.0
        _FireCoverage ("Fire Coverage", Range(0, 1)) = 0.5
        _FireHeightFalloff ("Fire Height Falloff", Range(0.1, 5)) = 1.5
        _FireAnimSpeed ("Fire Animation Speed", Range(0, 2)) = 0.4
        _FireDayFade ("Fire Day-side Fade", Range(0, 1)) = 0.7
        
        [Header(Animation)]
        _WindSpeed ("Wind Speed", Range(0, 0.5)) = 0.03
        _WindDirection ("Wind Direction", Vector) = (1, 0, 0.2, 0)
        _DetailWindMultiplier ("Detail Wind Speed", Range(0.5, 3)) = 1.5
        
        [Header(Textures)]
        _NoiseTexture ("3D Noise Texture", 3D) = "white" {}
        _NoiseTiling ("Noise Tiling", Vector) = (1, 1, 1, 0)
        _NoiseOffset ("Noise Offset", Vector) = (0, 0, 0, 0)
        _BlueNoise ("Blue Noise", 2D) = "gray" {}
        _BlueNoiseTiling ("Blue Noise Tiling", Vector) = (1, 1, 0, 0)
        _BlueNoiseOffset ("Blue Noise Offset", Vector) = (0, 0, 0, 0)
    }
    
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        
        Pass
        {
            Name "VolumetricCloudPass"
            
            // [FIX 5] Back-face only, no depth test, no depth write
            Cull Front
            ZTest Always
            ZWrite Off
            // [FIX 3] Correct premultiplied-alpha blend (was SrcAlpha One)
            Blend One OneMinusSrcAlpha
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE

            #pragma shader_feature_local _FIREENABLED_ON
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // ============================================================
            // Properties
            // ============================================================
            float _CloudDensity;
            float _CloudCoverage;
            float _CloudScale;
            float _DetailScale;
            float _DetailStrength;
            float _ErosionStrength;
            float _Patchiness;
            float _PatchScale;
            float _Billowness;
            float _InnerRadius;
            float _OuterRadius;
            float _CloudLayerDensity;
            int _MaxSteps;
            float _StepSize;
            int _LightSteps;
            float _LightAbsorption;
            float _CloudAbsorption;
            float _AmbientLight;
            float _ScatteringForward;
            float _ScatteringBack;
            float _ScatteringBlend;
            float _SilverLiningIntensity;
            float _SilverLiningSpread;
            float _PowderStrength;
            float _MultiScatter;
            float4 _CloudColorBright;
            float4 _CloudColorDark;
            float4 _AmbientColorTop;
            float4 _AmbientColorBottom;
            float4 _SunColor;

            // Fire effect properties (currently unused in shader code, but defined for future implementation)
            float _FireIntensity;
            float4 _FireColorBright;
            float4 _FireColorDark;
            float _FireScale;
            float _FireDetailScale;
            float _FireCoverage;
            float _FireHeightFalloff;
            float _FireAnimSpeed;
            float _FireDayFade;

            float _WindSpeed;
            float4 _WindDirection;
            float _DetailWindMultiplier;
            float4 _NoiseTiling;
            float4 _NoiseOffset;
            float4 _BlueNoiseTiling;
            float4 _BlueNoiseOffset;
            
            TEXTURE3D(_NoiseTexture);
            SAMPLER(sampler_NoiseTexture);
            TEXTURE2D(_BlueNoise);
            SAMPLER(sampler_BlueNoise);
            
            // [FIX 7] Depth buffer for scene occlusion
            TEXTURE2D_X_FLOAT(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);
            
            // ============================================================
            // Structures (cleaned — removed unused interpolators)
            // ============================================================
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 viewDirWS  : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
            };
            
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
                float cumulus = saturate(RemapValue(heightFraction, 0.0, 0.1, 0.0, 1.0)) 
                * saturate(RemapValue(heightFraction, 0.2, 0.5, 1.0, 0.9))
                * saturate(RemapValue(heightFraction, 0.5, 1.0, 0.9, 0.0));
                
                // Stratus: wider, flatter distribution
                float stratus = saturate(RemapValue(heightFraction, 0.0, 0.1, 0.0, 1.0))
                * saturate(RemapValue(heightFraction, 0.3, 0.95, 1.0, 0.0));
                
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
                    float3 detailUV = GetCloudUV(positionOS, _CloudScale * _DetailScale, heightFraction)
                    + windOffset * _DetailWindMultiplier;
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
                float  time    = _Time.y * _FireAnimSpeed;
                float3 normal  = normalize(positionOS);

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

                if (fireMask < 0.001) return float3(0, 0, 0);

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
                float2 outerHitL = RaySphereIntersect(positionOS, lightDirOS, float3(0,0,0), _OuterRadius);
                float2 innerHitL = RaySphereIntersect(positionOS, lightDirOS, float3(0,0,0), _InnerRadius);
                
                float lightEnd = max(outerHitL.y, 0.001);
                if (innerHitL.x > 0.0)
                lightEnd = min(lightEnd, innerHitL.x);
                
                float stepSize = lightEnd / float(_LightSteps);
                
                float3 refDir = abs(dot(lightDirOS, float3(0,1,0))) > 0.99
                ? float3(1,0,0)
                : float3(0,1,0);
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
                float3 multiScatter = lerp(float3(0.5, 0.6, 0.7), float3(1,1,1), multiScatterEnergy) * _MultiScatter;
                
                // Apply planet shadow to the final result
                return (lightEnergy + multiScatter * 0.15) * planetShadow;
            }
            
            // ============================================================
            // Vertex shader (slimmed — only outputs what fragment needs)
            // ============================================================
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = posInputs.positionCS;
                output.viewDirWS  = GetWorldSpaceViewDir(posInputs.positionWS);
                output.screenPos  = ComputeScreenPos(output.positionHCS);
                
                return output;
            }
            
            // ============================================================
            // Fragment shader
            // [FIX 2] Removed NoL post-multiply (was destroying volumetric lighting)
            // [FIX 5] Removed SV_Depth output
            // [FIX 6] Removed manual pow(1/2.2) gamma (URP handles this)
            // [FIX 7] Added depth buffer occlusion
            // ============================================================
            float4 frag(Varyings input) : SV_Target
            {
                float3 cameraPositionOS = TransformWorldToObject(_WorldSpaceCameraPos);
                float3 rayDirWS = normalize(-input.viewDirWS);
                float3 rayDirOS = normalize(TransformWorldToObjectDir(rayDirWS));
                
                float2 outerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0,0,0), _OuterRadius);
                float2 innerHit = RaySphereIntersect(cameraPositionOS, rayDirOS, float3(0,0,0), _InnerRadius);
                
                if (outerHit.x < 0.0 && outerHit.y < 0.0)
                return float4(0, 0, 0, 0);
                
                // =========================================================
                // Build up to 2 march segments.
                //
                //   Outside, ray hits inner sphere:
                //     Seg 0: outerHit.x  → innerHit.x   (near shell)
                //     Seg 1: innerHit.y  → outerHit.y   (far shell)  ← WAS MISSING
                //
                //   Outside, grazing (no inner hit):
                //     Seg 0: outerHit.x  → outerHit.y
                //
                //   Inside inner sphere:
                //     Seg 0: innerHit.y  → outerHit.y
                //
                //   Inside shell, looking inward:
                //     Seg 0: 0           → innerHit.x
                //     Seg 1: innerHit.y  → outerHit.y   (far shell)
                //
                //   Inside shell, looking outward:
                //     Seg 0: 0           → outerHit.y
                // =========================================================
                float4 segments = float4(0, 0, 0, 0); // (start0, end0, start1, end1)
                int numSegments = 0;
                float cameraRadius = length(cameraPositionOS);
                
                if (cameraRadius > _OuterRadius)
                {
                    if (outerHit.x < 0.0)
                    return float4(0, 0, 0, 0);
                    
                    segments.x = outerHit.x;
                    if (innerHit.x > 0.0)
                    {
                        segments.y = innerHit.x;       // near shell ends at inner sphere
                        segments.z = innerHit.y;       // far shell starts where ray exits inner sphere
                        segments.w = outerHit.y;       // far shell ends at outer sphere exit
                        numSegments = 2;
                    }
                    else
                    {
                        segments.y = outerHit.y;
                        numSegments = 1;
                    }
                }
                else if (cameraRadius < _InnerRadius)
                {
                    segments.x = innerHit.y;
                    segments.y = outerHit.y;
                    numSegments = 1;
                }
                else
                {
                    segments.x = 0.0;
                    if (innerHit.x > 0.0)
                    {
                        segments.y = innerHit.x;
                        segments.z = innerHit.y;
                        segments.w = outerHit.y;
                        numSegments = 2;
                    }
                    else
                    {
                        segments.y = outerHit.y;
                        numSegments = 1;
                    }
                }
                
                // ---- Scene depth occlusion ----
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float sceneDepthRaw = SAMPLE_TEXTURE2D_X(
                _CameraDepthTexture, sampler_CameraDepthTexture, screenUV).r;
                
                // Check if there is actual scene geometry (skip far-plane / sky pixels)
                float linearDepth01 = Linear01Depth(sceneDepthRaw, _ZBufferParams);
                bool hasSceneGeometry = linearDepth01 < 0.99;
                
                float sceneDistOS = 1e20; // default: nothing blocking
                if (hasSceneGeometry)
                {
                    float3 sceneWorldPos = ComputeWorldSpacePosition(
                    screenUV, sceneDepthRaw, UNITY_MATRIX_I_VP);
                    float3 sceneObjectPos = TransformWorldToObject(sceneWorldPos);
                    sceneDistOS = dot(sceneObjectPos - cameraPositionOS, rayDirOS);
                }
                
                // ---- Blue noise dithering ----
                float2 blueNoiseUV = screenUV * _ScreenParams.xy / 256.0;
                blueNoiseUV = blueNoiseUV * _BlueNoiseTiling.xy + _BlueNoiseOffset.xy;
                float blueNoise = SAMPLE_TEXTURE2D(_BlueNoise, sampler_BlueNoise, blueNoiseUV).r;
                
                // ---- Lighting setup ----
                Light mainLight = GetMainLight();
                float3 lightDirOS = normalize(TransformWorldToObjectDir(mainLight.direction));
                float3 lightColor = mainLight.color.rgb * _SunColor.rgb;
                float cosTheta = dot(rayDirOS, lightDirOS);
                float phase = DualLobePhase(cosTheta);
                float silverLining = pow(saturate(cosTheta * 0.5 + 0.5), _SilverLiningSpread)
                * _SilverLiningIntensity;
                
                // ---- Raymarch state (persists across both segments) ----
                float transmittance = 1.0;
                float3 luminance = float3(0, 0, 0);
                int stepsPerSeg = _MaxSteps / max(numSegments, 1);
                
                // ---- March each segment ----
                for (int seg = 0; seg < 2; seg++)
                {
                    if (seg >= numSegments) break;
                    if (transmittance < 0.01) break;
                    
                    float segStart = (seg == 0) ? segments.x : segments.z;
                    float segEnd   = (seg == 0) ? segments.y : segments.w;
                    
                    // Depth occlusion: if scene geometry is in front of this segment,
                    // this segment and everything beyond is hidden.
                    if (sceneDistOS > 0.0 && sceneDistOS <= segStart)
                    break;
                    if (sceneDistOS > 0.0)
                    segEnd = min(segEnd, sceneDistOS);
                    
                    if (segStart >= segEnd)
                    continue;
                    
                    float segLength = segEnd - segStart;
                    float segStepSize = max(_StepSize, segLength / float(stepsPerSeg));
                    int segSteps = min(stepsPerSeg, max(1, int(segLength / segStepSize)));
                    
                    float ditheredStart = segStart + blueNoise * segStepSize;
                    
                    [loop]
                    for (int i = 0; i < _MaxSteps; i++)
                    {
                        if (i >= segSteps) break;
                        if (transmittance < 0.01) break;
                        
                        float t = ditheredStart + float(i) * segStepSize;
                        if (t > segEnd) break;
                        
                        float3 samplePos = cameraPositionOS + rayDirOS * t;
                        float density = SampleCloudDensity(samplePos, false, blueNoise);
                        
                        if (density > 0.001)
                        {
                            float radius = length(samplePos);
                            float heightFraction = saturate(
                            (radius - _InnerRadius) / max(_OuterRadius - _InnerRadius, 0.0001));
                            
                            float3 lightEnergy = SampleLightEnergy(
                            samplePos, lightDirOS, heightFraction, cosTheta);
                            
                            float3 ambientColor = lerp(
                            _AmbientColorBottom.rgb, _AmbientColorTop.rgb, heightFraction);
                            float3 groundBounce = _AmbientColorBottom.rgb * 0.2 * (1.0 - heightFraction);

                            float NoL = dot(normalize(samplePos), lightDirOS);
                            float dayFactor = smoothstep(-0.1, 0.3, NoL);     // soft terminator ramp
                            float ambientScale = lerp(0.08, 1.0, dayFactor);   // night side keeps ~8% ambient

                            float3 ambient = (ambientColor + groundBounce) * _AmbientLight * ambientScale;
                            
                            float3 directLight = lightColor * lightEnergy * phase;
                            
                            float edgeFactor = 1.0 - pow(saturate(density * 2.0), 0.5);
                            directLight += lightColor * lightEnergy.x * silverLining
                            * edgeFactor * (0.5 + 0.5 * heightFraction);
                            
                            float lightIntensity = dot(lightEnergy, float3(0.33, 0.33, 0.33));
                            float3 cloudAlbedo = lerp(
                            _CloudColorDark.rgb,
                            _CloudColorBright.rgb,
                            pow(saturate(lightIntensity), 0.6));
                            
                            float stepDensity = density * segStepSize;
                            float stepTransmittance = exp(-stepDensity * _LightAbsorption);
                            
                            float3 scatteringIntegral = (directLight + ambient) * cloudAlbedo;
                            float3 inScattering = scatteringIntegral * (1.0 - stepTransmittance);

                            // ---- Fire emission ----
                            #ifdef _FIREENABLED_ON
                                {
                                    float3 fireEmission = SampleFireEmission(samplePos, heightFraction, density);

                                    // Fire glow fades on sun-lit side (hard to see glow in daylight)
                                    float fireDayMask = lerp(1.0, 1.0 - dayFactor, _FireDayFade);
                                    fireEmission *= fireDayMask;

                                    // Emission contribution: weighted by optical depth of this step
                                    // and accumulated transmittance (same integration as scattering)
                                    inScattering += fireEmission * (1.0 - stepTransmittance);

                                    // Fire tints cloud albedo toward warm hues where emission is strong
                                    float fireLum = dot(fireEmission, float3(0.299, 0.587, 0.114));
                                    float tintAmount = saturate(fireLum * 0.3);
                                    float3 warmTint = lerp(float3(1,1,1), normalize(fireEmission + 0.001), tintAmount);
                                    inScattering *= lerp(float3(1,1,1), warmTint, tintAmount);
                                }
                            #endif

                            luminance += inScattering * transmittance;
                            transmittance *= stepTransmittance;
                        }
                    }
                }
                
                float alpha = 1.0 - transmittance;
                
                if (alpha < 0.003)
                return float4(0, 0, 0, 0);
                
                luminance = 1.0 - exp(-luminance * 1.2);
                
                return float4(luminance * alpha, alpha);
            }
            
            ENDHLSL
        }
    }
    
    FallBack Off
}