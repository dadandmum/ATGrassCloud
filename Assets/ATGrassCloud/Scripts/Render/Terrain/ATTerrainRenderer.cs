using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Mathematics;
using UnityEngine.Rendering.Universal;
using System.Runtime.CompilerServices;

namespace ATGrassCloud
{
    public class ATTerrainRenderer : System.IDisposable
    {
        // private ComputeShader computeShader;
        private ComputeShader traverseQuadTreeShader;
        private ComputeShader buildLodMapShader;
        private ComputeShader buildPatchesShader;   

        private ATTerrainRenderData data;

        // QuadTree 
        // // ------------------------------------------------------------------------
        // topLevelTileList
        //
        // A GPU-accessible buffer storing the root-level tile IDs of the terrain.
        // Represents the topmost (LOD 0) grid of tiles, forming the root nodes of 
        // the quadtree system. Each tile is identified by a uint2(x, z) coordinate.
        //
        // Properties:
        //   - Type:       ComputeBuffer of uint2
        //   - Count:      TopLevelTileCount虏
        //   - Layout:     Row-major (Z-major), index = z * N + x
        //
        // This buffer is initialized once and remains static unless terrain size changes.
        // ------------------------------------------------------------------------
        private ComputeBuffer topLevelTileList;
        /// <summary>
        /// Double-buffered compute buffers used for GPU-side quadtree traversal.
        /// This buffer is configured as an AppendStructuredBuffer<uint2>
        /// 
        /// Data per element:
        /// - x: Local tile column index (within current LOD grid)
        /// - y: Local tile row index (within current LOD grid)
        /// 
        /// These buffers operate in a ping-pong fashion:
        /// - One buffer (ping) acts as the **input** list of current-level nodes to process.
        /// - The other (pong) acts as the **output** list where child nodes are appended.
        /// 
        /// After each traversal level, the roles are swapped. This enables efficient,
        /// hierarchical culling and LOD selection entirely on the GPU.
        /// 
        /// Buffers are typically created with Append/Consume semantics and bound to
        /// compute shaders using AppendStructuredBuffer and ConsumeStructuredBuffer.
        /// </summary>
        private ComputeBuffer tileListPing;
        private ComputeBuffer tileListPong;
        /// <summary>
        /// A GPU buffer used to store the final list of active terrain tiles after quadtree traversal.
        /// 
        /// This buffer is configured as an AppendStructuredBuffer<uint3> and accumulates results 
        /// from the GPU-side traversal process. Each appended element represents a tile that has been 
        /// selected (and potentially subdivided) at a specific LOD level.
        /// 
        /// Data per element:
        /// - x: Local tile column index (within current LOD grid)
        /// - y: Local tile row index (within current LOD grid)
        /// - z: LOD level (0 = root level, increasing for finer detail)
        /// 
        /// Note: Every node that is subdivided or retained is appended exactly once during traversal.
        /// This list can be used for instanced rendering, culling validation, or indirect drawing.
        /// 
        /// After dispatch, the buffer's actual count must be retrieved using ComputeBuffer.CopyCount
        /// before being read back or passed to rendering commands.
        /// </summary>
        private ComputeBuffer finalTileListBuffer;
        /// <summary>
        /// A GPU-accessible structured buffer that stores metadata for each tile in the terrain system.
        /// 
        /// This buffer is declared as RWStructuredBuffer<TileDescriptor> in shaders and has a 
        /// fixed size of MaxTileCount, allowing O(1) random access by tile ID.
        /// 
        /// It is primarily used to track whether a tile has been expanded (subdivided) during 
        /// GPU-driven quadtree traversal. This prevents redundant processing and ensures correct 
        /// hierarchical culling and LOD generation.
        /// 
        /// This buffer persists across traversal iterations and may be reset at the start of a new frame.
        /// </summary>
        private ComputeBuffer tileDescriptors;
        // Indirect arguments buffer for DispatchIndirect.
        // Updated per LOD level to reflect number of active nodes to process.
        // Format: [threadGroupX, threadGroupY, threadGroupZ]
        private ComputeBuffer travQTIndirectArgsBuffer;

        // A buffer for debug
        // record the final count of tile
        private ComputeBuffer traverseQuadTreeCntBuffer;
        private ComputeBuffer finalTileCntBuffer;

        private ComputeBuffer culledPatchBuffer;
        private ComputeBuffer patchIndirectArgsBuffer;
        private ComputeBuffer culledPatchCntBuffer;
        private ComputeBuffer patchBoundsBuffer;
        private ComputeBuffer patchBoundsIndirectArgsBuffer;
        private ComputeBuffer buildPatchIndirectArgsBuffer;


        private RTHandle lodMapRT;
        private int lodMapSize = 1;
        private RTHandle heightMapRT;
        private RTHandle minMaxHeightMapRT;
        private bool IsRTInited = false;

        public int[] traverseQuadTreeCntData = new int[ATTerrainRenderData.MAX_TERRAIN_LOD_LEVEL];
        public int[] finalTileCntData = new int[1];
        public int[] culledPatchCntData = new int[1];
        public uint[] tileDescriptorsData;

        // material
        public Material renderMaterail;

        // kernel id 
        public int traverseQuadTreeKernelID = 0;
        public int buildLodMapKernelID = 0;
        public int buildPatchesKernelID = 0;

        private int _maxTileBufferSize = 200;
        private int _tempNodeBufferSize = 50;

        private Plane[] cameraFrustumPlanes = new Plane[6];   
        private Vector4[] cameraFrustumPlanesV4 = new Vector4[6]; // (Normals.x , Normals.y , Normals.z , distance)

        private Vector3 cameraPositionWS;

        public static readonly int TileID_INT2_SIZE = sizeof(uint) * 2;
        public static readonly int TileID_INT3_SIZE = sizeof(uint) * 3;
        public static readonly int Descriptor_INT_SIZE = sizeof(uint);
        public static readonly int PATCH_SIZE = 4 * sizeof(float) + 5 * sizeof(uint);


        public ATTerrainRenderer(ATTerrainRenderData data)
        {
            
            this.data = data;
            traverseQuadTreeShader = data.traverseQuadTreeShader;
            buildLodMapShader = data.buildLodMapShader;
            buildPatchesShader = data.buildPatchesShader;

        }

        #region  Init 
        public void Init()
        {
            InitComputeBuffer();
            InitMaterial();
        }

        public void InitComputeBuffer()
        {

            // For QuadTree
            topLevelTileList?.Release();
            topLevelTileList = new ComputeBuffer(data.TopLevelTileCount * data.TopLevelTileCount,TileID_INT2_SIZE, ComputeBufferType.Append);
            InitTopLevelNodeBuffer();
            
            tileListPing?.Release();
            tileListPing = new ComputeBuffer(_tempNodeBufferSize,TileID_INT2_SIZE, ComputeBufferType.Append);
            
            tileListPong?.Release();
            tileListPong = new ComputeBuffer(_tempNodeBufferSize,TileID_INT2_SIZE, ComputeBufferType.Append);

            finalTileListBuffer?.Release();
            finalTileListBuffer = new ComputeBuffer(_maxTileBufferSize, TileID_INT3_SIZE, ComputeBufferType.Append);
            tileDescriptors?.Release();
            tileDescriptors = new ComputeBuffer( data.TotalTileCount, Descriptor_INT_SIZE);
            // set all tile descriptor to 0
            tileDescriptors.SetData(new uint[data.TotalTileCount]);
            tileDescriptorsData = new uint[data.TotalTileCount];

            travQTIndirectArgsBuffer?.Release();
            travQTIndirectArgsBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            travQTIndirectArgsBuffer.SetData(new uint[]{1,1,1});
            // For debug
            traverseQuadTreeCntBuffer?.Release();
            traverseQuadTreeCntBuffer = new ComputeBuffer(ATTerrainRenderData.MAX_TERRAIN_LOD_LEVEL , sizeof(int), ComputeBufferType.Raw);
            finalTileCntBuffer?.Release();
            finalTileCntBuffer = new ComputeBuffer( 1 , sizeof(int), ComputeBufferType.Raw);

            // For Patches
            culledPatchBuffer?.Release();
            culledPatchBuffer = new ComputeBuffer(_maxTileBufferSize * 64, PATCH_SIZE, ComputeBufferType.Append);
            patchIndirectArgsBuffer?.Release();
            patchIndirectArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            patchIndirectArgsBuffer.SetData(new uint[]{1,1,1});
            patchBoundsBuffer?.Release();
            patchBoundsBuffer = new ComputeBuffer(_maxTileBufferSize, sizeof(float) * 6, ComputeBufferType.Append);
            patchBoundsIndirectArgsBuffer?.Release();
            patchBoundsIndirectArgsBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            patchBoundsIndirectArgsBuffer.SetData(new uint[]{1,1,1});
            buildPatchIndirectArgsBuffer?.Release();
            buildPatchIndirectArgsBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            buildPatchIndirectArgsBuffer.SetData(new uint[]{1,1,1});
            culledPatchCntBuffer?.Release();
            culledPatchCntBuffer = new ComputeBuffer( 1 , sizeof(int), ComputeBufferType.Raw);
            culledPatchCntBuffer.SetData(new int[]{0});

        }

        public void InitRT(CommandBuffer cmd )
        {
            // setup lod map rt 
            lodMapSize = data.GetTileCountInRow(0,true);
            var desc = new RenderTextureDescriptor(lodMapSize,lodMapSize,RenderTextureFormat.R16,0,1);
            desc.autoGenerateMips = false;
            desc.enableRandomWrite = true;

            // cmd.GetTemporaryRT(LOD_MAP_TEXTURE_ID, desc, FilterMode.Point);
            RenderingUtils.ReAllocateIfNeeded(ref lodMapRT, desc, FilterMode.Point);

            // Init Height Map RT 
            if ( data.heightMap == null )
            {
                Debug.LogError("Cannot Find Height Map in Terrian Data ");
                return;
            }
            RenderTextureDescriptor descHeightMap = new RenderTextureDescriptor(data.textureSize, data.textureSize, RenderTextureFormat.RFloat, 0);
            descHeightMap.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref heightMapRT, descHeightMap, data.heightMap.filterMode);
            // cmd.Blit(data.heightMap, heightMapRT);
            cmd.CopyTexture(data.heightMap, heightMapRT);

            if ( data.MinMaxHeightMap == null || data.MinMaxHeightMap.Count == 0 )
            {
                Debug.LogError("Cannot Find MinMaxHeight Map in Terrian Data ");
                return;
            }

            // Init MinMaxHeight Map RT 
            RenderTextureDescriptor descMinMaxHeightMap = new RenderTextureDescriptor(data.textureSize, data.textureSize, RenderTextureFormat.RGFloat, 0);
            descMinMaxHeightMap.enableRandomWrite = true;
            descMinMaxHeightMap.useMipMap = true;
            descMinMaxHeightMap.autoGenerateMips = false;
            descMinMaxHeightMap.mipCount = ATTerrainRenderData.MAX_TERRAIN_LOD_LEVEL;

            RenderingUtils.ReAllocateIfNeeded(ref minMaxHeightMapRT, descMinMaxHeightMap, data.MinMaxHeightMap[0].filterMode );
        
            for (int i = 0; i < data.MinMaxHeightMap.Count; i++)
            {
                cmd.CopyTexture(data.MinMaxHeightMap[i], 0 , 0 , minMaxHeightMapRT, 0, i);
            }
        
        }

        public void InitMaterial()
        {
            renderMaterail = new Material(data.material);
            renderMaterail.CopyMatchingPropertiesFromMaterial(data.material);

        }

        /// <summary>
        /// Initializes the top-level tile buffer, which represents the root layer of the quadtree (LOD 0).
        /// This buffer contains the 2D coordinate IDs of all coarsest-level terrain tiles and is used 
        /// as the starting point for GPU-side terrain paging, LOD traversal, or instanced rendering.
        /// 
        /// Data Layout Example (TopLevelTileCount = 3):
        /// Linear Index:  0     1     2     3     4     5     6     7     8
        /// Coordinates: (0,0) (1,0) (2,0) (0,1) (1,1) (2,1) (0,2) (1,2) (2,2)
        /// Positions:    SW    SC    SE     W     C     E     NW    NC    NE
        /// </summary>
        /// /// 
        public void InitTopLevelNodeBuffer()
        {
            uint2[] topLevelTileIDs = new uint2[data.TopLevelTileCount * data.TopLevelTileCount];
            for (uint i = 0; i < data.TopLevelTileCount; i++)
            {
                for (uint j = 0; j < data.TopLevelTileCount; j++)
                {
                    topLevelTileIDs[i * data.TopLevelTileCount + j] = new uint2(i,j);
                }
            }
            topLevelTileList.SetData(topLevelTileIDs);
        }

        public void InitKernel()
        {
            traverseQuadTreeKernelID = traverseQuadTreeShader.FindKernel("TraverseQuadTree");
            buildLodMapKernelID = buildLodMapShader.FindKernel("BuildLodMap");
            buildPatchesKernelID = buildPatchesShader.FindKernel("BuildPatches");
        }

        public void SetupPrepass(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if ( traverseQuadTreeShader == null)
                return;

            // InitComputeBuffer();
            if (!IsRTInited)
            {
                InitRT(cmd);
                IsRTInited = true;
            }
        }

        private void CleanBufferCounter( CommandBuffer cmd)
        {
            cmd.SetBufferCounterValue(topLevelTileList, (uint)data.GetTopLevelTileCountTotal());
            cmd.SetBufferCounterValue(tileListPing, 0);
            cmd.SetBufferCounterValue(tileListPong, 0);
            cmd.SetBufferCounterValue(finalTileListBuffer, 0);
            cmd.SetBufferCounterValue(culledPatchBuffer, 0);
        }
        private void SetupComputeBuffer( CommandBuffer cmd)
        {
            // set all tile descriptor to 1
            // uint[] tileDescriptorsData = new uint[data.TotalTileCount];
            // for (int i = 0; i < data.TotalTileCount; i++)
            // {
            //     tileDescriptorsData[i] = (uint)UnityEngine.Random.Range(0,8);
            // }
            // tileDescriptors.SetData(tileDescriptorsData);


            CleanBufferCounter(cmd);
        }

        private void UpdateCameraFrustumPlanes(Camera camera){
            GeometryUtility.CalculateFrustumPlanes(camera,cameraFrustumPlanes);
            for(var i = 0; i < cameraFrustumPlanes.Length; i ++){
                Vector4 v4 = (Vector4)cameraFrustumPlanes[i].normal;
                v4.w = cameraFrustumPlanes[i].distance;
                cameraFrustumPlanesV4[i] = v4;
            }
        }

        private void SetupTerrainBasicData( ComputeShader shader, CommandBuffer cmd )
        {
            if ( shader == null )
                return;

            // init world LOD params 
            Vector4[] worldLODParams = data.GetWorldLodParam();
            
            cmd.SetComputeVectorArrayParam(shader,WORLD_LOD_PARAMS_ID, worldLODParams);
            // init tileID offset
            // float[] tileIDOffsets = data.GetTileIDOffsetArrayFloat();
            // cmd.SetComputeFloatParams(shader, TILE_ID_OFFSETS_BY_LOD_ID,tileIDOffsets);
            int[] tileIDOffsets = data.GetTileIDOffsetArrayInt();
            // cmd.SetComputeIntParams(shader, TILE_ID_OFFSETS_BY_LOD_ID,tileIDOffsets);
            cmd.SetComputeIntParam(shader,TILE_ID_OFFSET_BY_LOD_ID_0,tileIDOffsets[0]);
            cmd.SetComputeIntParam(shader,TILE_ID_OFFSET_BY_LOD_ID_1,tileIDOffsets[1]);
            cmd.SetComputeIntParam(shader,TILE_ID_OFFSET_BY_LOD_ID_2,tileIDOffsets[2]);
            cmd.SetComputeIntParam(shader,TILE_ID_OFFSET_BY_LOD_ID_3,tileIDOffsets[3]);
            cmd.SetComputeIntParam(shader,TILE_ID_OFFSET_BY_LOD_ID_4,tileIDOffsets[4]);
            cmd.SetComputeIntParam(shader,TILE_ID_OFFSET_BY_LOD_ID_5,tileIDOffsets[5]);
            cmd.SetComputeIntParam(shader,TILE_ID_OFFSET_BY_LOD_ID_6,tileIDOffsets[6]);

            
            // Camera Frustum Panel Data 
            // (Normals.x , Normals.y , Normals.z , distance)
            cmd.SetComputeVectorArrayParam(shader,TERRAIN_CAMERA_FRUSTUM_PLANES_ID, cameraFrustumPlanesV4);

            cmd.SetComputeVectorParam(shader,TERRAIN_CAMERA_POSITION_WS_ID, cameraPositionWS);
            cmd.SetComputeVectorParam(shader,TERRAIN_WORLD_SIZE_ID, data.WorldSize);
            cmd.SetComputeVectorParam(shader,TERRAIN_OFFSET_WS_ID, data.GetWorldOffset());
            cmd.SetComputeIntParam(shader,TERRAIN_LOD_LEVEL_ID, data.LODLevel);
        }
 

        private void SetupTraverseQuadTree( CommandBuffer cmd )
        {
            if ( traverseQuadTreeShader == null )
                return;

            cmd.SetComputeBufferParam(traverseQuadTreeShader,traverseQuadTreeKernelID,APPEND_FINAL_TILE_LIST_ID,finalTileListBuffer);
            cmd.SetComputeBufferParam(traverseQuadTreeShader,traverseQuadTreeKernelID,TILE_DESCRIPTORS_ID,tileDescriptors);
            cmd.SetComputeTextureParam(traverseQuadTreeShader,traverseQuadTreeKernelID,HEIGHT_MAP_TEXTURE_ID, heightMapRT);
            cmd.SetComputeTextureParam(traverseQuadTreeShader,traverseQuadTreeKernelID,MIN_MAX_HEIGHT_MAP_TEXTURE_ID, minMaxHeightMapRT);

            cmd.SetComputeFloatParam(traverseQuadTreeShader,TILE_EVALUATION_RANGE_ID, data.tileEvaluationRange);
            SetupTerrainBasicData(traverseQuadTreeShader, cmd);
        }

        private void SetupBuildLODMap( CommandBuffer cmd )
        {
            if ( buildLodMapShader == null )
                return;

            cmd.SetComputeTextureParam(buildLodMapShader, buildLodMapKernelID, LOD_MAP_TEXTURE_ID, lodMapRT);
            cmd.SetComputeBufferParam(buildLodMapShader, buildLodMapKernelID, TILE_DESCRIPTORS_ID, tileDescriptors);
            
            SetupTerrainBasicData(buildLodMapShader, cmd);
        }

        private void SetupBuildPatches( CommandBuffer cmd )
        {
            if ( buildPatchesShader == null )
                return;

            if (data.UseFrustumCull)
            {
                cmd.EnableKeyword(buildPatchesShader, new LocalKeyword(buildPatchesShader,"ENABLE_FRUS_CULL"));
            }else{
                cmd.DisableKeyword(buildPatchesShader, new LocalKeyword(buildPatchesShader,"ENABLE_FRUS_CULL"));
            }
            // if ( data.UseHiZOcclusionCull)
            // {
            //     cmd.EnableKeyword(buildPatchesShader, new LocalKeyword(buildPatchesShader,"ENABLE_HIZ_CULL"));
            // }else{
            //     cmd.DisableKeyword(buildPatchesShader, new LocalKeyword(buildPatchesShader,"ENABLE_HIZ_CULL"));
            // }
            cmd.SetComputeIntParam(buildPatchesShader,SECTOR_COUNT_WORLD_ID, data.GetTileCountInRow(0,false));

            cmd.SetComputeFloatParam(buildPatchesShader,BOUNDS_HEIGHT_REDUNDANCE_ID, data.boundsHeightRedundance);
            cmd.SetComputeBufferParam(buildPatchesShader, buildPatchesKernelID, FINAL_TILE_LIST_ID, finalTileListBuffer);

            cmd.SetComputeTextureParam(buildPatchesShader, buildPatchesKernelID, LOD_MAP_TEXTURE_ID,lodMapRT);
            cmd.SetComputeTextureParam(buildPatchesShader, buildPatchesKernelID, HEIGHT_MAP_TEXTURE_ID, heightMapRT);
            cmd.SetComputeTextureParam(traverseQuadTreeShader,traverseQuadTreeKernelID,MIN_MAX_HEIGHT_MAP_TEXTURE_ID, minMaxHeightMapRT);
            cmd.SetComputeTextureParam(buildPatchesShader, buildPatchesKernelID, MIN_MAX_HEIGHT_MAP_TEXTURE_ID, minMaxHeightMapRT);
            cmd.SetComputeBufferParam(buildPatchesShader, buildPatchesKernelID, CULL_PATCH_LIST_ID, culledPatchBuffer);
            cmd.SetComputeBufferParam(buildPatchesShader, buildPatchesKernelID, PATCH_BOUNDS_LIST_ID, patchBoundsBuffer);
            
            SetupTerrainBasicData(buildPatchesShader, cmd);
        }

        public void SetupMaterial( Material material )
        {
            if ( material == null )
                return;

            if ( data.UpdateFromMaterial )
            {
                material.CopyMatchingPropertiesFromMaterial(data.material);
            }

            material.SetTexture(RENDER_HEIGHT_MAP_ID, data.heightMap);
            material.SetTexture(RENDER_NORMAL_MAP_ID, data.normalMap);
            material.SetTexture(RENDER_SPLAT_MAP0_ID, data.SplatMap0);
            material.SetTexture(RENDER_SPLAT_MAP1_ID, data.SplatMap1);
            material.SetInt(PATCH_MESH_GRID_SIZE_ID, data.meshSize);

            material.SetBuffer(PATCH_LIST_ID, culledPatchBuffer);

            if ( data.lodSeamless )
            {
                material.EnableKeyword("ENABLE_LOD_SEAMLESS");
            }else{
                material.DisableKeyword("ENABLE_LOD_SEAMLESS");
            }

            if ( data.debugRenderPatch)
            {
                material.EnableKeyword("ENABLE_PATCH_DEBUG");
            }else{
                material.DisableKeyword("ENABLE_PATCH_DEBUG");
            }

            material.SetVector(RENDER_WORLD_SIZE_ID, data.WorldSize);
            material.SetMatrix(RENDER_WORLD_TO_NORMAL_MAP_MATRIX_ID,Matrix4x4.Scale(data.WorldSize).inverse);

        }

        #endregion 

        public void PreRender( ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd)
        {
            if ( traverseQuadTreeShader == null )
                return;

            var cam = Camera.main;

            if ( cam == null )
                return;

            if ( data.debugInfoCulledBatch || data.debugInfoCulledBatch || data.debugInfoDesctiption)
                Debug.Log(">>> Temp Camera " + renderingData.cameraData.camera.name);

            SetupComputeBuffer(cmd);
            UpdateCameraFrustumPlanes(cam);
            cameraPositionWS = cam.transform.position;
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Traverse Quad Tree")))
            {
                cmd.CopyCounterValue(topLevelTileList, travQTIndirectArgsBuffer,0);

                ComputeBuffer consumeNodeList = tileListPing;
                ComputeBuffer appendNodeList = tileListPong;
                SetupTraverseQuadTree(cmd);
                
                for (int lod = data.LODLevel; lod >= 0; lod -- )
                {
                    // cmd.SetComputeFloatParam( traverseQuadTreeShader, PASS_LOD_ID, lod);
                    cmd.SetComputeIntParam( traverseQuadTreeShader, PASS_LOD_ID, lod);
                    if ( lod == data.LODLevel )
                    {
                        cmd.SetComputeBufferParam(traverseQuadTreeShader,traverseQuadTreeKernelID,CONSUME_TILE_LIST_ID,topLevelTileList);
                    }else{
                        cmd.SetComputeBufferParam(traverseQuadTreeShader,traverseQuadTreeKernelID,CONSUME_TILE_LIST_ID,consumeNodeList);
                    }
                    cmd.SetComputeBufferParam(traverseQuadTreeShader,traverseQuadTreeKernelID,APPEND_TILE_LIST_ID,appendNodeList);

                    cmd.DispatchCompute(traverseQuadTreeShader,traverseQuadTreeKernelID,travQTIndirectArgsBuffer,0);

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    cmd.CopyCounterValue(appendNodeList, travQTIndirectArgsBuffer, 0);
                    cmd.CopyCounterValue(appendNodeList, traverseQuadTreeCntBuffer, (uint)lod * sizeof(uint));

                    // ping pong the node list 
                    var temp = consumeNodeList;
                    consumeNodeList = appendNodeList;
                    appendNodeList = temp;
                }

                cmd.CopyCounterValue(finalTileListBuffer, finalTileCntBuffer, 0);

                if (data.debugInfoQuadTree)
                {
                    finalTileCntBuffer.GetData(finalTileCntData);
                    Debug.Log("Final Tile Count From GPU:" + finalTileCntData[0]);
                    // For debug 
                    traverseQuadTreeCntBuffer.GetData(traverseQuadTreeCntData);
                    int acc = 0;
                    int pre = data.TopLevelTileCount * data.TopLevelTileCount;
                    for (int lod = data.LODLevel; lod >= 0; lod--)
                    {
                        if (lod == 0)
                        {
                            Debug.Log("LOD " + lod + " Rest Tile Count From GPU:" + (finalTileCntData[0] - acc) + " Saved to Final : " + (finalTileCntData[0]));
                        }
                        else
                        {
                            var temp = traverseQuadTreeCntData[lod];
                            acc += (pre - temp / 4);
                            pre = temp;
                            Debug.Log("LOD " + lod + " Expended Tile Count From GPU:" + traverseQuadTreeCntData[lod] + " Saved To Final : " + acc);
                        }
                    }
                }

                if ( data.debugInfoDesctiption)
                {
                    tileDescriptors.GetData(tileDescriptorsData);
                    // show first 100 tile descriptors in 10 x 10 ints 
                    string debugInfo="";
                    for (int i = 0; i < 10; i++)
                    {
                        debugInfo += " [" + i + "] : ";
                        for (int j = 0; j < 10; j++)
                        {
                            debugInfo += " " + tileDescriptorsData[i * 10 + j];
                        }
                        debugInfo += "\n";
                    }

                    Debug.Log("Debug Description : \n" + debugInfo);

                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

            }

            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] LOD Map")))
            {
                SetupBuildLODMap(cmd);

                int dispatchXNum = lodMapSize / 8;
                int dispatchYNum = lodMapSize / 8;

                cmd.DispatchCompute(buildLodMapShader, buildLodMapKernelID, dispatchXNum, dispatchYNum, 1);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Build Patches")))
            {
                SetupBuildPatches(cmd);

                cmd.CopyCounterValue(finalTileListBuffer, buildPatchIndirectArgsBuffer, 0);
                cmd.DispatchCompute(buildPatchesShader, buildPatchesKernelID, buildPatchIndirectArgsBuffer, 0);
                cmd.CopyCounterValue(culledPatchBuffer, patchIndirectArgsBuffer, 4);
                cmd.CopyCounterValue(culledPatchBuffer, culledPatchCntBuffer, 0);

                if (data.debugInfoCulledBatch)
                {
                    culledPatchCntBuffer.GetData(culledPatchCntData);
                    Debug.Log("Culled Patch Count From GPU:" + culledPatchCntData[0]);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

        }

        public void Render( ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd )
        {
            if ( renderMaterail == null || data.patchMesh == null || patchIndirectArgsBuffer == null )
                return;



            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Terrain Render")))
            {
                SetupMaterial(renderMaterail);

                cmd.DrawMeshInstancedIndirect(
                    data.patchMesh,
                    0,
                    renderMaterail,
                    0,
                    patchIndirectArgsBuffer,
                    0);
            }
        }


        public void Dispose()
        {
            Debug.Log("Dispose in Terrain Renderer");
            tileDescriptors?.Release();
            topLevelTileList?.Release();
            tileListPing?.Release();
            tileListPong?.Release();
            finalTileListBuffer?.Release();
            travQTIndirectArgsBuffer?.Release();
            traverseQuadTreeCntBuffer?.Release();
            finalTileCntBuffer?.Release();

            culledPatchBuffer?.Release();
            culledPatchCntBuffer?.Release();
            patchIndirectArgsBuffer?.Release();
            patchBoundsBuffer?.Release();
            patchBoundsIndirectArgsBuffer?.Release();
            buildPatchIndirectArgsBuffer?.Release();


            minMaxHeightMapRT?.Release();
            lodMapRT?.Release();
            heightMapRT?.Release();

        }


        // Compute Buffers 
        public static readonly int APPEND_FINAL_TILE_LIST_ID = Shader.PropertyToID("AppendFinalTileList");
        public static readonly int FINAL_TILE_LIST_ID = Shader.PropertyToID("FinalTileList");
        public static readonly int CONSUME_TILE_LIST_ID = Shader.PropertyToID("ConsumeTileList");
        public static readonly int APPEND_TILE_LIST_ID = Shader.PropertyToID("AppendTileList");
        public static readonly int TILE_DESCRIPTORS_ID = Shader.PropertyToID("TileDescriptors");

        public static readonly int CULL_PATCH_LIST_ID = Shader.PropertyToID("CulledPatchList");
        public static readonly int PATCH_COMSUME_LIST_ID = Shader.PropertyToID("PatchConsumeList");
        public static readonly int PATCH_BOUNDS_LIST_ID = Shader.PropertyToID("PatchBoundsList");
        public static readonly int TILE_ID_OFFSET_BY_LOD_ID_0 = Shader.PropertyToID("TileIDOffsetByLOD0");
        public static readonly int TILE_ID_OFFSET_BY_LOD_ID_1 = Shader.PropertyToID("TileIDOffsetByLOD1");
        public static readonly int TILE_ID_OFFSET_BY_LOD_ID_2 = Shader.PropertyToID("TileIDOffsetByLOD2");
        public static readonly int TILE_ID_OFFSET_BY_LOD_ID_3 = Shader.PropertyToID("TileIDOffsetByLOD3");
        public static readonly int TILE_ID_OFFSET_BY_LOD_ID_4 = Shader.PropertyToID("TileIDOffsetByLOD4");
        public static readonly int TILE_ID_OFFSET_BY_LOD_ID_5 = Shader.PropertyToID("TileIDOffsetByLOD5");
        public static readonly int TILE_ID_OFFSET_BY_LOD_ID_6 = Shader.PropertyToID("TileIDOffsetByLOD6");

        // Texture 
        public static readonly int HEIGHT_MAP_TEXTURE_ID = Shader.PropertyToID("_HeightMapTexture");
        public static readonly int MIN_MAX_HEIGHT_MAP_TEXTURE_ID = Shader.PropertyToID("_MinMaxHeightMapTexture");


        // Terrain Basic Data
        public static readonly int TERRAIN_WORLD_SIZE_ID = Shader.PropertyToID("_TerrainWorldSize");
        public static readonly int TERRAIN_OFFSET_WS_ID = Shader.PropertyToID("_TerrainOffsetWS");
        public static readonly int TERRAIN_CAMERA_POSITION_WS_ID = Shader.PropertyToID("_TerrainCameraPositionWS");
        public static readonly int TERRAIN_CAMERA_FRUSTUM_PLANES_ID = Shader.PropertyToID("_TerrainCameraFrustumPlanes");
        public static readonly int WORLD_LOD_PARAMS_ID = Shader.PropertyToID("WorldLodParams");
        public static readonly int TILE_ID_OFFSETS_BY_LOD_ID = Shader.PropertyToID("TileIDOffsetByLOD");
        public static readonly int TERRAIN_LOD_LEVEL_ID = Shader.PropertyToID("_TerrainLODLevel");

        // For Traverse Quad Tree
        public static readonly int PASS_LOD_ID = Shader.PropertyToID("_PassLOD");
        public static readonly int TILE_EVALUATION_RANGE_ID = Shader.PropertyToID("_TileEvaluationRange");

        // Build LOD Map
        public static readonly int LOD_MAP_TEXTURE_ID = Shader.PropertyToID("_LODMapTexture");


        // Build Patch 
        public static readonly int BOUNDS_HEIGHT_REDUNDANCE_ID = Shader.PropertyToID("_BoundsHeightRedundance");
        public static readonly int FINAL_TILE_LIST_COUNT_ID= Shader.PropertyToID("_FinalTileListCount");
        public static readonly int SECTOR_COUNT_WORLD_ID = Shader.PropertyToID("_SectorCountWorld");
        // Render Material 

        public static readonly int RENDER_HEIGHT_MAP_ID  = Shader.PropertyToID("_HeightMap");
        public static readonly int RENDER_NORMAL_MAP_ID  = Shader.PropertyToID("_NormalMap");
        public static readonly int RENDER_SPLAT_MAP0_ID  = Shader.PropertyToID("_SplatMap0");
        public static readonly int RENDER_SPLAT_MAP1_ID  = Shader.PropertyToID("_SplatMap1");
        public static readonly int RENDER_WORLD_SIZE_ID = Shader.PropertyToID("_WorldSize");
        public static readonly int RENDER_WORLD_TO_NORMAL_MAP_MATRIX_ID = Shader.PropertyToID("_WorldToNormalMapMatrix");
        public static readonly int PATCH_LIST_ID = Shader.PropertyToID("_PatchList");
        public static readonly int PATCH_MESH_GRID_SIZE_ID = Shader.PropertyToID("_PatchMeshGridSize"); 
    }
}