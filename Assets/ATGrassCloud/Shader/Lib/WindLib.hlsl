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
float4 wpermute(float4 x) { return wmod289(((x*34.0)+1.0)*x); }


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

float wsnoise(float3 p) {
    float3 a = floor(p);
    float3 d = p - a;
    d = d * d * (3.0 - 2.0 * d); // Fade curve

    // Simplex cell identities
    float4 u = float4(a.z, a.z, a.x, a.x);
    float4 v = float4(a.y, a.x, a.y, a.x);
    float4 ww = float4(1.0, 1.0, 0.0, 0.0);
    float4 gg = float4(0.0, 0.0, 1.0, 1.0);
    float4 idx = a.x + a.y + a.z;
    idx = wmod289(idx);

    // Gradient hashing
    float4 px = wmod289(idx + u);
    float4 py = wmod289(px + v);
    float4 pz = wmod289(py + ww);
    float4 pp = wpermute(wpermute(wpermute(px) + py) + pz);

    // Gradients: 7x7x6 points on a cube, mapped into 3D via Fibonacci projection
    float4 grad = wmod289(pp);
    float4 gradIndices = grad * (1.0 / 41.0); // 41 is period of permutation
    float4 hash = gradIndices - floor(gradIndices); // fractional part
    float4 gi = hash * 7.0;
    float4 gx = floor(gi) * (1.0 / 7.0);
    float4 gy = floor(gi - gx * 7.0) * (1.0 / 6.0);
    float4 gz = frac(gi * (1.0 / 7.0));
    gx = frac(gx);
    gy = frac(gy);

    // Fibonacci projection to get unit vectors
    float4 g00 = float4( 0.8660254, 0.0000000, 0.5000000, 0.0); // ( √3/2, 0, 1/2 )
    float4 g10 = float4( 0.2886751, 0.8164966, 0.5000000, 0.0);
    float4 g20 = float4(-0.2886751, 0.8164966, 0.5000000, 0.0);
    float4 g30 = float4(-0.8660254, 0.0000000, 0.5000000, 0.0);
    float4 g01 = float4(-0.2886751,-0.8164966, 0.5000000, 0.0);
    float4 g11 = float4( 0.2886751,-0.8164966, 0.5000000, 0.0);
    float4 g21 = float4( 0.8660254, 0.0000000,-0.5000000, 0.0);
    float4 g31 = float4( 0.2886751, 0.8164966,-0.5000000, 0.0);

    float4 gxv = lerp(lerp(g00, g10, gx), lerp(g20, g30, gx), step(0.5, gx));
    float4 gyv = lerp(lerp(g01, g11, gx), lerp(g21, g31, gx), step(0.5, gx));
    float4 gv = lerp(gxv, gyv, step(0.5, gy));

    float3 g = gv.xyz;

    // Compute noise contribution from four corners
    float3 r = 1.0 - d;
    float3 r2 = r * r;
    float3 d2 = d * d;
    float3 d3 = d2 * d;
    float3 r3 = r2 * r;

    float4 w = float4(d3.x + r3.x, d3.y + r3.y, d3.z + r3.z, 1.0);
    float4 w1 = w.x * w.y * w.z;

    float4 contribution = w1 * dot(g, d - float3(0.0,0.0,0.0));

    return dot(contribution, 1.0); // Final sum
}

// Curl Noise from 3D Simplex Noise
// 使用三个偏移噪声通道构造向量势，再求旋度
float3 curl_noise(float3 p) {
    float e = 0.1; // 小偏移量（有限差分步长）

    float noise_x0 = wsnoise(float3(p.x - e, p.y, p.z));
    float noise_x1 = wsnoise(float3(p.x + e, p.y, p.z));
    
    float noise_y0 = wsnoise(float3(p.x, p.y - e, p.z));
    float noise_y1 = wsnoise(float3(p.x, p.y + e, p.z));
    
    float noise_z0 = wsnoise(float3(p.x, p.y, p.z - e));
    float noise_z1 = wsnoise(float3(p.x, p.y, p.z + e));

    // 旋度公式：
    // curl = ( ∂w/∂y - ∂v/∂z,
    //          ∂u/∂z - ∂w/∂x,
    //          ∂v/∂x - ∂u/∂y )
    // 这里我们令：
    // u = noise_z, v = noise_x, w = noise_y （任意组合）
    
    float3 duv = float3(
        noise_z1 - noise_z0,
        noise_x1 - noise_x0,
        noise_y1 - noise_y0
    ) / (2.0 * e);

    float3 dwu = float3(
        noise_y1 - noise_y0,
        noise_z1 - noise_z0,
        noise_x1 - noise_x0
    ) / (2.0 * e);

    float3 curl;
    curl.x = dwu.y - duv.z;  // ∂w/∂y - ∂v/∂z
    curl.y = duv.x - dwu.z;  // ∂u/∂z - ∂w/∂x
    curl.z = duv.y - dwu.x;  // ∂v/∂x - ∂u/∂y

    return curl;
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