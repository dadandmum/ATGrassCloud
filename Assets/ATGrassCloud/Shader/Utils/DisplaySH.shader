Shader "Debug/Display SH"
{
    Properties
    {
        // 可以添加一个缩放因子来调整显示强度（可选）
        // _NormalScale("Normal Scale", Range(0.0, 2.0)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"="Geometry"}


        Pass
        {
            Name "NormalDisplay"
            // 使用通用前向渲染路径
            Tags { "LightMode" = "UniversalForward" } 

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/ATGrassCloud/Shader/Lib/ATGI.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL; // 从顶点数据中获取对象空间法线
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 normalWS     : TEXCOORD0; // 传递世界空间法线到片元着色器
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // 将顶点位置从对象空间转换到裁剪空间（HCS - Homogeneous Clip Space）
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // 将法线从对象空间转换到世界空间
                // 注意：法线是方向向量，需要用逆转置矩阵变换，但对于均匀缩放的变换，
                // 可以直接使用世界变换矩阵的左上3x3部分。
                // Unity的TransformObjectToWorldNormal函数处理了这些细节。
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 对法线进行归一化（虽然通常已经是单位向量，但以防万一）
                float3 normalizedNormal = normalize(input.normalWS);
                
                // float3 normalColor = normalizedNormal ;
                // 计算SH值
                // float3 shColor = ATGI_StaticSH(normalizedNormal);
                float3 shColor = ATGI_SampleSH0(normalizedNormal);
                // 返回SH颜色，Alpha设为1.0 (不透明)
                return half4(shColor, 1.0);
            }
            ENDHLSL
        }


        // --- Depth Only Pass (仅渲染深度) ---
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            // 确保写入深度缓冲区
            ZWrite On
            // 通常对于不透明物体，使用Less或LEqual深度测试
            ZTest LEqual
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            ENDHLSL
        }
        
        // --- Shadow Caster Pass (用于渲染阴影) ---
        // Shadow Caster Pass 本质上也是一种 Depth Only Pass，
        // 但可能需要处理 Alpha Test 或其他特定逻辑。
        // 这里提供一个标准的 Shadow Caster 实现。
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            Cull Back // 通常背面剔除

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            // 支持实例化
            #pragma multi_compile_instancing 
            // 支持点光源/聚光灯阴影
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW 

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                // float3 normalOS     : NORMAL; // 如果需要处理法线偏移等
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                // float3 normalWS     : TEXCOORD0; // 如果需要
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // TransformVertexToHClip 处理了阴影坐标变换
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                // 对于不透明物体，通常返回0即可
                // 如果有Alpha Test，则需要在此处进行discard
                return 0;
            }
            ENDHLSL
        }
        
    }
}



