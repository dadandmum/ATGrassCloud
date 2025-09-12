#ifndef __HEIGHT_MAP_LIB_HLSL__
#define __HEIGHT_MAP_LIB_HLSL__


#define MIN_HEIGHT -10.0
#define MAX_HEIGHT 300.0

#define HEIGHT_ENCODE_REMAP 0
#define HEIGHT_ENCODE_LOG 1

float HRemap(float In, float2 InMinMax, float2 OutMinMax)
{
    return OutMinMax.x + (In - InMinMax.x) * (OutMinMax.y - OutMinMax.x) / (InMinMax.y - InMinMax.x);
}

float EncodeHeight( float height, float2 bounds)
{
    // bounds.x = min, bounds.y = max
    float minH = max( MIN_HEIGHT, bounds.x);
    float maxH = min( MAX_HEIGHT, bounds.y);

#if HEIGHT_ENCODE_REMAP
    return HRemap(height, bounds, float2(0, 1));
#elif HEIGHT_ENCODE_LOG
    // 将 height 平移到从 0 开始
    float h = max( 0 , height - minH);
    
    float range = maxH - minH;
    // 防止 log(0)
    h = max(h, 1e-6); // 避免除零或对数无穷
    
    float efficiency = 10.0;
    // 使用对数映射：越接近 0 越密集（高精度）
    // log(1 + x) 在 x=0 附近变化快，适合提升低值精度
    float encoded = log( 1.0 + (h / range) * efficiency ) / log( 1.0 + efficiency);
    
    // clamp to [0,1]
    return saturate(encoded);
#else
    return height * 0.1;
#endif
}

float DecodeHeight( float encoded, float2 bounds)
{
    // bounds.x = min, bounds.y = max
    float minH = max( MIN_HEIGHT, bounds.x);
    float maxH = min( MAX_HEIGHT, bounds.y);

#if HEIGHT_ENCODE_REMAP
    return HRemap(encoded, float2(0, 1), bounds);
#elif HEIGHT_ENCODE_LOG
    float efficiency = 10.0;
    // 反映射：将编码值转换回原始高度
    float range = maxH - minH;
    float h = ( pow( 1.0 + efficiency, encoded ) - 1.0 ) / efficiency * range;
    
    // 恢复原始高度
    return h + minH;
#else
    return encoded * 10.0;
#endif
}


#endif