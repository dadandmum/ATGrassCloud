#ifndef __WIND_LIB_HLSL__
#define __WIND_LIB_HLSL__

TEXTURE2D(_WindResultTex);
SAMPLER(sampler_WindResultTex);
// === 2D Simplex Noise in HLSL ===
float wmod289(float x) { return x - floor(x / 289.0) * 289.0; }
float2 wmod289(float2 x) { return x - floor(x / 289.0) * 289.0; }
float3 wmod289(float3 x) { return x - floor(x / 289.0) * 289.0; }
float4 wmod289(float4 x) { return x - floor(x / 289.0) * 289.0; }

float3 wpermute(float3 x) { return wmod289(((x*34.0)+1.0)*x); }


float wsnoise(float2 v)
{
    const float4 C = float4(0.211324865405187,  // (3-sqrt(3))/6
                            0.366025403784439,  // 0.5*(sqrt(3)-1)
                            -0.577350269189626,  // -1+2*(0.5*(sqrt(3)-1))
                            0.024390243902439); // 1/41

    float2 i  = floor(v + dot(v, C.yy));
    float2 x0 = v - i + dot(i, C.xx);
    float2 i1 = (x0.x > x0.y) ? float2(1.0, 0.0) : float2(0.0, 1.0);
    float4 x12 = x0.xyxy + C.xxzz;
    x12.xy -= i1;

    i = wmod289(i);
    float3 p = wpermute(wpermute(i.y + float3(0.0, i1.y, 1.0)) + i.x + float3(0.0, i1.x, 1.0));
    float3 m = max(0.5 - float3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
    m = m*m;
    m = m*m;

    float3 x = 2.0 * frac(p * C.www) - 1.0;
    float3 h = abs(x) - 0.5;
    float3 ox = floor(x + 0.5);
    float3 a0 = x - ox;
    m *= 1.79284291400159 - 0.85373472095314 * (a0*a0 + h*h);
    float3 g;
    g.x = a0.x * x0.x + h.x * x0.y;
    g.yz = a0.yz * x12.xz + h.yz * x12.yw;
    return 1.421104 * dot(m, g);
}
// 计算噪声梯度（中心差分）
float2 noiseGradient(float2 p)
{
    float h = 0.001;
    float center = wsnoise(p);
    float dx = (wsnoise(p + float2(h, 0)) - wsnoise(p - float2(h, 0))) / (2.0 * h);
    float dy = (wsnoise(p + float2(0, h)) - wsnoise(p - float2(0, h))) / (2.0 * h);
    return float2(dx, dy);
}

// 2D Curl Noise: (-dy, dx)
float2 curlNoise(float2 p)
{
    float2 grad = noiseGradient(p);
    return float2(-grad.y, grad.x);
}

half2 windNoiseSimple(float2 worldPosXZ , float windScale, float windSpeed, float2 windDir )
{
    float2 wind = curlNoise(( worldPosXZ + normalize( windDir) * windSpeed * _Time.y) * windScale );
    
    wind *= 10.0;
    return wind;
}

half2 windNoise(float2 worldPosXZ , float windScale, float noiseScale, float flowStrength,  float noiseSpeed , float2 noiseDir, float windSpeed, float2 windDir )
{
    float2 noiseUV = (worldPosXZ  + noiseSpeed * _Time.y * normalize(noiseDir) ) * windScale * noiseScale + 5.17;
    float2 flow = curlNoise(noiseUV) * flowStrength;

    float2 wind = curlNoise(( worldPosXZ + normalize( windDir) * windSpeed * _Time.y) * windScale  + flow );

    wind *= 10.0;
    return wind;

}

half2 WindEncode( half2 wind)
{
    return wind * 0.5 + 0.5;

}

half2 WindDecode( half2 wind)
{
    return wind * 2.0 - 1.0;
}

float2 wind_UV2PosXZ( float2 uv , float4 windPositionParams )
{
    return uv * windPositionParams.z + windPositionParams.xy;
}

float2 wind_PosXZ2UV( float2 posXZ , float4 windPositionParams )
{
    return (posXZ - windPositionParams.xy) * windPositionParams.w;
}

half2 GetWind( float2 worldPosXZ , float4 windPositionParams )
{
    float2 uv = wind_PosXZ2UV(worldPosXZ, windPositionParams);
    float2 windEncode = SAMPLE_TEXTURE2D_LOD(_WindResultTex, sampler_WindResultTex, uv, 0);    


    return WindDecode(windEncode);
}


#endif