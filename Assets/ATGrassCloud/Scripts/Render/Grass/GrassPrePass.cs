using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class GrassPrePass : ScriptableRenderPass 
    {
        public ATGrassData grassData;
        public List<ATGrassCascadeData> grassCascadeDatas;

        private List<ATGrassCascade> cascades = new List<ATGrassCascade>();
        public List<ATGrassCascade> CascadesList { get { return cascades; } }

        private Material heightMapMat;
        private bool generateHeightMat;
        private ComputeShader computeShader;

        public ATGrassCascade GetCascade(int index)
        {
            if ( index < 0 || index >= cascades.Count )
            {
                Debug.LogError("GrassRenderPass(GetCascade): index out of range");
                return null;
            }
            return cascades[index];
        }

        public GrassPrePass(ATGrassData grassData)
        {
            if ( grassData == null )
            {
                Debug.LogError("GrassRenderPass(Constrcutor): grassData is null");
                return;
            }

            if ( grassData.generateHeightMat && grassData.heightMapMat == null )
            {
                Debug.LogError("GrassRenderPass(Constrcutor): should generate data in GrassRenderPass but  heightMapMat is null");
                return;
            }

            if ( grassData.computeShader == null )
            {
                Debug.LogError("GrassRenderPass(Constrcutor): computeShader is null");
                return;
            }
            this.grassData = grassData;
            grassCascadeDatas = grassData.cascadeDataList;

            this.heightMapMat = grassData.heightMapMat;
            this.generateHeightMat = grassData.generateHeightMat;
            this.computeShader = grassData.computeShader;
    
            cascades.Clear();
            for (int i = 0; i < grassCascadeDatas.Count; i++)
            {
                ATGrassCascadeData data = grassCascadeDatas[i];
                ATGrassCascade cascade = new ATGrassCascade(data , this , computeShader);

                if ( generateHeightMat  )
                {
                    cascade.SetHeightMapMaterial(heightMapMat);
                }
                cascades.Add(cascade);
            }

        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            for (int i = 0; i < cascades.Count; i++)
            {
                ATGrassCascade cascade = cascades[i];
                cascade.Init(cmd , ref renderingData);

            }
        }




        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //Now to render the textures we need we have two ways :
            //- Having a second camera in our scene that is looking from above and renders the necessary data (which is expensive)
            //- Manipulating the actuall main camera to render objects from above by changing the view and projection matricies (which is faster and the one I'm using here)
            //I took this technic from Colin Leung (NiloCat) repo
            //You can check it here (more detailed): https://github.com/ColinLeung-NiloCat/UnityURP-MobileDrawMeshInstancedIndirectExample/blob/master/Assets/URPMobileGrassInstancedIndirectDemo/InstancedIndirectGrass/Core/GrassBending/GrassBendingRTPrePass.cs

            if (heightMapMat == null || computeShader == null)
                return;

            CommandBuffer cmd = CommandBufferPool.Get("[AT] GrassPrePass");

            if (generateHeightMat)
            {
                // using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Grass Height Map RT")))
                {
                    // for each cascade do generate height map
                    for (int i = 0; i < cascades.Count; i++)
                    {
                        ATGrassCascade cascade = cascades[i];
                        cascade.DrawHeightMap(context , ref renderingData , cmd , false );
                    }
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }


            // set render target back to default 
            cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.camera.projectionMatrix);
            cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

            // using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Calculate Grass Data")))
            {
                for (int i = 0; i < cascades.Count; i++)
                {
                    ATGrassCascade cascade = cascades[i];
                    cascade.CalculateGrassData(context , ref renderingData , cmd );
                }
            }


            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);

        }




        public void Dispose()
        {
            for (int i = 0; i < cascades.Count; i++)
            {
                ATGrassCascade cascade = cascades[i];
                cascade.Dispose();
            }
        }

    }
}