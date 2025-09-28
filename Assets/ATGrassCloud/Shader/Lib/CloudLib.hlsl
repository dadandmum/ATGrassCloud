#ifndef CLOUDLIB_HLSL
#define CLOUDLIB_HLSL

#include "Assets/ATGrassCloud/Shader/Lib/QuaternionLib.hlsl"
#include "Assets/ATGrassCloud/Shader/Lib/SDFLib.hlsl"
#include "Assets/ATGrassCloud/Shader/Lib/Tonemap.hlsl"
#include "Assets/ATGrassCloud/Shader/Lib/ATGI.hlsl"
#include "Assets/ATGrassCloud/Shader/Lib/CloudUtil.hlsl"



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

// RayMarch
float _MaxRaymarchStepCount;
float _RaymarchRange;
float _RaymarchStep;
float _RaymarchNoiseOffset;

// LightMarch
float _MaxLightmarchStepCount;
float _LightmarchRange;
float _LightmarchStep;
float _LightmarchNoiseOffset;

// Short Light March
float _MaxShortLightmarchStepCount;
float _ShortLightmarchRange;
float _ShortLightmarchStep;
float _MaxMultipleScatteringStepCount;
float _MaxMultipleScatteringSampleCount;
float _MultipleScatteringRange;
float _MultipleScatteringStep;

float _DetailNoiseScale;
float _DetailNoiseMultiplier;
float4 _DetailNoiseWeights;
float3 _NoiseVelocity;
float _DetailShapeNoiseInfluenceExtend;
float _DetailShapeNoiseInfluenceFade;
float _NoiseOffset;

// Lighting Intensity
float _Brightness;
float _DirectLightingIntensity;
float _ShortLightingIntensity;
float _MultipleScatterIntensity;

// Beer Parameters
float _InAbsorption;
float _InTransmitPower;
float _InTransmitThreshold;
float _OutAbsorption;
float _OutTransmitPower;
float _OutTransmitThreshold;
float _ShortOutTransmitThreshold;
float _ShortOutTransmitPower;
float _ShortOutAbsorption;
float _MSBeerAbsorption;

// Powder Parameters
float _PowderAbsorption;

// HenyeyGreenstein Parameters
float _ForwardScatter;
float _BackwardScatter;
float _HGScatterMultiplier;

float4 _AmbientColor;
float _AmbientPower;

StructuredBuffer<CloudObjectData> _CloudObjectBuffer;
int _CloudObjectCount;

float4 _ScatterDirs[20];

float _DebugRate;
int   _DebugMode;

CBUFFER_END


TEXTURE2D(_BlueNoise);
SAMPLER(sampler_BlueNoise);

TEXTURE3D(_NoiseTex);
SAMPLER(sampler_NoiseTex);

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
        float4 cloudQuat = cloudObject.quat;
        float3 cloudScale = cloudObject.scale;
        float type = cloudObject.type;
        float boundRadius = cloudObject.boundRadius;
        float4 param = cloudObject.param;

        float3 localPos = WorldToModelSpace(posWS, cloudPos, cloudQuat, cloudScale);
        float3 localView = WorldToModelDir(viewDir, cloudQuat, cloudScale);

        if ( type == 1.0 ) // sphere 
        {
            float3 interectPoint = rayIntersectSphere(localPos, localView, param.x); // x => radius 
            if ( interectPoint.x < 1e30 )
            {
                float3 worldInterectPoint = ModelToWorldSpace(interectPoint, cloudPos, cloudQuat, cloudScale);
                distance = min( distance , length( worldInterectPoint - posWS ));
            }
        }else if ( type == 2.0 ) // box 
        {
            float3 interectPoint = rayIntersectBox(localPos, localView, param.x); // x => length 
            if ( interectPoint.x < 1e30 )
            {
                float3 worldInterectPoint = ModelToWorldSpace(interectPoint, cloudPos, cloudQuat, cloudScale);
                distance = min( distance , length( worldInterectPoint - posWS ));
            }
        }else if ( type == 3.0 ) // capsule
        {
            float3 interectPoint = rayIntersectCapsule(localPos, localView, param.x, param.y); // x => length , y => radius
            if ( interectPoint.x < 1e30 )
            {
                float3 worldInterectPoint = ModelToWorldSpace(interectPoint, cloudPos, cloudQuat, cloudScale);
                distance = min( distance , length( worldInterectPoint - posWS ));
            }
        }else if ( type == 4.0 ) // cylinder
        {
            float3 interectPoint = rayIntersectCylinder(localPos, localView, param.x, param.y); // x => height , y => radius
            if ( interectPoint.x < 1e30 )
            {
                float3 worldInterectPoint = ModelToWorldSpace(interectPoint, cloudPos, cloudQuat, cloudScale);
                distance = min( distance , length( worldInterectPoint - posWS ));
            }
        }
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
        }else if ( type == 3.0 ) // capsule
        {
            sdDis = sdCapsule( localPos , param.x , param.y ); // x => length , y => radius
        }else if ( type == 4.0 ) // cylinder
        {
            sdDis = sdCylinder( localPos , param.x , param.y ); // x => height , y => radius
        }
       
        distance = min( distance , sdDis );
    }

    return distance;
}

float GetFinalDensity( float density )
{
    return ( min( _CloudDensityMax , density ) + _CloudDensityOffset) * _CloudVolumeOffset ;

}

float3 SampleDensityCloudObject( float3 posWS , float maxDistance )
{
    float distance = SampleCloudObject( posWS  , maxDistance );
    distance = max( -distance , 0.0 );
    float pureDensity = distance * _CloudDensityByDistance;
    float clampedDensity =( min( _CloudDensityMax , pureDensity )) ;
    float finalDensity = GetFinalDensity( pureDensity);

    return float3( finalDensity , clampedDensity , pureDensity );
}



float3 GetNoisePositionUVW( float3 posWS )
{
    return ( posWS + _Time.y * _NoiseVelocity) * _DetailNoiseScale ;
}

float GetNoiseDensity( float4 noiseSample , float pureCloudDensity )
{
    float noiseFBM = dot(noiseSample, normalize(_DetailNoiseWeights)) + _NoiseOffset;

    float noiseDensity = noiseFBM * _DetailNoiseMultiplier 
    * ( 1.0 - exp( - pureCloudDensity * _DetailShapeNoiseInfluenceExtend ))
    * exp( - pureCloudDensity * _DetailShapeNoiseInfluenceFade );

    return noiseDensity;
}

float SampleDensityNoise( float3 posWS , float maxDistance )
{
    float3 cloudDensityResult = SampleDensityCloudObject( posWS , maxDistance  );
    float3 noisePos = GetNoisePositionUVW( posWS );

    float4 noise = SAMPLE_TEXTURE3D_LOD( _NoiseTex , sampler_NoiseTex , noisePos , 0 );
    float pureDensity = cloudDensityResult.z;
    float noiseDensity =  GetNoiseDensity( noise , pureDensity );

    return noiseDensity;

}

float SampleDensityWithNoise( float3 posWS  , float maxDistance , float cascadeFade  )
{   
    float3 cloudDensityResult = SampleDensityCloudObject( posWS , maxDistance  );

    float3 noisePos = GetNoisePositionUVW( posWS );

    float4 noise = SAMPLE_TEXTURE3D_LOD( _NoiseTex , sampler_NoiseTex , noisePos , 0 );
    float noiseFBM = dot(noise, normalize(_DetailNoiseWeights)) + _NoiseOffset;

    float pureDensity = cloudDensityResult.z;
    float noiseDensity = GetNoiseDensity( noise , pureDensity );

    float finalPure = pureDensity + noiseDensity;
    float finalDensity = GetFinalDensity( finalPure );

    finalDensity *= cascadeFade;

    finalDensity = max( finalDensity , 0.00001 ); 

    return finalDensity * _CloudDensityMultiplier * 0.1;
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

// input t : [0,1]
// output : [0,1]
float powerScale( float t , float power )
{
    if ( t > 0.5 )
    {
        return pow( saturate(( t - 0.5 ) * 2.0) , power ) * 0.5 + 0.5;
    }else {
        return - pow( saturate(- ( t - 0.5 ) * 2.0) , power ) * 0.5 + 0.5;

    }
}


float HenyeyGreenstein(float g, float angle) {
    float gg = g * g;
	return (1.0f - gg) / (4.0f * 3.14159 * pow( max( 0 , 1 + gg - 2.0f * g * angle) , 1.5f));
}

float beer(float d) {
    return exp(-d);
}

float powder(float d ) {
    return 1.0 - exp(- ( d * d));
}

float hgScatter(float angle , float3 lightDir) {
    
    float scatterAverage = (HenyeyGreenstein(_ForwardScatter, angle) + HenyeyGreenstein(-_BackwardScatter, angle)) / 2.0f;
    
    // Scale the brightness by sun position
    float sunPosModifier = 1.0;
    if (lightDir.y < 0) {
        sunPosModifier = pow(lightDir.y + 1,3);
    }


    return sunPosModifier + scatterAverage * _HGScatterMultiplier;
}

float hgScatter( float3 view , float3 lightDir ) {

    float angle = dot(view, lightDir);
    return hgScatter(angle, lightDir);
}


float hgScatterPure( float3 view , float3 lightDir ) {
    
    float angle = dot(view, lightDir);
    float scatterAverage = (HenyeyGreenstein(_ForwardScatter, angle) + HenyeyGreenstein(-_BackwardScatter, angle)) / 2.0f;
    return scatterAverage;
}

float cloud_Lightmarch(float3 posWS , float3 lightDir, float maxDistance , float cascadeFade) {
    float stepSize = _LightmarchStep;
    float density = 0;

    float noise = GetBlueNoise( float2( posWS.x , posWS.y + posWS.z ));
    float noiseOffset = ( noise - 0.5 ) * _LightmarchNoiseOffset;
    posWS += lightDir * noiseOffset;

    // directional lighting 
    for (int i = 0; i < _MaxLightmarchStepCount; i++) {
        posWS += lightDir * stepSize;
        density += max(0, SampleDensityCloudObject(posWS, maxDistance).x * stepSize);
    }

    float transmit = beer(density * _OutAbsorption) * powder( density * _PowderAbsorption ) ;
    transmit = pow(transmit , _OutTransmitPower);
    transmit = lerp(transmit, 1.0, _OutTransmitThreshold);
    return transmit;
}


float cloud_shortLightmarch(float3 posWS , float3 lightDir, float maxDistance , float cascadeFade) {
    float stepSize = _ShortLightmarchStep;

    float density = 0;

    // directional lighting 
    for (int i = 0; i < _MaxShortLightmarchStepCount; i++) {
        posWS += lightDir * stepSize;
        density += max(0, SampleDensityWithNoise(posWS, maxDistance, cascadeFade).x * stepSize);
    }

    float transmit = beer(density * _ShortOutAbsorption) * powder( density * _PowderAbsorption ) ;
    transmit = pow(transmit , _ShortOutTransmitPower);
    transmit = lerp(transmit, 1.0, _ShortOutTransmitThreshold);
    return transmit;
}



float3 cloud_MultipleScattering( float3 posWS , float3 viewDir, float maxDistance )
{
    float sampleCount = _MaxMultipleScatteringSampleCount;
    float stepCount = _MaxMultipleScatteringStepCount;
    
    float stepSize = _MultipleScatteringStep;
    float3 scattering  = float3(0,0,0);

    float3 forward = viewDir;
    float3 right = cross( forward , float3(0,1,0) );
    float3 up = cross( right , forward );

    uint scatterIndex = 0;
    for( float k = 0 ; k < stepCount ; k = k + 1.0f )
    {
        float stepDistance = stepSize * k;
        for (float i = 0; i < sampleCount ; i = i + 1.0f ) {
            
            float3 scatterDir = _ScatterDirs[scatterIndex].xyz;
            scatterIndex = (scatterIndex + 1) % 20;
            float3 sampleDir = normalize( scatterDir.x * right + scatterDir.y * up + scatterDir.z * forward);

            float3 pos = posWS + sampleDir * stepDistance;
            float density = max(0, SampleDensityCloudObject(pos, maxDistance).x * stepDistance);
            scattering += beer(density * _MSBeerAbsorption) * ATGI_SampleSHbyPosWS( pos , sampleDir ) * hgScatterPure(viewDir, sampleDir);
        }
    }

    return scattering / sampleCount  /  stepCount ;
}


float4 cloud_Raymarch( float3 origin , float3 dir , float2 uv , float3 lighting, float3 lightDir, float startDistance , float maxDistance , float4 cascadeRange)
{
    float distance = startDistance;
    float noise = GetBlueNoise(uv + _Time.yy * 0.5);
    distance += ( noise - 0.5 ) * _RaymarchNoiseOffset;

    float rayStep = _RaymarchStep;

    float stepCount = min( _MaxRaymarchStepCount , (int)(( maxDistance - startDistance ) / _RaymarchStep ));

    float transmit = 1.0;
    float3 illumination = float3( 0.0 , 0.0 , 0.0 );

    float scatter = _Brightness * hgScatter( dot(lightDir, dir) , lightDir);
    float totalDistance = 0.0;

    float rayCount = 0.0;
    float totalDensity = 0.0; 
    float totalNoiseDensity = 0.0;
    float3 debugIllumi = float3( 0.0 , 0.0 , 0.0 );

    for ( float i = 0 ; i < stepCount ; i++ )
    {
        float3 posWS = origin + dir * distance;


        float cascadeFade = cloud_GetDistanceFade( posWS , origin , cascadeRange );
        float density = SampleDensityWithNoise( posWS , maxDistance , cascadeFade);
        
        density *= _RaymarchStep;
        totalDistance += _RaymarchStep;

#ifdef _DEBUG_CLOUD
        rayCount = i / _MaxRaymarchStepCount;
        
        if ( _DebugMode == 4 )    
        {
            float nowPosDensity  = SampleDensityCloudObject(posWS, maxDistance).z + SampleDensityNoise( posWS , maxDistance );
            nowPosDensity = min( _CloudDensityMax , nowPosDensity ) * _RaymarchStep;
            totalDensity += nowPosDensity;
        }

        if ( _DebugMode == 5 )
        {
            totalNoiseDensity += SampleDensityNoise( posWS , maxDistance ) * _RaymarchStep;
        }
#endif

        if ( density > 0.0001)
        {
#if _DEBUG_CLOUD
            if ( _DebugMode == 6 )
            {
                float3 longLighting = cloud_Lightmarch( posWS , lightDir, maxDistance , cascadeFade ) * lighting * scatter ;
                debugIllumi += transmit * density * longLighting * _DirectLightingIntensity;
            }else if ( _DebugMode == 7 )
            {
                float3 shortLighting = cloud_shortLightmarch( posWS , lightDir, maxDistance , cascadeFade ) * lighting * scatter;
                debugIllumi += transmit * density * shortLighting * _ShortLightingIntensity;
            }else if ( _DebugMode == 8 )
            {
                float3 longLighting = cloud_Lightmarch( posWS , lightDir, maxDistance , cascadeFade ) * lighting * scatter ;
                float3 shortLighting = cloud_shortLightmarch( posWS , lightDir, maxDistance , cascadeFade ) * lighting * scatter;
                
                debugIllumi += transmit * density * longLighting * _DirectLightingIntensity * shortLighting * _ShortLightingIntensity;
            }else if ( _DebugMode == 9)
            {
                float3 multipleScattering = cloud_MultipleScattering( posWS , dir, maxDistance ) ;
                debugIllumi += transmit * density * multipleScattering * _MultipleScatterIntensity;
            }else if ( _DebugMode == 10 )
            {
                float3 ambient = _AmbientColor.xyz * _AmbientPower;
                debugIllumi += transmit * density * ambient;
            }

#else 

            
            float3 longLighting = cloud_Lightmarch( posWS , lightDir, maxDistance , cascadeFade ) * lighting * scatter;
            float3 shortLighting = 1.0f;
            if ( _MaxShortLightmarchStepCount > 0 )
            {
                shortLighting = cloud_shortLightmarch( posWS , lightDir, maxDistance , cascadeFade ) * lighting * scatter;
            }
            float3 directionalLighting = _DirectLightingIntensity * longLighting * _ShortLightingIntensity * shortLighting;
            float3 multipleScattering = float3(0,0,0);
            if ( _MaxMultipleScatteringSampleCount > 0 ) 
            {
                multipleScattering = cloud_MultipleScattering( posWS , dir, maxDistance ) * _MultipleScatterIntensity;
            }
            float3 ambient = _AmbientColor.xyz * _AmbientPower;
            // illumination += transmit * density * ( directionalLighting + multipleScattering + _AmbientColor * beer( density * _AmbientPower * 20.0 ) );
            illumination += transmit * density * ( directionalLighting + multipleScattering + ambient );

#endif 
            // illumination += transmit * density * ( _AmbientColor);

            float localTransmit = beer(density * _InAbsorption) ;
            localTransmit = powerScale(localTransmit, _InTransmitPower);
            localTransmit = lerp(localTransmit, 1.0, _InTransmitThreshold);
            transmit *= localTransmit;

            rayStep = _RaymarchStep;


        } else {
            rayStep *= 2.0;
        }

        if ( transmit < 0.001f || distance > maxDistance)
        {
            break;
        }

        distance += rayStep;

    }

    float3 finalColor = RomBinDaHouseToneMapping( illumination );
    
    // finalColor = illumination;
#ifdef _DEBUG_CLOUD 
    if ( _DebugMode == 1 )
    {
        float startRay = startDistance + ( noise - 0.5 ) * _RaymarchNoiseOffset;
        return float4( lerp( finalColor , float3( 0 ,  startRay / _ProjectionParams.z * 100.0 , 0 ) , _DebugRate) , lerp( 1.0 - transmit , 1.0 , _DebugRate) );
    }
    if ( _DebugMode == 2 )
    {
        return float4( lerp( finalColor , float3( rayCount, 0 ,0 ) , _DebugRate)  , lerp( 1.0 - transmit , 1.0 , _DebugRate) );
    }

    if ( _DebugMode == 3 )
    {
        return float4( lerp( finalColor , float3( 0 , 0 , 1.0 ) * (1.0 - transmit) , _DebugRate) , lerp( 1.0 - transmit , 1.0 , _DebugRate) );
    }
    if ( _DebugMode == 4 )
    {
        return float4( lerp( finalColor , float3( 0 , 1.0 ,0 ) * (totalDensity / totalDistance / _CloudDensityMax), _DebugRate)  , lerp( 1.0 - transmit , 1.0 , _DebugRate) );
    }
    if ( _DebugMode == 5 )
    {
        return float4( lerp( finalColor , float3( 0 , 1.0 ,0 ) * ( totalNoiseDensity / totalDistance / _CloudDensityMax) , _DebugRate)  , lerp( 1.0 - transmit , 1.0 , _DebugRate) );
    }
    if ( _DebugMode == 6 || _DebugMode == 7 || _DebugMode == 8 || _DebugMode == 9 || _DebugMode == 10 )
    {
        return float4( lerp( finalColor , debugIllumi , _DebugRate) , 1.0 - transmit );
    }
    
#endif

    return float4( finalColor , 1.0 - transmit );
}

// With out cascade Range
float4 cloud_RaymarchSim( float3 origin , float3 dir , float2 uv , float3 lighting, float3 lightDir, float startDistance , float maxDistance )
{
    return cloud_Raymarch( origin , dir , uv , lighting, lightDir, startDistance , maxDistance , float4( 0.0 , 99999.0 , 0.001 , 0.001 ));
}


#endif 