#ifndef ATGI_HLSL
#define ATGI_HLSL

float3  AT_SH_0;
float3  AT_SH_1;
float3  AT_SH_2;
float3  AT_SH_3;
float3  AT_SH_4;
float3  AT_SH_5;
float3  AT_SH_6;
float3  AT_SH_7;
float3  AT_SH_8;

#define ATGI_PI 3.1415926
static float ATGI_RCP_PI = rcp(ATGI_PI);


float3 ATGI_sampleSH( float3 dir, float3 sh[9] ) 
{
    float3 col = sh[0] * 0.5 * sqrt(ATGI_RCP_PI);
    col += sh[1] * dir.y * 0.5 * sqrt(3.0 * ATGI_RCP_PI);
    col += sh[2] * dir.z * 0.5 * sqrt(3.0 * ATGI_RCP_PI);
    col += sh[3] * dir.x * 0.5 * sqrt(3.0 * ATGI_RCP_PI);
    col += sh[4] * dir.x * dir.y * 0.5 * sqrt(15.0 * ATGI_RCP_PI);
    col += sh[5] * dir.y * dir.z * 0.5 * sqrt(15.0 * ATGI_RCP_PI);
    col += sh[6] * ( 3 * dir.z * dir.z - 1.0) * 0.25 * sqrt(5.0 * ATGI_RCP_PI);
    col += sh[7] * dir.x * dir.z * 0.5 * sqrt(15.0 * ATGI_RCP_PI);
    col += sh[8] * (dir.x * dir.x - dir.y * dir.y) * 0.25 * sqrt(15.0 * ATGI_RCP_PI);
    return col;
}


float3 ATGI_SampleSH0( float3 dir )
{
    float3 sh[9];
    sh[0] = AT_SH_0;
    sh[1] = AT_SH_1;
    sh[2] = AT_SH_2;
    sh[3] = AT_SH_3;
    sh[4] = AT_SH_4;
    sh[5] = AT_SH_5;
    sh[6] = AT_SH_6;
    sh[7] = AT_SH_7;
    sh[8] = AT_SH_8;
    return ATGI_sampleSH( dir, sh );
}


#endif 