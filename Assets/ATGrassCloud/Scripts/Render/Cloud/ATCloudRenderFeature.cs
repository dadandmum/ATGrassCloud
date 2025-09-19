using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ATGrassCloud;
using Sirenix.OdinInspector;



public class ATCloudRenderFeature : ScriptableRendererFeature
{
    [InlineEditor]
    public ATCloudData cloudData;
    [SerializeField] private RenderPassEvent renderPassEvent;

    private ATCloudRenderPass cloudRenderPass;

    public override void Create()
    {
        cloudRenderPass = new ATCloudRenderPass(cloudData);
        cloudRenderPass.renderPassEvent = renderPassEvent;
    }
    

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(cloudRenderPass);
    }

    protected override void Dispose(bool disposing)
    {
        if ( disposing )
        {
            cloudRenderPass.Dispose();

        }
    }

}
