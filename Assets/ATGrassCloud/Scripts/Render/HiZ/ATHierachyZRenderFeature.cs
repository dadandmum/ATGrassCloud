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

            public int stopHiZlevel = 8;
            public bool useDepthPrePass = true;
            public bool useCustomDepthPass = true;
            public Shader hiZShader;
            public Shader copyDepthShader;
            public LayerMask layerMask = ~0;

        }

        public ATHiZSettings settings = new ATHiZSettings();

        private HiZPass m_ScriptablePass;
        private ATCopyDepthPass m_CopyDepthPass;

        public override void Create() 
        {
            m_ScriptablePass = new HiZPass(
                settings.hiZShader,
                settings.stopHiZlevel,  
                settings.useDepthPrePass,
                settings.useCustomDepthPass,
                settings.layerMask);

            m_ScriptablePass.renderPassEvent = settings.hiZEvent;
            m_CopyDepthPass = new ATCopyDepthPass(settings.copyDepthShader);
            m_CopyDepthPass.renderPassEvent = settings.copyDepthEvent;

        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(m_ScriptablePass);
                renderer.EnqueuePass(m_CopyDepthPass);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if ( disposing )
            {
                m_ScriptablePass.Dispose();
            }
        }

    }
}