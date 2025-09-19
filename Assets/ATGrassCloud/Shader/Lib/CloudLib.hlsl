#ifndef CLOUDLIB_HLSL
#define CLOUDLIB_HLSL

#include "Assets/ATGrassCloud/Shader/Lib/QuaternionLib.hlsl"
#include "Assets/ATGrassCloud/Shader/Lib/SDFLib.hlsl"



struct CloudObjectData
{
    float3 position;
    float4 quat;
    float3 scale;
    float type;
    float boundRadius;
    float4 param;
};


CBUFFER_START(UnityPerMaterial)

float _CloudDensityMultiplier;
float _CloudDensityByDistance;
float _CloudDensityMax;
float _CloudVolumeOffset;
float _CloudDensityOffset;


float _MaxRaymarchSteps;
float _RaymarchRange;
float _RaymarchStep;
float _RaymarchNoiseOffset;

float _DetailNoiseScale;
float _DetailNoiseMultiplier;
float3 _DetailNoiseWeights;
float3 _NoiseVelocity;

StructuredBuffer<CloudObjectData> _CloudObjectBuffer;
int _CloudObjectCount;

CBUFFER_END


TEXTURE2D(_BlueNoise);
SAMPLER(sampler_BlueNoise);

TEXTURE3D(_NoiseTex);
SAMPLER(sampler_NoiseTex);

/**
 * @brief Transforms a world-space point into the local (object) space
 *        of an object defined by position, rotation (quaternion), and scale.
 * 
 * Process:
 * 1. Translate: worldPos - objectPos
 * 2. Rotate:  Apply inverse rotation (conjugate quaternion)
 * 3. Scale:   Divide by object scale (component-wise)
 *
 * @param worldPos    Point in world space
 * @param objectPos   Object's world position
 * @param objectRot   Object's rotation as quaternion (x,y,z,w)
 * @param objectScale Object's scale vector (sx, sy, sz)
 * @return The point in object's local space
 */
float3 WorldToModelSpace(
    float3 worldPos,
    float3 objectPos,
    float4 objectRot,
    float3 objectScale)
{
    // Step 1: Inverse Translation
    // Move point relative to object origin
    float3 offset = worldPos - objectPos;

    // Step 2: Inverse Rotation
    // Rotate the offset vector by the inverse (conjugate) of the object's rotation
    float4 qInv = quatConjugate(objectRot);
    float3 rotated = quatRotate(qInv, offset);

    // Step 3: Inverse Scaling
    // Divide by scale to cancel object's scale (prevent divide-by-zero)
    float3 epsilon = float3(1e-6, 1e-6, 1e-6);           // Small value to avoid division by zero
    float3 scaleSafe = max(abs(objectScale), epsilon);   // Clamp scale to minimum safe value
    float3 localPos = rotated / scaleSafe;               // Apply inverse scale

    return localPos;
}
/**
 * @brief Computes the distance from the ray origin to the first intersection with a sphere.
 *
 * This function analytically solves for the intersection between a ray and a sphere
 * in world space. The ray is defined by an origin point and a normalized direction vector.
 * The sphere is defined by its center position and radius.
 *
 * @param posWS    Ray origin in world space.
 * @param viewDir  Ray direction in world space (assumed to be normalized).
 * @param objPOS   Sphere center position in world space.
 * @param radius   Sphere radius (must be positive).
 *
 * @return
 *   - A positive float: distance to the first intersection point on the sphere surface.
 *   - -1.0f: if the ray origin is inside the sphere.
 *   - 1e30f (approximates INF): if the ray does not intersect the sphere.
 *
 * @note
 *   - This function assumes `viewDir` is normalized. If not, results will be incorrect.
 *   - Uses a quadratic equation solver approach for exact analytical intersection.
 *   - Small epsilon (1e-5f) is used to avoid self-intersection due to floating-point precision.
 *   - If the ray starts inside the sphere, returns -1 regardless of direction.
 */
float RaySphereIntersect(float3 posWS, float3 viewDir, float3 objCenter, float radius)
{
    // Vector from sphere center to ray origin
    float3 L = posWS - objCenter;
    
    // Ray equation: P(t) = posWS + t * viewDir
    // Sphere equation: |P(t) - objCenter|^2 = radius^2
    // Substituting gives a quadratic in t: t^2 * |viewDir|^2 + 2*t*(L 路 viewDir) + |L|^2 - radius^2 = 0
    
    // Assuming viewDir is normalized (typical for camera rays)
    // So |viewDir|^2 = 1.0
    float a = 1.0f;
    float b = 2.0f * dot(L, viewDir);
    float c = dot(L, L) - radius * radius;
    
    float discriminant = b * b - 4.0f * a * c;
    
    // No real roots 鈫?no intersection
    if (discriminant < 0.0f)
    {
        return 1e30f; // Return INF (no hit)
    }
    
    // Compute the two intersection points
    float sqrtD = sqrt(discriminant);
    float t1 = (-b - sqrtD) * 0.5f; // Closer intersection
    float t2 = (-b + sqrtD) * 0.5f; // Farther intersection
    
    // Check if ray origin is inside the sphere
    float distToCenter = length(L);
    if (distToCenter < radius)
    {
        return -1.0f; // Inside the sphere
    }
    
    // Origin is outside the sphere: find the first valid forward intersection
    if (t1 > 1e-5f) // Avoid self-intersection due to floating-point error
    {
        return t1; // Hit the front surface
    }
    else if (t2 > 1e-5f)
    {
        return t2; // Ray passes through the back (e.g., entering from behind)
    }
    else
    {
        // Both intersections are behind the ray origin 鈫?no hit
        return 1e30f; // Return INF
    }
}

float InOutEaseCubic( float x )
{
    return x * x * ( 3.0 - 2.0 * x );
}

float cloud_GetDistanceFade( float3 worldPos , float3 camPos, float4 cascadeRange )
{
    float d = distance(worldPos.xz, camPos.xz);
    float innerRange = cascadeRange.x;
    float outterRange = cascadeRange.y;
    float innerRangeFadeInv = cascadeRange.z;
    float innerRangeX = InOutEaseCubic(saturate( 1.0 - ( innerRange - d ) * innerRangeFadeInv));
    float outterRangeFade = cascadeRange.w;
    float outterRangeX = InOutEaseCubic(saturate( 1.0 - ( d - outterRange ) * outterRangeFade));
    return innerRangeX * outterRangeX;
}

float GetCloudObjectSurfaceDistance( float3 posWS , float3 viewDir , float maxDistance  )
{
    float distance = maxDistance;

    for ( int i = 0 ; i < _CloudObjectCount ; i++ )
    {
        CloudObjectData cloudObject = _CloudObjectBuffer[i];
        float3 cloudPos = cloudObject.position;
        float boundRadius = cloudObject.boundRadius;

        float distanceToBoundSphere = RaySphereIntersect(posWS, viewDir, cloudPos, boundRadius);
       
        distance = min( distance , distanceToBoundSphere );
    }

    return distance;
}

float SampleCloudObject( float3 posWS , float maxDistance )
{
    float distance = maxDistance;

    for ( int i = 0 ; i < _CloudObjectCount ; i++ )
    {
        CloudObjectData cloudObject = _CloudObjectBuffer[i];
        float3 cloudPos = cloudObject.position;
        float4 cloudQuat = cloudObject.quat;
        float3 cloudScale = cloudObject.scale;
        float type = cloudObject.type;
        float boundRadius = cloudObject.boundRadius;
        float4 param = cloudObject.param;

        float3 localPos = WorldToModelSpace(posWS, cloudPos, cloudQuat, cloudScale);

        float sdDis = 999999.0;
        if ( type == 1.0 ) // sphere 
        {
            sdDis = sdSphere( localPos , param.x ); // x => radius 
        }else if ( type == 2.0 ) // box
        {
            sdDis = sdBox( localPos , param.x ); // x => length 
        }
       
        distance = min( distance , sdDis );
    }

    return distance;
}

float2 SampleDensityCloudObject( float3 posWS , float maxDistance )
{
    float distance = SampleCloudObject( posWS  , maxDistance );
    distance = max( -distance , 0.0 );
    float density = _CloudVolumeOffset * ( min( _CloudDensityMax , distance * _CloudDensityByDistance )) ;
    float fixedDensity = density +  _CloudDensityOffset * 0.05;

    return float2( fixedDensity , density );
}


float SampleDensityWithNoise( float3 posWS  , float maxDistance , float4 cascadeRange )
{   
    float2 cloudShapeDensity = SampleDensityCloudObject( posWS , maxDistance  );

    float time = _Time.y;
    float3 noisePos = ( posWS + time * _NoiseVelocity) * exp( _DetailNoiseScale ) * 0.1f;

    float4 noise = SAMPLE_TEXTURE3D_LOD( _NoiseTex , sampler_NoiseTex , noisePos , 0 );
    float noiseFBM = dot(noise, normalize(_DetailNoiseWeights)) ;

    float density = cloudShapeDensity.x - noiseFBM * pow( 1.0 - cloudShapeDensity.y , 3.0 ) * _DetailNoiseMultiplier;
    
    density = max( density , 0.0 ); 
    return density * _CloudDensityMultiplier * 0.1;
}

// Used to scale the blue-noise to fit the view
float2 scaleUV(float2 uv, float scale) {
    float x = uv.x * _ScreenParams.x;
    float y = uv.y * _ScreenParams.y;
    return float2 (x,y)/scale;
}

float GetBlueNoise( float2 uv )
{
    float noise = SAMPLE_TEXTURE2D_LOD( _BlueNoise, sampler_BlueNoise, scaleUV(uv, 96) , 0 ).r;
    return noise;
}


float4 cloud_Raymarch( float3 origin , float3 dir , float2 uv , float3 lighting, float startDistance , float maxDistance , float4 cascadeRange)
{
    float distance = startDistance;
    float totalDensity = 0.0;
    float4 color = float4( 0.0 , 0.0 , 0.0 , 0.0 ); 
    float noise = GetBlueNoise(uv + _Time.yy * 0.5);
    distance += ( noise - 0.5 ) * _RaymarchNoiseOffset;

    float rayStep = _RaymarchStep;

    float stepCount = min( _MaxRaymarchSteps , (int)(( maxDistance - startDistance ) / _RaymarchStep  ));

    // if ( stepCount < _MaxRaymarchSteps * 0.25 )
    // {
    //     stepCount = _MaxRaymarchSteps * 0.25;
    //     rayStep = ( maxDistance - startDistance ) / stepCount;
    // }

    for ( float i = 0 ; i < stepCount ; i++ )
    {
        float3 posWS = origin + dir * distance;

        float density = SampleDensityWithNoise( posWS , maxDistance , cascadeRange);
        float cascadeFade = cloud_GetDistanceFade( posWS , origin , cascadeRange );
        density *= cascadeFade;

        totalDensity += density * rayStep;
        color.rgb += lighting.rgb * density;
        distance += rayStep;
    }

    return float4( color.rgb , totalDensity );
}

// With out cascade Range
float4 cloud_RaymarchSim( float3 origin , float3 dir , float2 uv , float3 lighting, float startDistance , float maxDistance )
{
    return cloud_Raymarch( origin , dir , uv , lighting, startDistance , maxDistance , float4( 0.0 , 99999.0 , 0.001 , 0.001 ));
}


#endif 