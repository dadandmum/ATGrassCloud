using System.Collections;
using System.Collections.Generic;
using Microsoft.SqlServer.Server;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{

    public class WindDebugPass : ScriptableRenderPass
    {
        public WindPrePass windPass;
        public WindData data;

        public WindDebugPass(
            WindPrePass windPass ,
            WindData windData)
        {
            this.windPass = windPass;
            this.data = windData;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ( !data.enableDebug || renderingData.cameraData.cameraType != CameraType.Game) 
            {
                return;
            }
            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler("Wind Debug")))
            {
                
                cmd.Blit(windPass.GetCurrentWindTexture(), renderingData.cameraData.renderer.cameraColorTargetHandle);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }
    }
}
