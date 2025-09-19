using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class GrassRenderOpaquePass : ScriptableRenderPass
    {
        public List<ATGrassCascadeData> grassCascadeDatas;
        public ATGrassData grassData;

        private GrassPrePass grassRenderPass;

        public GrassRenderOpaquePass(ATGrassData grassData, GrassPrePass grassRenderPass)
        {
            if (grassData == null)
            {
                Debug.LogError("GrassRenderPass(Constrcutor): grassData is null");
                return;
            }

            if (grassData.generateHeightMat && grassData.heightMapMat == null)
            {
                Debug.LogError("GrassRenderPass(Constrcutor): should generate data in GrassRenderPass but  heightMapMat is null");
                return;
            }

            if (grassData.computeShader == null)
            {
                Debug.LogError("GrassRenderPass(Constrcutor): computeShader is null");
                return;
            }

            this.grassData = grassData;
            grassCascadeDatas = grassData.cascadeDataList;
            this.grassRenderPass = grassRenderPass;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // 可选：配置渲染目标或临时纹理
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            // 如果需要配置渲染目标，例如切换 RenderTarget
            // 当前使用场景默认目标，无需配置
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Grass Render")))
            {
                for (int i = 0; i < grassRenderPass.CascadesList.Count; i++)
                {
                    ATGrassCascade cascade = grassRenderPass.CascadesList[i];
                    cascade.RenderGrass(context , ref renderingData , cmd );
                }
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

        private void UpdateArgsBuffer(CommandBuffer cmd)
        {
            // 如果 indexCount 或 baseVertex 会变，才需要更新
            // 否则可以只初始化一次
            // 当前假设静态，可注释掉或按需开启
            // cmd.SetComputeBufferParam(...) // 如果使用 Compute Shader 更新 args，才需要
        }


        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            // 不需要清理相机相关资源
        }

        public override void OnFinishCameraStackRendering(CommandBuffer cmd)
        {
            // 所有相机渲染完成后调用
        }

        public void Dispose()
        {
        }
    }
}