using System.Collections;
using System.Collections.Generic;
using System.Drawing.Drawing2D;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace ATGrassCloud
{

    public class ATGrassCascade
    {

        private List<ShaderTagId> shaderTagsList = new List<ShaderTagId>();
        private List<ShaderTagId> heightMapTagList = new List<ShaderTagId>();
        private RTHandle heightRT;
        public RTHandle GetHeightRT()
        {
            return heightRT;
        }
        private RTHandle heightDepthRT;
        private RTHandle maskRT;
        private RTHandle colorRT;
        private RTHandle slopeRT;

        private ComputeBuffer grassDataBuffer;

        private GrassData[] grassDataInit;
        private ComputeBuffer grassCntBuffer;
        private ComputeBuffer argsBuffer;
        private uint[] args;
        private Mesh cachedGrassMesh;
        private int[] grassCntData;

        public ATGrassCascadeData data;

        private GrassPrePass pass;

        private ComputeShader computeShader;

        // ====== Material ======
        
        private Material heightMapMat;
        private Material grassMat;

        public int GetGrassCount()
        {
            return grassCntData[0];
        }

        public struct GrassData
        {
            public Vector3 position;
            public float windOffset;
            public float rand;
        }

        public GrassData[] GetGrassData()
        {
            if (grassDataBuffer == null)
                return null;

            Debug.Log("GrssCount : " + GetGrassCount());
            GrassData[] datas = new GrassData[GetGrassCount()];
            grassDataBuffer.GetData(datas);
            return datas;
        }

        public ATGrassCascade( ATGrassCascadeData data , GrassPrePass pass , ComputeShader computeShader)
        {
            this.data = data;
            this.pass = pass;
            this.computeShader = computeShader;
            calculatePositionKernel = computeShader.FindKernel("CS_CalculatePosition");
            
            shaderTagsList = new List<ShaderTagId>();
            shaderTagsList.Add(new ShaderTagId("SRPDefaultUnlit"));
            shaderTagsList.Add(new ShaderTagId("UniversalForward"));
            shaderTagsList.Add(new ShaderTagId("UniversalForwardOnly"));
            heightMapTagList = new List<ShaderTagId>();
            heightMapTagList.Add(new ShaderTagId("HeightMap"));
            heightMapTagList.Add(new ShaderTagId("SRPDefaultUnlit"));
            heightMapTagList.Add(new ShaderTagId("UniversalForward"));

            grassCntData = new int[2];
            args = new uint[5];

            grassMat = null;
            if ( data.ProcedualMeshMaterial == null )
            {
                return;
            }

            grassMat = new Material(data.ProcedualMeshMaterial);
            // copy material property
            grassMat.CopyPropertiesFromMaterial(data.ProcedualMeshMaterial);

            grassDataInit = new GrassData[data.GetMaxInstanceCount()];
            for (int i = 0; i < grassDataInit.Length; i++)
            {
                grassDataInit[i].position = Vector3.negativeInfinity; 
                grassDataInit[i].windOffset = 0;
                grassDataInit[i].rand = Random.Range(0.0f, 1.0f);
            }
        }

        public void SetHeightMapMaterial(Material mat)
        {
            heightMapMat = new Material(mat);
        }

        public void Init()
        {
            int textureSize = data.GetTextureSize();

            var desc = new RenderTextureDescriptor(
                textureSize, 
                textureSize, 
                RenderTextureFormat.RGFloat,
                0
                );
            desc.msaaSamples = 1;

            var depthDesc = desc;
            depthDesc.depthBufferBits = 32;
            depthDesc.colorFormat = RenderTextureFormat.RFloat;

            RenderingUtils.ReAllocateIfNeeded(ref heightRT, desc, FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref heightDepthRT, depthDesc, FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref maskRT, desc, FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref colorRT, desc, FilterMode.Bilinear);
            RenderingUtils.ReAllocateIfNeeded(ref slopeRT, desc, FilterMode.Bilinear);

            // pass.ConfigureTarget(heightRT, heightDepthRT);
            // pass.ConfigureClear(ClearFlag.All, Color.black);
            
            var maxBufferCount = data.GetMaxInstanceCount();
            grassDataBuffer?.Release();
            grassDataBuffer = new ComputeBuffer(maxBufferCount, sizeof(float) * 5 , ComputeBufferType.Append);
            grassCntBuffer?.Release();
            grassCntBuffer = new ComputeBuffer(2, sizeof(int), ComputeBufferType.Raw);
            argsBuffer?.Release();
            argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

            
            args[0] = (uint)GetGrassMeshCache().GetIndexCount(0);
            args[1] = 1; // this value is changing in UpdateGrassData
            args[2] = (uint)GetGrassMeshCache().GetIndexStart(0);
            args[3] = (uint)GetGrassMeshCache().GetBaseVertex(0);
            args[4] = 0;
            argsBuffer.SetData(args);
        }

        public void DrawHeightMap(ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd , bool resetRenderTarget = true )
        {
            if ( !data.isRender || !heightMapMat)
            {
                return;
            }
            using (new ProfilingScope(cmd, new ProfilingSampler("HeightMap Cascade " + data.cascadeName)))
            {
                Camera camera = Camera.main;
                Matrix4x4 viewMatrix, projMatrix;
                GrassPrePass.CalculateTopDownCameraData(
                    camera,
                    data.GetMaxDistance(),
                    data.GetSnapDistance(),
                    out viewMatrix, out projMatrix);

                cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);//Update the camera marticies

                cmd.SetRenderTarget(heightRT, heightDepthRT);
                cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                var cameraBounds = GrassPrePass.CalculateCameraBounds(camera, data.GetMaxDistance());
                //Replace the material of the objects with the "heightMapLayer" and render them
                var drawSetting = pass.CreateDrawingSettings(heightMapTagList, ref renderingData, SortingCriteria.QuantizedFrontToBack);

            
                heightMapMat.SetVector("_BoundsYMinMax", new Vector2(cameraBounds.min.y, cameraBounds.max.y));
                drawSetting.overrideMaterial = heightMapMat;
                var filterSetting = new FilteringSettings(RenderQueueRange.all, data.heightMapLayer);
                context.DrawRenderers(renderingData.cullResults, ref drawSetting, ref filterSetting);

            }

            if ( resetRenderTarget)
            {
                cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.camera.projectionMatrix);
                cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,BuiltinRenderTextureType.Depth);
                
            }

        }

        public void CalculateGrassData(ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd )
        {
            if ( !data.isRender )
            {
                return;
            }


            using (new ProfilingScope(cmd, new ProfilingSampler("Calculate Grass Data " + data.cascadeName)))
            {
                var camera = Camera.main;
                var cameraBounds = GrassPrePass.CalculateCameraBounds(camera, data.GetMaxDistance());
                var tileSize = data.TileSize;
                var maxBufferCount = data.GetMaxInstanceCount();
                var centerPos = GrassPrePass.GetCenterPosition(camera , data.GetSnapDistance());
                var heightMapData = GrassPrePass.GetDrawTopDownTextureData(cameraBounds , data.GetMaxDistance() , data.GetSnapDistance());
                
                Matrix4x4 projectionMatrix = GL.GetGPUProjectionMatrix( camera.projectionMatrix , false);
                Matrix4x4 viewProjectionMatrix = projectionMatrix * camera.worldToCameraMatrix;


                Vector2Int tileNumber = new Vector2Int(Mathf.CeilToInt(cameraBounds.size.x / tileSize), Mathf.CeilToInt(cameraBounds.size.z / tileSize));
                Vector2Int tileStartIndex = new Vector2Int(Mathf.FloorToInt(cameraBounds.min.x / tileSize), Mathf.FloorToInt(cameraBounds.min.z / tileSize));

                // clean compute buffer
                // grassDataBuffer.SetData(grassDataInit);
                cmd.SetBufferCounterValue(grassDataBuffer, 0);

                // set data 
                cmd.SetComputeMatrixParam(computeShader, "_ViewProjectMatrix", viewProjectionMatrix);
                cmd.SetComputeVectorParam(computeShader, "_CascadeRange", data.GetRangeData() );
                cmd.SetComputeIntParam(computeShader, "_InstancePerTile", data.instancePerTile);
                cmd.SetComputeVectorParam(computeShader, "_BoundsMin", cameraBounds.min);
                cmd.SetComputeVectorParam(computeShader, "_BoundsMax", cameraBounds.max);
                cmd.SetComputeVectorParam(computeShader, "_CameraPosition", camera.transform.position);
                cmd.SetComputeVectorParam(computeShader, "_CenterPosition", centerPos);
                cmd.SetComputeVectorParam(computeShader, "_HeightMapData", heightMapData);
                cmd.SetComputeFloatParam(computeShader, "_TileSize", tileSize);
                cmd.SetComputeFloatParam(computeShader, "_DrawDistance", data.GetMaxDistance());
                cmd.SetComputeFloatParam(computeShader, "_SnapDistance", data.GetSnapDistance());
                cmd.SetComputeVectorParam(computeShader, "_TileNumer", new Vector4(tileNumber.x, tileNumber.y, 0, 0));
                cmd.SetComputeVectorParam(computeShader, "_TileStartIndex", new Vector4(tileStartIndex.x, tileStartIndex.y, 0, 0));
                cmd.SetComputeFloatParam(computeShader, "_OccludHeightOffset", data.OccludHeightOffset);
                cmd.SetComputeFloatParam(computeShader, "_EdgeFrustumCullingOffset", data.EdgeFrustumCullingOffset);
                cmd.SetComputeFloatParam(computeShader, "_NearPlaneOffset", data.NearPlaneOffset);
                cmd.SetComputeIntParam(computeShader, "_UseFrustumCulling", data.UseFrustumCulling ? 1 : 0);
                cmd.SetComputeIntParam(computeShader, "_UseDepthOcclusionCulling", data.UseDepthOcclusionCulling ? 1 : 0);
                cmd.SetComputeIntParam(computeShader, "_UseDistanceDensityCulling", data.distanceDensityCulling ? 1 : 0);
                cmd.SetComputeFloatParam(computeShader, "_FullDensityDistance", data.FullDensityDistance);

                // set texture 
                cmd.SetComputeTextureParam(computeShader, calculatePositionKernel, "_HeightMapRT", heightRT);
                cmd.SetComputeTextureParam(computeShader, calculatePositionKernel, "_GrassMaskMapRT", maskRT);
                
                // Output 
                cmd.SetComputeBufferParam(computeShader, calculatePositionKernel, "_GrassOutput", grassDataBuffer);

                cmd.DispatchCompute(computeShader,
                    calculatePositionKernel,
                    Mathf.CeilToInt((float)tileNumber.x / 8),
                    Mathf.CeilToInt((float)tileNumber.y / 8),
                    1);
                
                cmd.CopyCounterValue(grassDataBuffer, argsBuffer, 1 * sizeof(uint));
                cmd.CopyCounterValue(grassDataBuffer, grassCntBuffer, 1 * sizeof(int));

                // For debug 
                // ComputeBuffer.CopyCount(grassDataBuffer, grassCntBuffer, 0);
                // grassCntBuffer.GetData(grassCntData);
                // Debug.Log("GrassData Count From CPU:" + grassCntData[0]);
                // Debug.Log("GrassCnt Count From GPU:" + grassCntData[1]);

            
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

            }

        }


        public void RenderGrass(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd)
        {
            if ( !data.isRender )
            {
                return;
            }
            using (new ProfilingScope(cmd, new ProfilingSampler("Render Grass " + data.cascadeName)))
            {
               if ( data.renderType == ATGrassRenderType.ProcedualMesh )
               {
                    RenderProcedualMesh(context, ref renderingData, cmd);
               }        
                

            }
        }


        public void RenderProcedualMesh(ScriptableRenderContext context, ref RenderingData renderingData, CommandBuffer cmd)
        {
            if (grassMat == null || data.ProcedualMeshMaterial == null)
            {
                return;
            }

            Camera camera = Camera.main;
            Bounds cameraBounds = GrassPrePass.CalculateCameraBounds(camera, data.GetMaxDistance());
            var mapData = GrassPrePass.GetDrawTopDownTextureData(cameraBounds , data.GetMaxDistance() , data.GetSnapDistance());

            if ( data.updateMaterial)
                grassMat.CopyPropertiesFromMaterial(data.ProcedualMeshMaterial);

            grassMat.EnableKeyword("_PROCEDURAL_MESH");
            grassMat.SetVector("_MapData", mapData);
            grassMat.SetBuffer("_GrassData", grassDataBuffer);
            grassMat.SetVector("_CascadeRange", data.GetRangeData());

            cmd.SetGlobalVector("unity_LightData", new Vector4(1.0f,4.0f,1.0f,0));
            cmd.DrawMeshInstancedIndirect(
                GetGrassMeshCache(),
                0 ,
                grassMat,
                0,
                argsBuffer,
                0

            );

            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
        }



    int oldSubdivision = -1;
    public Mesh GetGrassMeshCache() //Code to generate the grass blade mesh based on the subdivision value
    {
        var N = data.procedualMeshSegments; // 细分段数


        if (!cachedGrassMesh || oldSubdivision != N)
        {

            cachedGrassMesh = new Mesh();

                // === 1. 生成顶点 ===
                // 总顶点数：每层2个顶点 × (N+1) 层（y=0 到 y=N/(N+1)） + 顶部1个
                int totalVertices = 2 * (N + 1) + 1;
            Vector3[] vertices = new Vector3[totalVertices];

            int vertexIndex = 0;
            // 生成从 y=0 到 y=N/(N+1) 的左右顶点
            for (int i = 0; i <= N; i++)
            {
                float y = (float)i / (N + 1) ;
                vertices[vertexIndex++] = new Vector3(-0.5f , y, 0); // 左
                vertices[vertexIndex++] = new Vector3(0.5f, y, 0);   // 右
            }
            // 顶部尖端
            vertices[vertexIndex] = new Vector3(0, 1, 0); // 索引 = 2*(N+1)

            // === 2. 生成三角形索引 ===
            int totalTriangles = N * 2 + 1; // 每段2个三角形 + 顶部1个
            int[] triangles = new int[totalTriangles * 3];

            int triIndex = 0;
            // 生成中间段的四边形（两个三角形）
            for (int i = 0; i < N; i++)
            {
                int bottomLeft  = i * 2;     // 当前层左
                int bottomRight = i * 2 + 1; // 当前层右
                int topLeft     = (i + 1) * 2;     // 下一层左
                int topRight    = (i + 1) * 2 + 1; // 下一层右

                // 三角形 1: BL -> TR -> BR
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topRight;
                triangles[triIndex++] = bottomRight;

                // 三角形 2: BL -> TL -> TR
                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = topRight;
            }

            // 顶部三角形
            int topVertex = 2 * (N + 1);           // 顶端 (0,1)
            int lastTopLeft = N * 2;               // 最后一层左
            int lastTopRight = N * 2 + 1;          // 最后一层右

            triangles[triIndex++] = lastTopLeft;
            triangles[triIndex++] = topVertex;
            triangles[triIndex++] = lastTopRight;

            // === 3. 构建网格 ===
            cachedGrassMesh.SetVertices(vertices);
            cachedGrassMesh.SetTriangles(triangles, 0);
            cachedGrassMesh.RecalculateNormals();
            cachedGrassMesh.RecalculateBounds();

            oldSubdivision = N;
        }

        return cachedGrassMesh;
    }

        public void Dispose()
        {
            heightRT?.Release();
            heightDepthRT?.Release();
            maskRT?.Release();
            colorRT?.Release();
            slopeRT?.Release();
            grassDataBuffer?.Release();
            grassCntBuffer?.Release();
            argsBuffer?.Release();
        }

        public readonly int calculatePositionKernel ;

    }

}