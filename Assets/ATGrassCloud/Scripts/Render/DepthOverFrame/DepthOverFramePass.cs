using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
    {
    public class DepthOverFramePass : ScriptableRenderPass
    {
        private Material m_material;
        private RTHandle m_temp;
        private int m_tempRTWidth, m_tempRTHeight;

        private string m_globalPreDepthTextureName;

        public DepthOverFramePass( string globalPreDepthTextureName )
        {
            m_globalPreDepthTextureName = globalPreDepthTextureName;

        }

        override public void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
        }

        public void Dispose()
        {
            m_temp?.Release();
        }

    }
}
