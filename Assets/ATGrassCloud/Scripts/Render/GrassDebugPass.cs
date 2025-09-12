using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class GrassDebugPass : ScriptableRenderPass
    {
        public ATGrassRenderFeature.GrassDebugData debugData;
        public GrassPrePass grassPass;

        public ATGrassData grassData;

        public GrassDebugPass(
            ATGrassRenderFeature.GrassDebugData debugData , 
            GrassPrePass grassPass ,
            ATGrassData grassData)
        {
            this.debugData = debugData;
            this.grassPass = grassPass;
            this.grassData = grassData;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 可选：配置渲染目标或临时纹理
        }
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // 如果需要配置渲染目标，例如切换 RenderTarget
            // 当前使用场景默认目标，无需配置
        }
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ( !debugData.enableDebug || renderingData.cameraData.cameraType != CameraType.Game) 
            {
                return;
            }
            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler("Grass Debug")))
            {
                {
                    var cascade = grassPass.GetCascade(debugData.debugCascade);

                    if (debugData.isShowHeightMap)
                    {
                        cmd.Blit(cascade.GetHeightRT(), renderingData.cameraData.renderer.cameraColorTargetHandle);
                    }
                }


            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

    }
}