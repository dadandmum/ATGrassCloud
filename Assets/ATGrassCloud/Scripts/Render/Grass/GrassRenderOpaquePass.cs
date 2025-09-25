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
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
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
        }


        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }

        public override void OnFinishCameraStackRendering(CommandBuffer cmd)
        {
        }

        public void Dispose()
        {
        }
    }
}