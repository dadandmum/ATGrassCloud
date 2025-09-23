#ifndef TONEMAP_HLSL
#define TONEMAP_HLSL

// Reference : 
// https://www.shadertoy.com/view/lslGzl

const static float gamma = 2.2;


float3 linearToneMapping(float3 color)
{
    float exposure = 1.0;
    color = clamp(exposure * color, 0.0, 1.0);
    color = pow(color, 1.0 / gamma);
    return color;
}

float3 simpleReinhardToneMapping(float3 color)
{
    float exposure = 1.5;
    color = exposure * color / (1.0 + color / exposure);
    color = pow(color, 1.0 / gamma);
    return color;
}

float3 lumaBasedReinhardToneMapping(float3 color)
{
    float3 lumaCoeff = float3(0.2126, 0.7152, 0.0722);
    float luma = dot(color, lumaCoeff);
    float toneMappedLuma = luma / (1.0 + luma);
    color *= toneMappedLuma / luma;
    color = pow(color, 1.0 / gamma);
    return color;
}

float3 whitePreservingLumaBasedReinhardToneMapping(float3 color)
{
    float white = 2.0;
    float3 lumaCoeff = float3(0.2126, 0.7152, 0.0722);
    float luma = dot(color, lumaCoeff);
    float toneMappedLuma = luma * (1.0 + luma / (white * white)) / (1.0 + luma);
    color *= toneMappedLuma / luma;
    color = pow(color, 1.0 / gamma);
    return color;
}

float3 RomBinDaHouseToneMapping(float3 color)
{
    color = exp(-1.0 / (2.72 * color + 0.15));
    color = pow(color, 1.0 / gamma);
    return color;
}

float3 filmicToneMapping(float3 color)
{
    color = max(0.0, color - 0.004);
    color = (color * (6.2 * color + 0.5)) / (color * (6.2 * color + 1.7) + 0.06);
    return color;
}


float3 Uncharted2ToneMapping(float3 color)
{
	float A = 0.15;
	float B = 0.50;
	float C = 0.10;
	float D = 0.20;
	float E = 0.02;
	float F = 0.30;
	float W = 11.2;
	float exposure = 2.;
	color *= exposure;
	color = ((color * (A * color + C * B) + D * E) / (color * (A * color + B) + D * F)) - E / F;
	float white = ((W * (A * W + C * B) + D * E) / (W * (A * W + B) + D * F)) - E / F;
	color /= white;
	color = pow(color, float3(1.0 / gamma, 1.0 / gamma, 1.0 / gamma));
	return color;
}


#endif 