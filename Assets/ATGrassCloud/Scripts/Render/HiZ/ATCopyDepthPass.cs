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
        private RTHandle m_DepthCopyRT;

        private Material m_DepthCopyMaterial;

        static public int CAMERA_DEPTH_ATTACHMENT_ID = Shader.PropertyToID("_CameraDepthAttachment");
        static public int DEPTH_FULL_TEX_ID = Shader.PropertyToID("_DepthFullTex");

        public ATCopyDepthPass( Shader copyDepthShader )
        {
            if ( copyDepthShader != null )
                m_DepthCopyMaterial  = new Material(copyDepthShader);
        }


        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.colorFormat = RenderTextureFormat.RFloat;
            

            RenderingUtils.ReAllocateIfNeeded(ref m_DepthCopyRT, desc, name: "Depth_Copy");

            ConfigureTarget(m_DepthCopyRT);
            ConfigureClear(ClearFlag.All, Color.clear);

        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_DepthCopyMaterial == null )
            {
                return;
            }

    
            CommandBuffer cmd = CommandBufferPool.Get("[AT] Copy Depth");
            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Copy Depth")))
            {

                var source = renderingData.cameraData.renderer.cameraDepthTargetHandle;
                var cameraData = renderingData.cameraData;
                bool yflip = cameraData.IsHandleYFlipped(source) != cameraData.IsHandleYFlipped(m_DepthCopyRT); 
                Vector2 viewportScale = source.useScaling ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y) : Vector2.one;
                Vector4 scaleBias = yflip ? new Vector4(viewportScale.x, -viewportScale.y, 0, viewportScale.y) : new Vector4(viewportScale.x, viewportScale.y, 0, 0);
      
                int msaaSamples = renderingData.cameraData.cameraTargetDescriptor.msaaSamples;

                switch (msaaSamples)
                {
                    case 8:
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                        cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                        break;

                    case 4:
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                        break;

                    case 2:
                        cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                        break;

                    // MSAA disabled, auto resolve supported or ms textures not supported
                    default:
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                        cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                        break;
                }
                cmd.DisableShaderKeyword("_OUTPUT_DEPTH");

                
                m_DepthCopyMaterial.SetVector("_BlitScaleBias", scaleBias);
                // cmd.SetGlobalVector("_BlitScaleBias", scaleBias);
                

                cmd.SetGlobalTexture(CAMERA_DEPTH_ATTACHMENT_ID, renderingData.cameraData.renderer.cameraDepthTargetHandle);
                cmd.Blit(renderingData.cameraData.renderer.cameraDepthTargetHandle, m_DepthCopyRT, m_DepthCopyMaterial );
                
                cmd.SetGlobalTexture(DEPTH_FULL_TEX_ID, m_DepthCopyRT);

                // cmd.Blit(renderingData.cameraData.renderer.cameraDepthTargetHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle, null, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
