using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class ATHierachyZRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class ATHiZSettings
        {
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingPrePasses;
            public int stopHiZlevel = 8;
            public bool useDepthPrePass = true;
            public bool useCustomDepthPass = true;
            public Shader hiZShader;

            public LayerMask layerMask = ~0;

        }

        public ATHiZSettings settings = new ATHiZSettings();

        private HiZPass m_ScriptablePass;

        public override void Create() 
        {
            m_ScriptablePass = new HiZPass(
                settings.hiZShader,
                settings.stopHiZlevel,  
                settings.useDepthPrePass,
                settings.useCustomDepthPass,
                settings.layerMask);

            m_ScriptablePass.renderPassEvent = settings.Event;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            // if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                renderer.EnqueuePass(m_ScriptablePass);
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