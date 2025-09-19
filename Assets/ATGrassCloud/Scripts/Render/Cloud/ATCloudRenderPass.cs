using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class ATCloudRenderPass : ScriptableRenderPass
    {
        public ATCloudData cloudData;

        private List<ATCloudCascade> cascades = new List<ATCloudCascade>();

        private ComputeShader computeShader;

        public ATCloudCascade GetCascade(int index)
        {
            if ( index < 0 || index >= cascades.Count )
            {
                Debug.LogError("ATCloudRenderPass(GetCascade): index out of range");
                return null;
            }
            return cascades[index];
        }


        public ATCloudRenderPass(ATCloudData cloudData)
        {
            if ( cloudData == null )
                return;

            this.cloudData = cloudData;

            cascades.Clear();
            this.computeShader = cloudData.computeShader;

            for (int i = 0; i < cloudData.cascadeDataList.Count; i++)
            {
                ATCloudCascadeData data = cloudData.cascadeDataList[i];
                ATCloudCascade cascade = new ATCloudCascade(data, this, cloudData.computeShader);
                cascades.Add(cascade);
            }

            cascades.Sort((a, b) => b.GetCascadeOrder().CompareTo(a.GetCascadeOrder()));

        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if ( cascades == null )
                return;

            List<ATCloudSceneObject> cloudObjects = new List<ATCloudSceneObject>();
            
            if ( ATCloudObjectManager.Instance != null )
            {
                cloudObjects.AddRange(ATCloudObjectManager.Instance.GetCloudObjects());

            }

            for (int i = 0; i < cascades.Count; i++)
            {
                cascades[i].Init(cmd, ref renderingData, cloudObjects);

            }

        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ( cascades == null )
                return;
                
            if (cloudData == null)
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("[AT] Cloud Render Pass");


            using ( new ProfilingScope(cmd, new ProfilingSampler("[AT] Render Cloud")))
            {
                for (int i = 0; i < cascades.Count; i++)
                {
                    cascades[i].RenderCloud(context, ref renderingData, cmd);
                }
            }


            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
            
        }

        public void Dispose()
        {
            if ( cascades == null )
                return;
            for (int i = 0; i < cascades.Count; i++)
            {
                cascades[i].Dispose();
            }

        }

    }



}