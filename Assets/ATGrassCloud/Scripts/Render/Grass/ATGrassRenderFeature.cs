using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ATGrassCloud;
using Sirenix.OdinInspector;

public class ATGrassRenderFeature : ScriptableRendererFeature
{
    [InlineEditor]
    public ATGrassData grassData;
    // [SerializeField] private Material heightMapMat;
    // [SerializeField] private ComputeShader computeShader;
    // [SerializeField] private bool generateHeightMat;
    [SerializeField] private RenderPassEvent renderPassEvent;
    [SerializeField] private RenderPassEvent drawOpaqueEvent;

    [System.Serializable]
    public struct GrassDebugData
    {
        public bool enableDebug;
        public int debugCascade;
        public bool isShowHeightMap;
    }

    [SerializeField] private GrassDebugData debugData;

    GrassPrePass grassDataPass;

    GrassRenderOpaquePass grassOpaquePass;

    GrassDebugPass grassDebugPass;

    public override void Create()
    {
        grassDataPass = new GrassPrePass(
            grassData );
        grassDataPass.renderPassEvent = renderPassEvent;
        grassOpaquePass = new GrassRenderOpaquePass(grassData, grassDataPass);
        grassOpaquePass.renderPassEvent = drawOpaqueEvent;
        grassDebugPass = new GrassDebugPass(debugData, grassDataPass, grassData);
        grassDebugPass.renderPassEvent = RenderPassEvent.AfterRendering;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(grassDataPass);
        renderer.EnqueuePass(grassOpaquePass);
        renderer.EnqueuePass(grassDebugPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            grassDataPass.Dispose();
            grassOpaquePass.Dispose();
        }
    }


}
