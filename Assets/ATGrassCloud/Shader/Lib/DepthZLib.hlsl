#ifndef __DEPTHZLIB_HLSL__
#define __DEPTHZLIB_HLSL__

// #pragma multi_compile_local _REVERSE_Z

Texture2D<float> _DepthFullTex;
SamplerState sampler_DepthFullTex;

float2 _DepthFullSize;

Texture2D<float4> _DepthHiZTex;
SamplerState sampler_DepthHiZTex;

int _DepthHiZSize;
int _DepthHiZMipmapCount;
float3 _DepthHiZCameraPosition;
float4x4 _DepthHiZ_VP;

float depth_GetDepthByPosCS(float4 posCS)
{
    // Transfer grass position from clipping to NDC
    float3 ndcPosition = posCS.xyz / posCS.w;
    return ndcPosition.z;
}


float2 depth_PosCS2UV(float4 posCS)
{
    // Transfer grass position from clipping to NDC
    float3 ndcPosition = posCS.xyz / posCS.w;
    
    // Transfer to uv coordinate
    float2 uvPosition = float2(ndcPosition.x, ndcPosition.y) * 0.5f + 0.5f;
    return uvPosition;
}


float3 depth_WorldPos2UVD(float3 positionWS)
{
    float4 positionCS = mul(_DepthHiZ_VP, float4(positionWS, 1.0));
    float3 uvd = positionCS.xyz / positionCS.w;
    uvd.xy = (uvd.xy + 1) * 0.5;
    //if the positionWS is behind the camera 
    // #if AT_REVERSE_Z || UNITY_REVERSED_Z
    // uvd.z = 1.0 - uvd.z;
    // #endif

    if(uvd.z < 0){
        #if AT_REVERSE_Z || UNITY_REVERSED_Z
        uvd.z = 1;
        #else
        uvd.z = 0;
        #endif
    }
    return uvd;
}


float depth_GetDepthHiZ(float2 uvPosition, int mipLevel)
{
    return _DepthHiZTex.SampleLevel(sampler_DepthHiZTex, uvPosition, mipLevel).r;
}

float depth_GetDepthFull(float2 uvPosition)
{
    return _DepthFullTex.SampleLevel(sampler_DepthFullTex, uvPosition, 0).r;
}

float depth_GetDepthHiZ( float4 posCS , int mipLevel)
{
    float2 uv = depth_PosCS2UV(posCS);
    return depth_GetDepthHiZ(uv, mipLevel);
}

uint depth_GetHizMip(float2 uvSize ){
    float2 size = abs(uvSize) * _DepthHiZSize;
    uint2 mip2 = ceil(log2(size));
    uint mip = clamp(max(mip2.x,mip2.y),1,_DepthHiZMipmapCount - 1);
    return mip;
}

float4 depth_GetDepthHiZ(float4 minPosCS , float4 maxPosCS )
{
    float2 uvMin = depth_PosCS2UV(minPosCS);
    float2 uvMax = depth_PosCS2UV(maxPosCS);
    float2 uvSize = abs(uvMax - uvMin);
    int mipLevel = depth_GetHizMip(uvSize);

    float d1 = depth_GetDepthHiZ(uvMin, mipLevel);
    float d2 = depth_GetDepthHiZ(uvMax, mipLevel);
    float d3 = depth_GetDepthHiZ(float2(uvMin.x,uvMax.y), mipLevel);
    float d4 = depth_GetDepthHiZ(float2(uvMax.x,uvMin.y), mipLevel);

    return float4(d1,d2,d3,d4);
}

bool depth_checkHizCull(float3 posMin , float3 posMax )
{
    float3 uvdMin = depth_WorldPos2UVD(posMin);
    float3 uvdMax = depth_WorldPos2UVD(posMax);
    float4 d = depth_GetDepthHiZ(float4(uvdMin,1.0), float4(uvdMax,1.0));

    float depth = 0;
    #if AT_REVERSE_Z || UNITY_REVERSED_Z
    depth = max( uvdMax.z , uvdMin.z); 
    return d.x > depth && d.y > depth && d.z > depth && d.w > depth;
    #else
    depth = min( uvdMin.z , uvdMax.z);
    return d.x < depth && d.y < depth && d.z < depth && d.w < depth;
    #endif
}


// Calculate linear eye depth from depth buffer value
// _ProjectionParams.x = 1.0 (or -1.0, depending on platform)
// _ProjectionParams.y = Near plane distance
// _ProjectionParams.z = Far plane distance
// _ProjectionParams.w = 1.0 + 1.0 / Far plane distance
float depth_LinearEyeDepth( float depthValue , float near, float far )
{
    float d = depthValue;
#if AT_REVERSE_Z || UNITY_REVERSED_Z
    d = 1.0 - d;
#endif 

    // only work in perspective 
    // float invEyeZ = (1.0 / near - 1.0 / far) * d + 1.0 / far;
    // return 1.0 / invEyeZ;
    return  (2.0 * near * far) / (far + near - d * (far - near) + 1e-6);
}
float depth_LinearEyeDepth(float depthValue , float4 projectionParams )
{
    // Get near and far plane distances
    float near = projectionParams.y;
    float far = projectionParams.z;

    return depth_LinearEyeDepth(depthValue,near,far);
}


float depth_DepthLinear01(float d, float near, float far)
{
    #if AT_REVERSE_Z  || UNITY_REVERSED_Z
        float z01 = 1.0 - d; 
    #else
        float z01 = d;
    #endif

    float linearDepth = (near * far * 2.0) / (far + near - z01 * (far - near) + 1e-6);
    return (linearDepth - near) / (far - near);
}

// x = 1-far/near
// y = far/near
// z = x/far
// w = y/far
// or in case of a reversed depth buffer (UNITY_REVERSED_Z is 1)
// x = -1+far/near
// y = 1
// z = x/far
// w = 1/far

// // zBufferParam = { (f-n)/n, 1, (f-n)/n*f, 1/f }
// float depth_Linear01DepthZ(float depth , float4 zBufferParam )
// {
//     return 1.0 / (zBufferParam.x * depth + zBufferParam.y);
// }

// // zBufferParam = { (f-n)/n, 1, (f-n)/n*f, 1/f }
// float depth_LinearEyeDepthZ(float depth, float4 zBufferParam)
// {
//     return 1.0 / (zBufferParam.z * depth + zBufferParam.w);
// }

float2 depth_WorldPos2UV(float3 positionWS)
{
    float4 positionHS = mul(_DepthHiZ_VP, float4(positionWS, 1.0));
    float2 uv = positionHS.xy / positionHS.w;
    uv = (uv + 1.0) * 0.5;
    return uv;
}


float depth_Depth2WorldDistance(float depthValue , float4 projectionParams)
{
    float linearEyeDepth = depth_LinearEyeDepth(depthValue, projectionParams);
    return linearEyeDepth;

}


#endif 