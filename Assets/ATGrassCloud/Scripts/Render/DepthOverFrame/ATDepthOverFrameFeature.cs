using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class ATDepthOverFrameRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class ATDOFSettings
        {
            public RenderPassEvent Event = RenderPassEvent.AfterRenderingTransparents;
            public string globalPreDepthTextureName = "_PreDepthTexture";
        }

        public ATDOFSettings settings = new ATDOFSettings();

        private DepthOverFramePass m_ScriptablePass;

        public override void Create() 
        {
            m_ScriptablePass = new DepthOverFramePass(settings.globalPreDepthTextureName);  
            m_ScriptablePass.renderPassEvent = settings.Event;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);
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