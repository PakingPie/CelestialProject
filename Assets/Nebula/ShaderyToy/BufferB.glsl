/*
    Volumetric clouds with gyroid noise
    See https://www.shadertoy.com/view/3sffzj for more detailed comments on clouds.
*/

// Different step counts for full and interactive states
#define STEPS_PRIMARY 75
#define STEPS_PRIMARY_LOW 7

#define STEPS_LIGHT 32
#define STEPS_LIGHT_LOW 7

// Offset the sample point by blue noise to get rid of banding
#define DITHERING
const float goldenRatio = 1.61803398875;

// Scattering coefficient based on Earth's atmosphere but tweaked for this look
const vec3 BETA_RAYLEIGH = 100.0*vec3(0.05802, 0.14558, 0.331);
const vec3 BETA_OZONE = vec3(0.650, 1.881, 0.085);

// Scattering
const vec3 sigmaS = 2.0*(BETA_RAYLEIGH);
// Absorption
const vec3 sigmaA = 4.0*(BETA_RAYLEIGH + 3.0*BETA_OZONE);
// Extinction
const vec3 sigmaE = sigmaA; // + sigmaS

const float sunLocation = -1.0;
const float sunHeight = 0.5;
        
// Main light power
const float power = 200.0;

// For size of AABB
#define CLOUD_EXTENT 10.0
const vec3 minCorner = vec3(-CLOUD_EXTENT, -CLOUD_EXTENT, -CLOUD_EXTENT);
const vec3 maxCorner = vec3(CLOUD_EXTENT, CLOUD_EXTENT, CLOUD_EXTENT);

//-------------------------------- Camera --------------------------------

vec3 rayDirection(float fieldOfView, vec2 fragCoord) {
    vec2 xy = fragCoord - iResolution.xy / 2.0;
    float z = (0.5 * iResolution.y) / tan(radians(fieldOfView) / 2.0);
    return normalize(vec3(xy, -z));
}

// https://www.geertarien.com/blog/2017/07/30/breakdown-of-the-lookAt-function-in-OpenGL/
mat3 lookAt(vec3 targetDir, vec3 up){
  vec3 zaxis = normalize(targetDir);    
  vec3 xaxis = normalize(cross(zaxis, up));
  vec3 yaxis = cross(xaxis, zaxis);

  return mat3(xaxis, yaxis, -zaxis);
}

//-------------------------------- Intersection --------------------------------

// https://gist.github.com/DomNomNom/46bb1ce47f68d255fd5d
// Compute the near and far intersections using the slab method.
// No intersection if tNear > tFar.
vec2 intersectAABB(vec3 rayOrigin, vec3 rayDir, vec3 boxMin, vec3 boxMax) {
    vec3 tMin = (boxMin - rayOrigin) / rayDir;
    vec3 tMax = (boxMax - rayOrigin) / rayDir;
    vec3 t1 = min(tMin, tMax);
    vec3 t2 = max(tMin, tMax);
    float tNear = max(max(t1.x, t1.y), t1.z);
    float tFar = min(min(t2.x, t2.y), t2.z);
    return vec2(tNear, tFar);
}

bool insideAABB(vec3 p){
    float eps = 1e-4;
	return  (p.x > minCorner.x-eps) && (p.y > minCorner.y-eps) && (p.z > minCorner.z-eps) && 
			(p.x < maxCorner.x+eps) && (p.y < maxCorner.y+eps) && (p.z < maxCorner.z+eps);
}

bool getCloudIntersection(vec3 org, vec3 dir, out float distToStart, out float totalDistance){
	vec2 intersections = intersectAABB(org, dir, minCorner, maxCorner);
	
    if(insideAABB(org)){
        intersections.x = 1e-4;
    }
    
    distToStart = intersections.x;
    totalDistance = intersections.y - intersections.x;
    return intersections.x > 0.0 && (intersections.x < intersections.y);
}

// https://www.shadertoy.com/view/3s3GDn
float getGlow(float dist, float radius, float intensity){
	return max(0.0, pow(radius/max(dist, 1e-5), intensity));	
}

//-------------------------------- Random --------------------------------

// https://www.shadertoy.com/view/4djSRW
vec3 hash(vec3 p3){
    p3 = fract(p3 * vec3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yxz + 33.33);
    return 2.0 * fract((p3.xxy + p3.yxx) * p3.zyx) - 1.0;
}

//-------------------------------- Shape --------------------------------

// https://en.wikipedia.org/wiki/Gyroid
// https://www.shadertoy.com/view/wddfDM
float gyroid(vec3 p, float thickness, float bias, float frequency){
    return clamp(abs(dot(sin(p*0.5), cos(p.zxy*1.23) * frequency) - bias) - thickness, 0.0, 3.0)/3.0;
}

// Gyroid noise based on https://www.shadertoy.com/view/3l23Rh
float fbm(vec3 p){

    const int octaves = 12;
    const float fbmScale = 1.95;

    // Rotation of the gyroid every iteration to produce a noise look
    const float a = PI / float(octaves);
    const mat3 m3 = fbmScale * mat3(cos(a), sin(a), 0, -sin(a), cos(a), 0, 0, 0, 1);


    float weight = 0.0;
    float amplitude = 1.0;
    float frequency = 1.0;
    float res = 0.0;
    
    
    for(int i = min(0, iFrame); i < octaves; i++){
        res += amplitude * gyroid(p, 0.1, 0.0, frequency);
        p *= m3;
        weight += amplitude;
        amplitude *= (i < 4 ? 0.9 : 0.7);
        frequency *= (i < 3 ? 0.65 : 0.78);
    }
    
    return saturate(res / weight);
}

float clouds(vec3 p){
    if(!insideAABB(p)){
        return 0.0;
    }
    float noise = fbm(0.25*p);
    float structure = smoothstep(3.0, 5.0, length(p)) * smoothstep(0.05, 0.1, noise);
    float haze = smoothstep(2.0, 10.0, length(p)) * smoothstep(0.02, 0.5, noise);
    return 3e-4+(0.5*haze + 0.75 * structure);

}


//-------------------------------- Stars --------------------------------

// https://iquilezles.org/articles/palettes/
vec3 getColour(float t){
    vec3 a = vec3(0.65);
    vec3 b = 1.0 - a;
    vec3 c = vec3(1.0,1.0,1.0);
    vec3 d = vec3(0.15,0.5,0.75);

    return pow(a + b * cos(TWO_PI * (c * t + d)), vec3(2.2));
}


// Stars with random placement and strength
vec3 getStars(vec3 p){
    p *= 0.2;
    vec3 rand;
    float d = 1e10;
    vec3 cell;
    
    for(int x = -1; x <= 1; x++){
        for(int y = -1; y <= 1; y++){
            for(int z = -1; z <= 1; z++){
                vec3 c = floor(p) + vec3(x, y, z);
                vec3 h = hash(c);
                vec3 f = c + 0.5 + 0.5 * h;
                float dd = length(p - f);
                if(dd < d){
                    d = dd;
                    rand = h;
                    cell = c;
                }
            }
        }
    }
    rand = clamp(0.5 + 0.5 * rand, 0.0, 1.0);
    vec3 rand2 = clamp(0.5 + 0.5 * hash(cell+vec3(3.12, 104.9, -9.5)), 0.0, 1.0);
    return vec3(0.05) * rand.z * step(0.45, rand2.z) * mix(vec3(1), getColour(rand.y), 0.3) * smoothstep(0.5, 0.0, d) * getGlow(d, 0.25, 2.0);
}

//-------------------------------- Lighting --------------------------------

float HenyeyGreenstein(float g, float costh){
	return (1.0 / (4.0 * 3.1415))  * ((1.0 - g * g) / pow(1.0 + g*g - 2.0*g*costh, 1.5));
}

// https://twitter.com/FewesW/status/1364629939568451587/photo/1
vec3 multipleOctaves(float extinction, float mu, float stepL){

    vec3 luminance = vec3(0);
    const float octaves = 6.0;
    
    // Attenuation
    float a = 1.0;
    // Contribution
    float b = 1.0;
    // Phase attenuation
    float c = 1.0;
    
    float phase;
    
    for(float i = 0.0; i < octaves; i++){
        // Two-lobed HG
        phase = mix(HenyeyGreenstein(-0.1 * c, mu), HenyeyGreenstein(0.3 * c, mu), 0.7);
        luminance += b * phase * exp(-stepL * extinction * sigmaE * a);
        // Lower is brighter
        a *= 0.3;
        // Higher is brighter
        b *= 0.5;
        c *= 0.5;
    }
    return luminance;
}

// Get the amount of light that reaches a sample point.
vec3 lightRay(vec3 org, vec3 p, float phaseFunction, float mu, vec3 sunDirection, bool low){

	float lightRayDistance = CLOUD_EXTENT*0.25;
    float distToStart = 0.0;
    
    getCloudIntersection(p, sunDirection, distToStart, lightRayDistance);
        
    float stepL = lightRayDistance/float(low ? STEPS_LIGHT_LOW : STEPS_LIGHT);

	float lightRayDensity = 0.0;

	// Collect total density along light ray.
	for(int j = 0; j < (low ? STEPS_LIGHT_LOW : STEPS_LIGHT); j++){
		lightRayDensity += clouds(p + sunDirection * float(j) * stepL);
	}
    
	vec3 beersLaw = multipleOctaves(lightRayDensity, mu, stepL);
	
    // Return product of Beer's law and powder effect depending on the 
    // view direction angle with the light direction.
	return mix(beersLaw * 2.0 * (1.0 - (exp( -stepL * lightRayDensity * 2.0 * sigmaE))), 
               beersLaw, 
               0.5 + 0.5 * mu);
}


//-------------------------------- Raymarching --------------------------------

// Get the colour along the main view ray.
vec3 mainRay(vec3 org, vec3 dir, vec3 sunDirection, out vec3 totalTransmittance, float offset, bool low){
    
	// Variable to track transmittance along view ray. 
    // Assume clear sky and attenuate light when encountering clouds.
	totalTransmittance = vec3(1.0);

	// Default to black.
	vec3 colour = vec3(0.0);
    
    // The distance at which to start ray marching.
    float distToStart = 0.0;
    
    // The length of the intersection.
    float totalDistance = 0.0;

    // Determine if ray intersects bounding volume.
	// Set ray parameters in the cloud layer.
	bool renderClouds = getCloudIntersection(org, dir, distToStart, totalDistance);
   
	if(!renderClouds){
		return colour;
    }

	// Sampling step size.
    float stepS = totalDistance / float(low ? STEPS_PRIMARY_LOW : STEPS_PRIMARY); 
    
    // Offset the starting point by blue noise.
    distToStart += stepS * offset;
    
    // Track distance to sample point.
    float dist = distToStart;

    // Initialise sampling point.
    vec3 p = org + dist * dir;
    
    float mu = dot(dir, sunDirection);
    // Combine backward and forward scattering to have details in all directions.
	float phaseFunction = mix(HenyeyGreenstein(-0.3, mu), HenyeyGreenstein(0.3, mu), 0.7);
    
    vec3 sunLight = vec3(1) * power;

	for(int i = 0; i < (low ? STEPS_PRIMARY_LOW : STEPS_PRIMARY); i++){

        float density = clouds(p);

        vec3 sampleSigmaS = sigmaS * density;
        vec3 sampleSigmaE = sigmaE * density;

        // If there is a cloud at the sample point.
        if(density > 0.0 ){

            // Stars in the clouds
            vec3 ambient = 1.0*getStars(p) + 
                           2.0*getStars(1.5*p + 17.51) + 
                           1.0*getStars(2.4*p - 6.2) +
                           getStars(3.7*p + 109.9);
            
            // Scale starlight by the density at the sample point
            ambient *= smoothstep(1e-3, 2e-3, density);

            // Amount of sunlight that reaches the sample point through the cloud 
            // is the combination of ambient light and attenuated direct light.
            vec3 luminance = ambient + sunLight * phaseFunction * 
                                        lightRay(org, p, phaseFunction, mu, sunDirection, low);

            // Scale light contribution by density of the cloud.
            luminance *= sampleSigmaS;

            // Beer-Lambert.
            vec3 transmittance = exp(-sampleSigmaE * stepS);

            // Better energy conserving integration
            // "From Physically based sky, atmosphere and cloud rendering in Frostbite" 5.6
            // by Sebastian Hillaire.
            colour += 
                totalTransmittance * (luminance - luminance * transmittance) / sampleSigmaE; 

            // Attenuate the amount of light that reaches the camera.
            totalTransmittance *= transmittance;  

            // If ray combined transmittance is close to 0, nothing beyond this sample 
            // point is visible, so break early.
            if(length(totalTransmittance) <= 0.001){
                totalTransmittance = vec3(0.0);
                return colour;
            }
        }

        dist += stepS;

		// Step along ray.
		p = org + dir * dist;
	}

	return colour;
}

// https://knarkowicz.wordpress.com/2016/01/06/aces-filmic-tone-mapping-curve/
vec3 ACESFilm(vec3 x){
    return clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0.0, 1.0);
}

void mainImage( out vec4 fragColor, in vec2 fragCoord ){
    
    float tileSize = iResolution.x < 2000.0 ? 256.0 : 128.0;
    float framesToDraw = ceil(iResolution.x / tileSize);
    
    float framesDrawn = texelFetch(iChannel1, ivec2(0.5, 0.5), 0).x;
    bool blueNoiseNotLoaded = iChannelResolution[2].xy != vec2(1024);
    bool resolutionChanged = texelFetch(iChannel0, ivec2(0.5, 2.5), 0).x > 0.0;
    
    bool renderPreview = resolutionChanged || blueNoiseNotLoaded || iFrame == 0 || iMouse.z > 0.0;
    bool renderFull = framesDrawn < framesToDraw && !renderPreview;
    
    float tileStart = framesDrawn * tileSize;
    bool renderThisFrame = fragCoord.x > tileStart && fragCoord.x < tileStart + tileSize;
    
    if(renderPreview || (renderFull && renderThisFrame)){

        //----------------- Define a camera -----------------

        // Get the default direction of the ray (along the negative Z direction)
        vec3 rayDir = rayDirection(55.0, fragCoord);
        
        vec3 cameraPos = texelFetch(iChannel0, ivec2(0.5, 1.5), 0).xyz;

        vec3 targetDir = -cameraPos;

        vec3 up = vec3(0.0, 1.0, 0.0);

        // Get the view matrix from the camera orientation
        mat3 viewMatrix = lookAt(targetDir, up);

        // Transform the ray to point in the correct direction
        rayDir = normalize(viewMatrix * rayDir);

        //---------------------------------------------------
        
        vec3 sunDirection = normalize(vec3(cos(sunLocation), sunHeight, sin(sunLocation)));

        vec3 background = 0.05 * vec3(0.09, 0.33, 0.81);

        float mu = 0.5+0.5*dot(rayDir, sunDirection);
        background += getGlow(1.0-mu, 0.00015, 0.9);

        vec3 totalTransmittance = vec3(1.0);

        float offset = 0.0;

        #ifdef DITHERING
        // Sometimes the blue noise texture is not immediately loaded into iChannel2
        // leading to jitters.
        if(iChannelResolution[2].xy == vec2(1024)){
            // From https://blog.demofox.org/2020/05/10/ray-marching-fog-with-blue-noise/
            float blueNoise = texture(iChannel2, fragCoord / 1024.0).r;
            offset = fract(blueNoise + float(iFrame%32) * goldenRatio);
        }
        #endif

        vec3 col = mainRay(cameraPos, rayDir, sunDirection, totalTransmittance, offset, renderPreview); 
        
        col += background * totalTransmittance;

        // Tonemapping
        col = ACESFilm(col);
        // Gamma
        col = pow(col, vec3(0.4545));

        fragColor = vec4(col, 1.0);
        
    }else{
        vec4 oldData = texelFetch(iChannel1, ivec2(fragCoord), 0);
        fragColor = oldData;
    }
    
    // Store the number of full resolution frames which have been drawn for the current view
    if(fragCoord == vec2(0.5, 0.5)){
        if(renderPreview){
            fragColor = vec4(vec3(0.0), 1.0);
        }else{
            fragColor = vec4(framesDrawn + 1.0, 0.0, 0.0, 1.0);
        }
    }
}