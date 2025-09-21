using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class StaticGIPass : ScriptableRenderPass
{
    private const int SH_COEFF_COUNT = 9; // L0 + L1 + L2

    private ATGIData data;
    public StaticGIPass(ATGIData data)
    {
        this.data = data;
    }

    public void Setup()
    {
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if ( data == null )
        {
            return;
        }

        CommandBuffer cmd = CommandBufferPool.Get("AT Static GI Pass");
        // 可选：将 SH 数据写入纹理或 ComputeBuffer 用于 GPU 实时 GI

        // 写入 SH 系数到全局变量
        for (int i = 0; i < SH_COEFF_COUNT; i++)
        {
            cmd.SetGlobalVector($"AT_SH_{i}", data.AT_SH_Params[i]);
        }
        

        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);

    }

    public override void FrameCleanup(CommandBuffer cmd)
    {
        // 清理资源（如有 RenderTarget 分配）
    }
}