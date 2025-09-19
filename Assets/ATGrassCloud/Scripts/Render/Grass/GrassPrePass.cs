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
                using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Grass Height Map RT")))
                {
                    // for each cascade do generate height map
                    for (int i = 0; i < cascades.Count; i++)
                    {
                        ATGrassCascade cascade = cascades[i];
                        cascade.DrawHeightMap(context , ref renderingData , cmd , false );
                    }
                }
            }


            // set render target back to default 
            cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.camera.projectionMatrix);
            cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);

            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Calculate Grass Data")))
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

        // return Vector4 in format : (centerPos.x , centerPos.y , size , 1.0f / size )
        // size is the draw distance + snap distance
        public static Vector4 GetDrawTopDownTextureData( Camera camera , float drawDistance , float snapDistance )
        {
            Bounds cameraBounds = CalculateCameraBounds(camera, drawDistance + snapDistance);
            return GetDrawTopDownTextureData(cameraBounds, drawDistance, snapDistance);

            // var centerPos = GetCenterPosition(camera, snapDistance);
            // var size = drawDistance + snapDistance;
            // return new Vector4(centerPos.x , centerPos.y , size , 1.0f / Mathf.Max( 0.00001f , size));
        }

        public static Vector4 GetDrawTopDownTextureData( Bounds cameraBounds , float drawDistance , float snapDistance  )
        {
            Vector3 centerPos = GetCenterPosition(cameraBounds.center,snapDistance);
            float size =  (drawDistance + snapDistance) ;
            return new Vector4(centerPos.x , centerPos.y , size , 1.0f / Mathf.Max( 0.00001f , size));
        }

        public static Vector3 SnapCameraPosition( Vector3 pos  , float snapDistance )
        {
            Vector3 snappedPos = pos;
            snappedPos.x = Mathf.Round(pos.x / snapDistance) * snapDistance;
            snappedPos.z = Mathf.Round(pos.z / snapDistance) * snapDistance;
            return snappedPos;
        }


        
        //First thing is to calculate the new position of the camera
        //The "centerPos" refer to the XZ position of the camera while the Y position is the max.y of the calculated bounds
        //You can see that we are moving the camera in steps, cause we want the textures to not get updated until we move a certain threshold
        //if we let the camera move a lot we gonna have instability issues and a lot of flikering so we try to minimize that as much as possible
        // Vector2 centerPos = new Vector2(Mathf.Floor(camera.transform.position.x / textureUpdateThreshold) * textureUpdateThreshold, Mathf.Floor(Camera.main.transform.position.z / textureUpdateThreshold) * textureUpdateThreshold);
        public static Vector2 GetCenterPosition( Camera camera , float snapDistance )
        {
            return GetCenterPosition(camera.transform.position , snapDistance);
        }

        public static Vector2 GetCenterPosition( Vector3 pos , float snapDistance )
        {
            Vector3 snapPos = SnapCameraPosition(pos , snapDistance);
            Vector2 centerPos = new(snapPos.x, snapPos.z);
            return centerPos;
        }
        

        static public void CalculateTopDownCameraData( Camera camera , float drawDistance, float snapDistance , out Matrix4x4 viewMatrix , out Matrix4x4 projectionMatrix)
        {
            Bounds cameraBounds = CalculateCameraBounds(camera, drawDistance);
            Vector4 topDownData = GetDrawTopDownTextureData(cameraBounds, drawDistance, snapDistance);
            Vector3 cameraPos = new Vector3(topDownData.x, cameraBounds.max.y + 10f, topDownData.y);
            float size = topDownData.z;

            viewMatrix = Matrix4x4.TRS(cameraPos, Quaternion.LookRotation(-Vector3.up), new Vector3(1, 1, -1)).inverse;
            projectionMatrix = Matrix4x4.Ortho(- size , size, -size, size, 0, cameraBounds.size.y * 1.5f );
        }

        static public Bounds CalculateCameraBounds(Camera camera, float drawDistance)
        {
            Vector3 ntopLeft = camera.ViewportToWorldPoint(new Vector3(0, 1, camera.nearClipPlane));
            Vector3 ntopRight = camera.ViewportToWorldPoint(new Vector3(1, 1, camera.nearClipPlane));
            Vector3 nbottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, camera.nearClipPlane));
            Vector3 nbottomRight = camera.ViewportToWorldPoint(new Vector3(1, 0, camera.nearClipPlane));

            Vector3 ftopLeft = camera.ViewportToWorldPoint(new Vector3(0, 1, drawDistance));
            Vector3 ftopRight = camera.ViewportToWorldPoint(new Vector3(1, 1, drawDistance));
            Vector3 fbottomLeft = camera.ViewportToWorldPoint(new Vector3(0, 0, drawDistance));
            Vector3 fbottomRight = camera.ViewportToWorldPoint(new Vector3(1, 0, drawDistance));

            float[] xValues = new float[] { ftopLeft.x, ftopRight.x, ntopLeft.x, ntopRight.x, fbottomLeft.x, fbottomRight.x, nbottomLeft.x, nbottomRight.x };
            float startX = xValues.Max();
            float endX = xValues.Min();

            float[] yValues = new float[] { ftopLeft.y, ftopRight.y, ntopLeft.y, ntopRight.y, fbottomLeft.y, fbottomRight.y, nbottomLeft.y, nbottomRight.y };
            float startY = yValues.Max();
            float endY = yValues.Min();

            float[] zValues = new float[] { ftopLeft.z, ftopRight.z, ntopLeft.z, ntopRight.z, fbottomLeft.z, fbottomRight.z, nbottomLeft.z, nbottomRight.z };
            float startZ = zValues.Max();
            float endZ = zValues.Min();

            Vector3 center = new Vector3((startX + endX) / 2, (startY + endY) / 2, (startZ + endZ) / 2);
            Vector3 size = new Vector3(Mathf.Abs(startX - endX), Mathf.Abs(startY - endY), Mathf.Abs(startZ - endZ));

            Bounds bounds = new Bounds(center, size);
            bounds.Expand(1);
            return bounds;
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