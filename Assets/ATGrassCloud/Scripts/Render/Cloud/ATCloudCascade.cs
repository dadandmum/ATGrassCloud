using System.Collections;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using Microsoft.SqlServer.Server;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class ATCloudCascade 
    {
        private ATCloudCascadeData cascadeData;

        private ATCloudRenderPass cloudRenderPass;
        private ComputeShader computeShader;

        private Material cloudMat;

        private RTHandle CloudSDFRT;

        private RTHandle cloudRenderRT;

        private ComputeBuffer cloudObjectBuffer;

        private int cloudObjectCount;

        public int GetCascadeOrder()
        {
            if ( cascadeData == null )
            {
                return 0;
            }

            return cascadeData.cascadeLevel;

        }
        public ATCloudCascade( 
            ATCloudCascadeData data , 
            ATCloudRenderPass pass , 
            ComputeShader computeShader)

        {
            cascadeData = data;
            cloudRenderPass = pass;
            this.computeShader = computeShader;

            if (cascadeData.cloudRenderMaterial != null)
            {
                cloudMat = new Material(cascadeData.cloudRenderMaterial);
                cloudMat.CopyPropertiesFromMaterial(cascadeData.cloudRenderMaterial);
            }

        }


        public void Init(CommandBuffer cmd, ref RenderingData renderingData, List<ATCloudSceneObject> cloudObjects)
        {
            if (cascadeData == null || !cascadeData.isRender )
            {
                return;
            }

            // Set up Cloud SDF Render Texture
            var desc = renderingData.cameraData.cameraTargetDescriptor;

            desc.depthBufferBits = 0;
            desc.colorFormat = RenderTextureFormat.ARGBFloat;
            desc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(ref CloudSDFRT, desc, FilterMode.Bilinear, name: "CloudSDFRT");

            // Set up Cloud Scene Objects 
            var camera = renderingData.cameraData.camera;
            var cameraPos = camera.transform.position;
            var mainCam = Camera.main;
            var mainCamPos = (mainCam != null ) ? mainCam.transform.position : Vector3.zero;

            List<ATCloudSceneObject> cloudObjectsInCascade = new List<ATCloudSceneObject>();
            for (int i = 0; i < cloudObjects.Count; i++)
            {
                var pos = cloudObjects[i].transform.position;
                var objRadius = cloudObjects[i].GetCloudObjectBoundingRadius();
                // TODO: deal frustum culling

                // deal distance culling
                float distanceXZ = Vector3.Distance(new Vector3(mainCamPos.x, 0, mainCamPos.z), new Vector3(pos.x, 0, pos.z));
                
                if ( distanceXZ > cascadeData.cascadeOutterRange + objRadius || distanceXZ < cascadeData.cascadeInnerRange - objRadius )
                {
                    continue;
                }

                cloudObjectsInCascade.Add(cloudObjects[i]);

            }
            // set up buffer 
            if (cloudObjectBuffer != null)
            {
                cloudObjectBuffer.Release();
            }
            if (cloudObjectsInCascade.Count == 0)
            {
                int bufferSize = (3 + 4 + 3 + 1 + 1 + 4);
                cloudObjectBuffer = new ComputeBuffer(1, sizeof(float) * bufferSize);
                ATCloudObjectBuffer[] cloudObjectData = new ATCloudObjectBuffer[1];
                cloudObjectData[0] = ATCloudSceneObject.GetDefaultObjectBuffer();
                cloudObjectBuffer.SetData(cloudObjectData);
                cloudObjectCount = 0;

            }else
            {
                int bufferSize = (3 + 4 + 3 + 1 + 1 + 4);
                cloudObjectBuffer = new ComputeBuffer(cloudObjectsInCascade.Count, sizeof(float) * bufferSize);

                float[] cloudObjectData = new float[cloudObjectsInCascade.Count * bufferSize];
                for (int i = 0; i < cloudObjectsInCascade.Count; i++)
                {
                    var cloudObject = cloudObjectsInCascade[i];
                    ATCloudObjectBuffer objBuffer = cloudObject.GetObjectBuffer();
                    cloudObjectData[i * bufferSize] = objBuffer.position.x; 
                    cloudObjectData[i * bufferSize + 1] = objBuffer.position.y;
                    cloudObjectData[i * bufferSize + 2] = objBuffer.position.z;
                    cloudObjectData[i * bufferSize + 3] = objBuffer.rotation.x;
                    cloudObjectData[i * bufferSize + 4] = objBuffer.rotation.y;
                    cloudObjectData[i * bufferSize + 5] = objBuffer.rotation.z;
                    cloudObjectData[i * bufferSize + 6] = objBuffer.rotation.w;
                    cloudObjectData[i * bufferSize + 7] = objBuffer.scale.x;
                    cloudObjectData[i * bufferSize + 8] = objBuffer.scale.y;
                    cloudObjectData[i * bufferSize + 9] = objBuffer.scale.z;
                    cloudObjectData[i * bufferSize + 10] = objBuffer.objectType;
                    cloudObjectData[i * bufferSize + 11] = objBuffer.boundRadius;
                    cloudObjectData[i * bufferSize + 12] = objBuffer.param.x;
                    cloudObjectData[i * bufferSize + 13] = objBuffer.param.y;
                    cloudObjectData[i * bufferSize + 14] = objBuffer.param.z;
                    cloudObjectData[i * bufferSize + 15] = objBuffer.param.w;
                }
                cloudObjectBuffer.SetData(cloudObjectData);
                cloudObjectCount = cloudObjectsInCascade.Count;
                
            }

            // set up cloud noise 
            if (cascadeData.cloudNoiseData != null )
            {
                cascadeData.cloudNoiseData.UpdateNoise();
            }

        }

        
        public void DrawSDF(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, bool resetRenderTarget = true)
        {

        }

        public void RenderCloud(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd, bool resetRenderTarget = true)
        {
            if ( cascadeData == null || !cascadeData.isRender )
            {
                return;
            }

            if (cloudMat == null || cascadeData.cloudRenderData == null || cascadeData.cloudNoiseData == null)
            {
                return;
            }

            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Render Cloud " + cascadeData.cascadeName)))
            {

                cmd.SetGlobalBuffer("_CloudObjectBuffer", cloudObjectBuffer);
                cmd.SetGlobalInt("_CloudObjectCount", cloudObjectCount);
                cmd.SetGlobalTexture("_CustomDepthFull", renderingData.cameraData.renderer.cameraDepthTargetHandle);
                cmd.SetGlobalTexture("_CameraDepthAttachment", renderingData.cameraData.renderer.cameraDepthTargetHandle);
                cmd.SetGlobalTexture("_NoiseTex", cascadeData.cloudNoiseData.NoiseTex);

                cascadeData.SetMaterialParameter(cloudMat);

                var target = renderingData.cameraData.renderer.cameraColorTargetHandle;

                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, cloudMat, 0, 0);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

            }


        }
        
        public void Dispose()
        {
            CloudSDFRT?.Release();
            cloudRenderRT?.Release();
            cloudObjectBuffer?.Release();

        }



    }
}
