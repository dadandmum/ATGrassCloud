#ifndef DEPTHZLIB_HLSL
#define DEPTHZLIB_HLSL


TEXTURE2D(_DepthHiZTex);
SAMPLER(sampler_DepthHiZTex);

int _DepthHiZSize;
int _DepthHiZMipmapCount;

TEXTURE2D(_DepthFullTex);
SAMPLER(sampler_DepthFullTex);

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


float depth_GetDepthFull(float2 uvPosition)
{
    return _DepthFullTex.SampleLevel(sampler_DepthFullTex, uvPosition, 0).r;
}

float depth_GetDepthFromDepthTex( float4 posCS )
{
    float2 uv = depth_PosCS2UV(posCS);
    return depth_GetDepth(uv);

}

// 从Unity内置变量获取投影参数
// _ProjectionParams.x = 1.0 (或-1.0，取决于平台)
// _ProjectionParams.y = 近平面距离
// _ProjectionParams.z = 远平面距离
// _ProjectionParams.w = 1.0 + 1.0/远平面距离

float depth_LinearEyeDepth(float depthValue)
{
    // 获取近平面和远平面距离
    float near = _ProjectionParams.y;
    float far = _ProjectionParams.z;
    
    // 判断当前投影类型（透视/正交）
    // 透视投影矩阵的[2][3]元素为-1，正交为0
    bool isPerspective = unity_OrthoParams.w == 0;
    
    if (isPerspective)
    {
        // 透视投影的深度转换
        // 推导公式: 1/eyeZ = (1/near - 1/far) * depthValue + 1/far
        float invEyeZ = (1.0 / near - 1.0 / far) * depthValue + 1.0 / far;
        return 1.0 / invEyeZ;
    }
    else
    {
        // 正交投影的深度转换（线性关系）
        // 深度值直接与距离成线性比例
        return depthValue * (far - near) + near;
    }
}


float depth_Depth2WorldDistance(float depth)
{
    float linearEyeDepth = depth_LinearEyeDepth(depth);
    return linearEyeDepth;

}



#endif 