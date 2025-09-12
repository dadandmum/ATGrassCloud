
float QuadraticLagrange(float t, float t0, float v0, float t1, float v1, float t2, float v2)
{
    return v0 * (t - t1) * (t - t2) / ((t0 - t1) * (t0 - t2)) +
           v1 * (t - t0) * (t - t2) / ((t1 - t0) * (t1 - t2)) +
           v2 * (t - t0) * (t - t1) / ((t2 - t0) * (t2 - t1));
}
float grass_EvaluateCurveLagrange(float t, float4 keys)
{

    float v[6] = {1.0f, keys.x, keys.y, keys.z, keys.w, 0.0f};
    float t_vals[6] = {0.0f, 0.2f, 0.4f, 0.6f, 0.8f, 1.0f};

    t = clamp(t, 0.0f, 1.0f);
    int i = 0;
    if (t <= 0.2f) i = 0;  // 浣跨敤 0,1,2
    else if (t <= 0.4f) i = 0;
    else if (t <= 0.6f) i = 1;  // 浣跨敤 1,2,3
    else if (t <= 0.8f) i = 2;  // 浣跨敤 2,3,4
    else i = 3;  // 浣跨敤 3,4,5

    return QuadraticLagrange(t, 
        t_vals[i],   v[i],
        t_vals[i+1], v[i+1],
        t_vals[i+2], v[i+2]);
}

float grass_EvaluateCurveLinear(float t, float4 keyValues, float randomness)
{
    t = clamp(t, 0.0, 1.0);
    float points[6] = { 1.0, 
    keyValues.x * (1 + randomness * 0.2 ), 
    keyValues.y * (1 + randomness * 0.1 ), 
    keyValues.z * (1 - randomness * 0.1 ), 
    keyValues.w * (1 - randomness * 0.2 ),
     0.0 };

    // 绛夎窛锛氭瘡 0.2 涓€娈?鈫?娈电储寮?= floor(t / 0.2)
    float segment = t * 5.0;  // t鈭圼0,1] 鈫?segment鈭圼0,5]
    int i = (int)segment;

    // 杈圭晫澶勭悊
    if (i >= 5) return points[5];
    if (i < 0) return points[0];

    // 灞€閮ㄥ弬鏁?u 鈭?[0,1]
    float u = segment - i;

    return lerp(points[i], points[i+1], u);
}


float3 grass_CreateProceduralPosition(float3 positionOS , float width , float height, float4 curveKeys, float randomness )
{
    // calculate the position based on the curve index
    float x = grass_EvaluateCurveLinear(positionOS.y, curveKeys, randomness ) * positionOS.x;
    return float3( x * width , positionOS.y * height, positionOS.z);
}

float3 grass_AdjustWithRandomness( float3 pivot, float3 positionOS, float width, float widthRandomness , float height, float heightRandomness , float3 cameraPos , float4 cascadeRange , float rangeExtend , float3 faceDirection )
{
    float grassWidth = width * (1 - grass_random(pivot.x * 950 + pivot.z * 10) * widthRandomness);

    float distanceFromCamera = length(cameraPos - pivot);
    //Expand the grass width based on the distance from camera
    grassWidth += saturate(grass_Remap(distanceFromCamera, float2(cascadeRange.x, cascadeRange.y), float2(0, 1))) * rangeExtend;

    grassWidth += dot(faceDirection, float3(0,1,0)) * rangeExtend;
    
    float grassHeight = height * (1 - grass_random(pivot.x * 230 + pivot.z * 10) * heightRandomness);

    return float3(positionOS.x * grassWidth, positionOS.y * grassHeight, positionOS.z);
}

float decodeRand(uint rand)
{
    return (rand / 4294967295.0);
}

float decodeRandLow(uint rand)
{
    return ( rand & 0xFFFF ) / 65535.0;
}

float decodeRandHigh(uint rand)
{
    return ( rand >> 16 ) / 65535.0;
}



float3 GetDefaultUpDirection( float3 pivot , uint rand, float randomness, float3 cameraPos)
{   
    float angle = decodeRandHigh(rand) * 6.28318530718;
    float range = decodeRandLow(rand) * randomness;
    float3 up = float3(0,1,0);

    float3 direcitonToCamera = normalize( cameraPos - pivot);
    float3 tangentDir = cross( up , direcitonToCamera);
    float3 fixedUp = - cross( tangentDir , direcitonToCamera);
    float upLerp = dot( direcitonToCamera , up);
    upLerp *= upLerp ;
    upLerp *= upLerp * 0.5;
    float3 upDir = lerp( up , fixedUp , upLerp);

    return normalize(float3(sin(angle) * range , 0 , cos(angle) * range ) + upDir);
}

float3 GetDefaultFaceDirection( float3 pivot , uint rand, float randomness, float3 cameraPos)
{
    float angle = decodeRandLow(rand) * 6.28318530718;
    float range = decodeRandHigh(rand) * randomness;

    float3 direcitonToCamera = normalize( cameraPos - pivot);
    float3 dirY = abs(direcitonToCamera.y);
    direcitonToCamera.y *= direcitonToCamera.y * 0.5 ;
    // direcitonToCamera.y *= 0;
    direcitonToCamera = normalize(direcitonToCamera);
    float3 up = float3(0,1.0,0);

    float3 tangentDir = cross( up , direcitonToCamera);
    float3 bitangentDir = cross( direcitonToCamera , tangentDir);

    float offsetRange = lerp( 1.0f , 0.02f , dirY ) * range;
    float3 offsetNoise = (sin(angle)  * tangentDir + cos(angle)  * bitangentDir) * offsetRange;
    return normalize(offsetNoise + direcitonToCamera);
}

float3 grass_RecalculateByDirection( float3 up , float3 forward , float3 positionModel , float droop  )
{
    float3 newForward = normalize(forward);
    float3 right = normalize(-cross(up, newForward));
    float3 newUp = normalize( cross(right, newForward));

    float3 newPosition = positionModel.x * right + positionModel.y * newUp + positionModel.z * newForward;
    newPosition += droop * positionModel.y * positionModel.y * float3( 0 , -1.0 , 0 );
    return newPosition;
}


float3 grass_ApplyWindToUp( float3 upDir , float2 windXZ , float windStrength, uint rand, float windRandomness, float3 positionModel )
{
    float3 wind = float3(windXZ.x, 0, windXZ.y);
    float randAngle = decodeRandLow(rand << 3) * 6.28318530718;
    wind *= 1.0 + (decodeRandLow(rand << 3) * 2 - 1) * windRandomness;

    wind *= windStrength;


    float3 newDir = upDir + wind * positionModel.y;
    return normalize(newDir);


}
