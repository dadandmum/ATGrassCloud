Shader "ATGrassCloud/GrassInstanceShader"
{

    Properties
    {
        [Header(Grass Shading)]
        _ColorA("ColorA(Wet Grass)", Color) = (0.2,1,0.8,1)
        _ColorB("ColorB(Dry Grass)", Color) = (0.5,0.8,0.8,1)
        _ColorNoiseFactor("Color Noise Factor", Range(0, 10.0)) = 0.5
        _AOColor("AO Color", Color) = (0.5,0.5,0.5)
        _AOFactor("AO Factor", Range(0, 2.0)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.5
        _Metallic("Metallic", Range(0, 1)) = 0.5

        [Header(Grass Geometry)][Space]
        _GrassWidth("Grass Width", Float) = 1
        _GrassHeight("Grass Height", Float) = 1
        _GrassCurveKeys("Grass Curve Key Values" , Vector ) = (1.0,0.5,0.5,0.5)
        _GrassWidthRandomness("Grass Width Randomness", Range(0, 1)) = 0.25
        _GrassHeightRandomness("Grass Height Randomness", Range(0, 1)) = 0.5

        [Header(Grass Direction)][Space]
        _GrassUpDirectionRandom("Grass Up Direction Randomness", Range(0, 1)) = 0.25
        _GrassFaceDirectionRandom("Grass Face Direction Randomness", Range(0, 1)) = 0.25
        _GrassDroopIntensity("Grass Droop Intensity", Range(0, 1)) = 2.0
        _GrassCurving("Grass Curving", Float) = 0.1

        [Space]
        _ExpandDistantGrassWidth("Expand Distant Grass Width", Float) = 1
        _ExpandDistantGrassRange("Expand Distant Grass Range", Vector) = (50, 200, 0, 0)
        
        [Header(Normal)][Space]
        _GrassNormalBlend("Grass Normal Blend", Range(-1, 1)) = 0.25

        [Header(Wind)][Space]
        _WindTexture("Wind Texture", 2D) = "white" {}
        _WindScroll("Wind Scroll", Vector) = (1, 1, 0, 0)
        _WindStrength("Wind Strength", Float) = 1

        [Header(Lighting)][Space]
        _RandomNormal("Random Normal", Range(0, 1)) = 0.1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Geometry"}

        Pass
        {
            Cull Back
            ZTest Less
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _PROCEDURAL_MESH

            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Lib/GrassLib.hlsl"
            #include "Lib/GrassShadingLib.hlsl"
            #include "Lib/GrassGeometryLib.hlsl"
            #include "Lib/Simplex.hlsl"
            
            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                half3 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                // Shading 
                half3 _ColorA;
                half3 _ColorB;
                float4 _BaseColorTexture_ST;
                half3 _AOColor;
                float _AOFactor;
                float _ColorNoiseFactor;
                float _Smoothness;
                float _Metallic;

                // Geometry 
                float _GrassWidth;
                float _GrassHeight;
                float4 _GrassCurveKeys;
                float _GrassWidthRandomness;
                float _GrassHeightRandomness;

                float _GrassUpDirectionRandom;
                float _GrassFaceDirectionRandom;

                float _GrassDroopIntensity;
                float _GrassCurving;

                float _ExpandDistantGrassWidth;
                float2 _ExpandDistantGrassRange;
                float4 _CascadeRange; // ( innerRange , outterRange , 1.0f / innerFade , 1.0f / outterFade )

                // Normal 
                float _GrassNormalBlend;

 
                float4 _WindTexture_ST;
                float _WindStrength;
                float2 _WindScroll;

                half _RandomNormal;

                // float2 _CenterPos;

                // float _DrawDistance;
                // // float _TextureUpdateThreshold;
                // float _SnapDistance;

                float4  _MapData;

                StructuredBuffer<GrassData> _GrassData;

            CBUFFER_END

            sampler2D _BaseColorTexture;
            sampler2D _WindTexture;

            sampler2D _GrassColorRT;
            sampler2D _GrassSlopeRT;

            half3 ApplySingleDirectLight(Light light, half3 N, half3 V, half3 albedo, half mask, half positionY)
            {
                half3 H = normalize(light.direction + V);

                half directDiffuse = dot(N, light.direction) * 0.5 + 0.5;

                float directSpecular = saturate(dot(N,H));
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;
                directSpecular *= directSpecular;

                directSpecular *= positionY * 0.12;

                half3 lighting = light.color * (light.shadowAttenuation * light.distanceAttenuation);
                half3 result = (albedo * directDiffuse + directSpecular * (1-mask)) * lighting;

                result = half3( 1 , 0 , 0);
                return result; 
            }



            float3 CalculateLighting(float3 albedo, float3 positionWS, float3 N, float3 V, float mask, float positionY){

                half3 result = SampleSH(0) * albedo;

                return albedo;
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(positionWS));
                result += ApplySingleDirectLight(mainLight, N, V, albedo, mask, positionY);

                return result;

                int additionalLightsCount = GetAdditionalLightsCount();
                for (int i = 0; i < additionalLightsCount; ++i)
                {
                    Light light = GetAdditionalLight(i, positionWS);
                    result += ApplySingleDirectLight(light, N, V, albedo, mask, positionY);
                }

                return result;
            }

            Varyings vert(Attributes IN, uint instanceID : SV_InstanceID)
            {
                Varyings OUT;

                float3 pivot = _GrassData[instanceID].position;
                uint rand = _GrassData[instanceID].rand0;

                // float2 uv = (pivot.xz - _CenterPos) / (_DrawDistance + _SnapDistance);
                float2 uv = (pivot.xz - _MapData.xy) * _MapData.w;
                uv = uv * 0.5 + 0.5;


                float3 upDirection = GetDefaultUpDirection(pivot, rand, _GrassUpDirectionRandom, _WorldSpaceCameraPos);
                float3 faceDirection = GetDefaultFaceDirection(pivot, rand, _GrassFaceDirectionRandom, _WorldSpaceCameraPos);

                float3 positionOS = IN.positionOS;
                #ifdef _PROCEDURAL_MESH
                float3 positionModel = grass_CreateProceduralPosition(positionOS, _GrassWidth, _GrassHeight, _GrassCurveKeys, _GrassWidthRandomness * grass_random(pivot.x * 842 + pivot.z * 12) );
                positionModel = grass_AdjustWithRandomness(pivot, positionModel, 1.0, _GrassWidthRandomness, 1.0, _GrassHeightRandomness, _WorldSpaceCameraPos, _CascadeRange, _ExpandDistantGrassWidth, faceDirection);
                #else

                float3 positionModel = grass_AdjustWithRandomness(pivot, positionOS, _GrassWidth, _GrassWidthRandomness, _GrassHeight, _GrassHeightRandomness, _WorldSpaceCameraPos, _CascadeRange, _ExpandDistantGrassWidth, faceDirection);
                #endif


                // TODO : add wind 


                float3 positionModelWithRot = grass_RecalculateByDirection(
                    upDirection, 
                    faceDirection, 
                    positionModel, 
                    _GrassDroopIntensity);
                 
                //posOS -> posWS
                float3 positionWS = positionModelWithRot + pivot;

                float3 modelNormal = grass_ModelNormal(positionModel, _GrassNormalBlend);
                float3 normalWS = grass_GetNormal(
                    positionModel,
                    upDirection,
                    faceDirection,
                    modelNormal,
                    _GrassDroopIntensity);

                float albedoRand = snoise(positionWS / _CascadeRange.y * 3.41 * _ColorNoiseFactor);
                float3 albedo = grass_Albedo(_ColorA, _ColorB, albedoRand, _AOColor, positionOS, _AOFactor);
                float3 viewWS = normalize(_WorldSpaceCameraPos - positionWS);

                //posWS -> posCS
                OUT.positionCS = TransformWorldToHClip(positionWS);

                // shading 
                float specularMask = 0.0; // TODO : pass by map
                OUT.color = grass_Shading(albedo, _Smoothness, _Metallic, positionWS, normalWS, viewWS, specularMask, positionModel);
                
                // OUT.color = float4(0,positionWS.y * 0.1 + 0.5,0,1);
                return OUT;

                // float grassWidth = _GrassWidth * (1 - grass_random(pivot.x * 950 + pivot.z * 10) * _GrassWidthRandomness);

                // float distanceFromCamera = length(_WorldSpaceCameraPos - pivot);
                // //Expand the grass width based on the distance from camera
                // grassWidth += saturate(grass_Remap(distanceFromCamera, float2(_ExpandDistantGrassRange.x, _ExpandDistantGrassRange.y), float2(0, 1))) * _ExpandDistantGrassWidth;
                // grassWidth *= (1 - IN.positionOS.y);

                // //Grass Height
                // float grassHeight = _GrassHeight * (1 - grass_random(pivot.x * 230 + pivot.z * 10) * _GrassHeightRandomness);
                
                //Billboard Logic
                // float3 cameraTransformRightWS = UNITY_MATRIX_V[0].xyz;
                // float3 cameraTransformUpWS = UNITY_MATRIX_V[1].xyz;
                // float3 cameraTransformForwardWS = -UNITY_MATRIX_V[2].xyz;

                // // float4 slope = tex2Dlod(_GrassSlopeRT, float4(uv, 0, 0));
                // float4 slope = float4(0, 0, 0, 0);
                // float xSlope = slope.r * 2 - 1;
                // float zSlope = slope.g * 2 - 1;

                // float3 slopeDirection = normalize(float3(xSlope, 1 - (max(abs(xSlope), abs(zSlope)) * 0.5), zSlope));//Direction reconstructed from the slope texture
                // float3 bladeDirection = normalize(lerp(float3(0, 1, 0), slopeDirection, slope.a));//The original direction is upward

                // // half3 windTex = tex2Dlod(_WindTexture, float4(TRANSFORM_TEX(pivot.xz, _WindTexture) + _WindScroll * _Time.y,0,0));
                // half3 windTex = half3(0, 0, 0);
                // float2 wind = (windTex.rg * 2 - 1) * _WindStrength * (1-slope.a);

                // bladeDirection.xz += wind * IN.positionOS.y;//Adding wind and multiplying with the Y position to affect the tip only

                // bladeDirection = normalize(bladeDirection);
                
                // float3 rightTangent = normalize(cross(bladeDirection, cameraTransformForwardWS));//The direction we gonna stretch the blade

                // float3 positionOS = bladeDirection * IN.positionOS.y * grassHeight 
                //                     + rightTangent * IN.positionOS.x * grassWidth;//This insures that the blade is always facing the camera

                // positionOS.xz += (IN.positionOS.y * IN.positionOS.y) * float2(srandom(pivot.x * 851 + pivot.z * 10), srandom(pivot.z * 647 + pivot.x * 10)) * _GrassCurving;
                //Adds a bit of curving to grass blade


                // half3 baseColor = lerp(_ColorA, _ColorB, tex2Dlod(_BaseColorTexture, float4(TRANSFORM_TEX(pivot.xz, _BaseColorTexture),0,0)).r);
                
                // half3 albedo = lerp(_AOColor, baseColor, IN.positionOS.y);

                // float4 color = tex2Dlod(_GrassColorRT, float4(uv, 0, 0));
                // albedo = lerp(albedo, color.rgb, color.a);

                // //Lighting Stuff
                // half3 N = normalize(bladeDirection + cameraTransformForwardWS * -0.5 + _RandomNormal * half3(grass_srandom(pivot.x * 314 + pivot.z * 10), 0, grass_srandom(pivot.z * 677 + pivot.x * 10)));
                // //The normal vector is just the blade direction tilted a bit towards the camera with a bit of randomness
                // half3 V = normalize(_WorldSpaceCameraPos - positionWS);

                // float3 lighting = CalculateLighting(albedo, positionWS, N, V, color.a, IN.positionOS.y);
                // //I'm also passing the Alpha Channel of the Color Map cause I dont want the blades that are affected with color to receive specular light 
                // //The main use of the color map for me is burning the grass and the burned grass should not receive specular light
                
                // float fogFactor = ComputeFogFactor(OUT.positionCS.z);
                // OUT.color.rgb = MixFog(lighting, fogFactor);

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(IN.color.rgb,1);
            }
            ENDHLSL
        }
    }
}