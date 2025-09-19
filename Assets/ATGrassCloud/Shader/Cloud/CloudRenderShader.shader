Shader "ATGrassCloud/CloudRenderShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }


    HLSLINCLUDE

    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Assets/ATGrassCloud/Shader/Lib/DepthZLib.hlsl"
    #include "Assets/ATGrassCloud/Shader/Lib/CloudLib.hlsl"

    struct Attributes
    {
        uint vertexID : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 texcoord   : TEXCOORD0;
        float3 viewDir    : TEXCOORD1;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    
    Varyings vert(Attributes input)
    {
        Varyings output;

        UNITY_SETUP_INSTANCE_ID(input);

        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);

        output.texcoord = GetFullScreenTriangleTexCoord(input.vertexID);

        float2 ndp = output.texcoord * 2 - 1.0;

        float3 viewVector = mul(unity_CameraInvProjection, float4(ndp, 0, -1.0 ));
        output.viewDir = mul(unity_CameraToWorld, float4(viewVector,0));

        return output;
    }
    
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}

        Pass
        {
            Name "Cloud Render Pass"
            Cull Off
            ZTest Always
            Blend One OneMinusSrcAlpha
            // Blend One Zero

            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT

            float4 _CascadeRange;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            Texture2DMS<float, 2> _CameraDepthAttachment;
            float4 _CameraDepthAttachment_TexelSize;

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float3 viewDir = IN.viewDir / length(IN.viewDir);
               
                float4 cascadeRange = _CascadeRange;
                float maxCascadeDis = _CascadeRange.y + 1.0 / _CascadeRange.w;
                float depthDistance = depth_LinearEyeDepth( depth_GetDepthFull(uv));
                float maxDistance = min(maxCascadeDis, depthDistance);

                float3 camPos = _WorldSpaceCameraPos;
                float3 lighting = GetMainLight().color;
                // float depth = LOAD_TEXTURE2D_MSAA(_CameraDepthAttachment, uv , 0 ).r;
                // float worldDistance = depth_Depth2WorldDistance(depth);
                // return half4( worldDistance, 0, 0, 1);
                float startDistance = GetCloudObjectSurfaceDistance( camPos, viewDir, maxDistance);

                float alpha = 0;
                float3 color = float3(0,0,0);
                if ( startDistance < maxDistance - 0.01f  )
                {
                    float4 raymarchResult = cloud_Raymarch( camPos, viewDir, uv, lighting, startDistance, maxDistance , cascadeRange);
                    color = raymarchResult.rgb;
                    alpha = raymarchResult.w;
                }

                return half4(  color , alpha);

            }

            ENDHLSL
        }
    }
}
