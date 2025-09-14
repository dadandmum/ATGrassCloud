#ifndef DEPTHZLIB_HLSL
#define DEPTHZLIB_HLSL


Texture2D<float> _DepthHiZTex;
SamplerState sampler_DepthHiZTex;
int _DepthHiZSize;
int _DepthHiZMipmapCount;

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

float depth_GetDepth(float2 uvPosition)
{
    return _DepthHiZTex.SampleLevel(sampler_DepthHiZTex, uvPosition, 0).r;
}

float depth_GetDepthFromDepthTex( float4 posCS )
{
    float2 uv = depth_PosCS2UV(posCS);
    return depth_GetDepth(uv);

}




#endif 