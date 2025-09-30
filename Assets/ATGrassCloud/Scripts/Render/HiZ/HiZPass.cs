using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class HiZPass : ScriptableRenderPass
    {
        private ATDepthRenderer renderer;

        public HiZPass(ATDepthRenderer renderer)
        {
            this.renderer = renderer;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {

            if ( renderer == null )
                return;
            renderer.ConfigureHiZ(cmd, ref renderingData);
        }

        public void SetGlobalParameters( ComputeShader computeShader , int kernel )
        {
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // if ( m_material == null )
            //     return; 
            
            if ( renderer == null )
                return;

            CommandBuffer cmd = CommandBufferPool.Get("[AT] HiZPass");
            
            // using (new ProfilingScope(cmd, new ProfilingSampler("[AT] HiZ Pass")))
            {
                renderer.RenderHiZ(context, ref renderingData,cmd,this);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);


        }

        public void Dispose()
        {

        }



    }
}