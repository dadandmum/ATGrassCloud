using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ATStaticGIRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public ATGIData data;
        public RenderPassEvent Event = RenderPassEvent.AfterRenderingTransparents;
    }

    [SerializeField] private Settings m_Settings = new Settings();
    private StaticGIPass m_Pass;

    public override void Create()
    {
        m_Pass = new StaticGIPass(m_Settings.data);
        m_Pass.renderPassEvent = m_Settings.Event;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_Pass);
    }
}