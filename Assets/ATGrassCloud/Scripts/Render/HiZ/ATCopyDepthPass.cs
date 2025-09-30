using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace ATGrassCloud
{
    public class ATCopyDepthPass : ScriptableRenderPass
    {
        // private RTHandle m_DepthCopyRT;

        // private Material m_DepthCopyMaterial;

        private ATDepthRenderer depthRenderer;

        // static public int CAMERA_DEPTH_ATTACHMENT_ID = Shader.PropertyToID("_CameraDepthAttachment");
        // static public int DEPTH_FULL_TEX_ID = Shader.PropertyToID("_DepthFullTex");


        public ATCopyDepthPass( ATDepthRenderer depthRenderer )
        {
            this.depthRenderer = depthRenderer;
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if ( depthRenderer == null )
                return;
                
            depthRenderer.ConfigureCopyDepth(cmd, ref renderingData,this);
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ( depthRenderer == null )
                return;

            CommandBuffer cmd = CommandBufferPool.Get("[AT] Copy Depth");
            
            depthRenderer.RenderCopyDepth(context, ref renderingData, cmd);
            
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }



        public void Dispose()
        {
            
        }

    }
}
