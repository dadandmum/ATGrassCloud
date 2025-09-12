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

            public Shader hiZShader;
        }

        public ATHiZSettings settings = new ATHiZSettings();

        private HiZPass m_ScriptablePass;

        public override void Create() 
        {
            m_ScriptablePass = new HiZPass(settings.stopHiZlevel);  
            m_ScriptablePass.renderPassEvent = settings.Event;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(m_ScriptablePass);

        }
    }
}