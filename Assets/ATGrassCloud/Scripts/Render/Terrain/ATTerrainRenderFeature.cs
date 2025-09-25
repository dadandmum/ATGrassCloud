using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class ATTerrainRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public ATTerrainRenderData data;
            public RenderPassEvent PreRenderEvent = RenderPassEvent.BeforeRendering;
            public RenderPassEvent RenderEvent = RenderPassEvent.AfterRenderingOpaques;
        }   
         [SerializeField] private Settings m_Settings = new Settings();

        private ATTerrainRenderPass renderPass;
        private ATTerrainPreRenderPass preRenderPass;

        private ATTerrainRenderer renderer;

        public override void Create()
        {
            if (m_Settings.data == null)
            {
                Debug.LogError("ATTerrainRenderFeature: data is null");
                return;
            }
            if (m_Settings.data.patchMesh == null)
            {
                Debug.LogError("ATTerrainRenderFeature: patchMesh is null");
                return;
            }

            renderer = new ATTerrainRenderer(m_Settings.data);
            renderer.Init();
            renderPass = new ATTerrainRenderPass(m_Settings.data, renderer);
            renderPass.renderPassEvent = m_Settings.RenderEvent;
            preRenderPass = new ATTerrainPreRenderPass(m_Settings.data, renderer);
            preRenderPass.renderPassEvent = m_Settings.PreRenderEvent;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (renderPass != null && preRenderPass != null)
            {
                renderer.EnqueuePass(renderPass);
                renderer.EnqueuePass(preRenderPass);
            }
        }

        protected override void Dispose( bool disposing )
        {
            if (disposing)
            {
                renderPass?.Dispose();
                preRenderPass?.Dispose();
                renderer?.Dispose();
            }
        }
    }
    
}
