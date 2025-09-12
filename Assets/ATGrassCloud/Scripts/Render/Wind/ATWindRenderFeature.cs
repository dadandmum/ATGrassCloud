using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class ATWindRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class ATWindSettings
        {
            public RenderPassEvent Event = RenderPassEvent.BeforeRenderingTransparents;
            public WindData windData;
        }

        public ATWindSettings settings = new ATWindSettings();

        private WindPrePass m_ScriptablePass;
        private WindDebugPass m_DebugPass;

        public override void Create() 
        {
            m_ScriptablePass = new WindPrePass(settings.windData);
            m_ScriptablePass.renderPassEvent = settings.Event;
            m_DebugPass = new WindDebugPass( m_ScriptablePass, settings.windData);
            m_DebugPass.renderPassEvent = RenderPassEvent.AfterRendering;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);
            renderer.EnqueuePass(m_DebugPass);

        }
    }
}