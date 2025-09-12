Shader "ATGrassCloud/WindShader"
{
    Properties
    {
        [Header(Wind Settings)]
        _MainTex ("Texture", 2D) = "white" {}
        _WindSampleTex ("Wind Texture", 2D) = "white" {}
        _WindDir("Wind Direction",Vector) = (0.5, 0.3, 0, 0)
        _WindSpeed("Wind Speed",Float) = 1.0
        _WindScale("Wind Scale",Float) = 0.1

        [Header(Noise Settings)]
        _NoiseScale ("Noise Scale", Float) = 5.0
        _NoiseSpeed ("Noise Speed", Vector) = (0.5, 0.3, 0, 0)
        _FlowStrength ("Flow Strength", Float) = 1.0
        _Color ("Base Color", Color) = (0.2, 0.4, 1.0, 1.0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "CurlNoisePass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Lib/WindLib.hlsl"
 
            // Texture & properties
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_WindSampleTex);
            SAMPLER(sampler_WindSampleTex);


            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _NoiseScale;
                float2 _WindDir;
                float _WindSpeed;
                float _WindScale;

                float2 _NoiseSpeed;
                float4 _WindPositionParams; // ( windPos.x, windPos.Z , 1.0 / windRange , 0 )
                float _FlowStrength;
                float4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 posWorldXZ : TEXCOORD1; // 用于时间动画
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                // VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                // OUT.positionCS = positionInputs.positionCS;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);

                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                // OUT.posWorld = positionInputs.positionWS;

                OUT.posWorldXZ = wind_UV2PosXZ(IN.uv, _WindPositionParams);

                return OUT;
            }

            // Fragment Shader
            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 posWorldXZ = IN.posWorldXZ;

                return half4( WindEncode(
                    windNoise(
                        posWorldXZ,
                        _WindScale,
                        _FlowStrength, 
                        _NoiseSpeed, 
                        _WindSpeed, 
                        normalize(_WindDir) )) 
                        , 0 , 1.0);   


                // float2 uv = IN.uv;
                // float2 posWorldXZ = IN.posWorldXZ;

                // // 动画参数
                // float2 offset = _NoiseSpeed * _Time.y; // _Time.y 是 Unity 时间
                // float2 noiseUV = uv * _NoiseScale + offset;
                // float2 windOffset = _WindDir.xy * _WindSpeed * _Time.y;

                // // 获取旋度噪声向量
                // float2 flow = curlNoise(noiseUV) * _FlowStrength;

                // // 可视化方向和强度
                // half2 colorDir = half2(0.5, 0.5) + 0.5 * normalize(flow); // 方向映射
                // half len = length(flow);

                // // 扰动纹理采样（可选）
                // half4 texSample = SAMPLE_TEXTURE2D(_WindSampleTex, sampler_WindSampleTex, uv + flow  + windOffset); 
            
                // return half4(texSample.rgb, 1.0);
            }
            ENDHLSL
        }
    }
}
