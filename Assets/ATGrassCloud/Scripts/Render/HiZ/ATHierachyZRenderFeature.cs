using JetBrains.Annotations;
using Microsoft.SqlServer.Server;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.Universal.Internal;

namespace ATGrassCloud
{
    public class ATHierachyZRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class ATHiZSettings
        {
            public RenderPassEvent hiZEvent = RenderPassEvent.AfterRenderingPrePasses;

            public RenderPassEvent copyDepthEvent = RenderPassEvent.AfterRenderingOpaques;

            // public int stopHiZlevel = 8;
            // public bool useDepthPrePass = true;
            // public bool useCustomDepthPass = true;
            // public Shader hiZShader;
            // public Shader copyDepthShader;
            // public LayerMask layerMask = ~0;

            public ATHiZData data;
        }

        public ATHiZSettings settings = new ATHiZSettings();

        private HiZPass m_HiZPass;
        private ATCopyDepthPass m_CopyDepthPass;

        private ATDepthRenderer depthRenderer;

        public override void Create() 
        {
            depthRenderer = new ATDepthRenderer(settings.data);
            // m_HiZPass = new HiZPass(
            //     settings.hiZShader,
            //     settings.stopHiZlevel,  
            //     settings.useDepthPrePass,
            //     settings.useCustomDepthPass,
            //     settings.layerMask);
            m_HiZPass = new HiZPass(depthRenderer);
            m_HiZPass.renderPassEvent = settings.hiZEvent;
            m_CopyDepthPass = new ATCopyDepthPass(depthRenderer);
            m_CopyDepthPass.renderPassEvent = settings.copyDepthEvent;

        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(m_HiZPass);
                renderer.EnqueuePass(m_CopyDepthPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if ( disposing )
            {
                m_HiZPass?.Dispose();
                // m_CopyDepthPass?.Dispose();
                depthRenderer?.Dispose();    
            }
        }

    }
}