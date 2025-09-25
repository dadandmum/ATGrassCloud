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
        //   - Count:      TopLevelTileCount²
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
        private ComputeBuffer patchBoundsBuffer;
        private ComputeBuffer patchBoundsIndirectArgsBuffer;

        private RTHandle lodMap;


        public int[] traverseQuadTreeCntData = new int[ATTerrainRenderData.MAX_TERRAIN_LOD_LEVEL];
        public int[] finalTileCntData = new int[1];

        // kernel id 
        public int traverseQuadTreeKernelID = 0;
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
        }

        public void InitComputeBuffer()
        {

            // For QuadTree
            topLevelTileList = new ComputeBuffer(data.TopLevelTileCount * data.TopLevelTileCount,TileID_INT2_SIZE, ComputeBufferType.Append);
            InitTopLevelNodeBuffer();
            
            tileListPing = new ComputeBuffer(_tempNodeBufferSize,TileID_INT2_SIZE, ComputeBufferType.Append);
            tileListPong = new ComputeBuffer(_tempNodeBufferSize,TileID_INT2_SIZE, ComputeBufferType.Append);

            finalTileListBuffer = new ComputeBuffer(_maxTileBufferSize, TileID_INT3_SIZE, ComputeBufferType.Append);
            tileDescriptors = new ComputeBuffer( data.TotalTileCount, Descriptor_INT_SIZE);

            travQTIndirectArgsBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            travQTIndirectArgsBuffer.SetData(new uint[]{1,1,1});
            // For debug
            traverseQuadTreeCntBuffer = new ComputeBuffer(ATTerrainRenderData.MAX_TERRAIN_LOD_LEVEL , sizeof(int), ComputeBufferType.Raw);
            finalTileCntBuffer = new ComputeBuffer( 1 , sizeof(int), ComputeBufferType.Raw);

            // For Patches
            culledPatchBuffer = new ComputeBuffer(_maxTileBufferSize * 64, PATCH_SIZE, ComputeBufferType.Append);
            patchIndirectArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            patchIndirectArgsBuffer.SetData(new uint[]{1,1,1});
            patchBoundsBuffer = new ComputeBuffer(_maxTileBufferSize, sizeof(float) * 6, ComputeBufferType.Append);
            patchBoundsIndirectArgsBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            patchBoundsIndirectArgsBuffer.SetData(new uint[]{1,1,1});


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
        }

        public void SetupPrepass(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if ( traverseQuadTreeShader == null)
                return;

        }

        private void CleanBufferCounter( CommandBuffer cmd)
        {
            cmd.SetBufferCounterValue(topLevelTileList, (uint)topLevelTileList.count);
            cmd.SetBufferCounterValue(tileListPing, 0);
            cmd.SetBufferCounterValue(tileListPong, 0);
            cmd.SetBufferCounterValue(finalTileListBuffer, 0);
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
            float[] tileIDOffsets = data.GetTileIDOffsetArray();
            cmd.SetComputeFloatParams(shader, TILE_ID_OFFSETS_BY_LOD_ID,tileIDOffsets);


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

            cmd.SetComputeFloatParam(traverseQuadTreeShader,TILE_EVALUATION_RANGE_ID, data.tileEvaluationRange);
            SetupTerrainBasicData(traverseQuadTreeShader, cmd);
        }

        #endregion 

        public void PreRender( ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd)
        {
            if ( traverseQuadTreeShader == null )
                return;

            var cam = Camera.main;

            if ( cam == null )
                return;

            CleanBufferCounter(cmd);
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

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

            }



            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Load Map")))
            {


            }

        }

        public void Render( ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd )
        {
            if ( traverseQuadTreeShader == null )
                return;
                

        }


        public void Dispose()
        {
            tileDescriptors?.Dispose();
            topLevelTileList?.Dispose();
            tileListPing?.Dispose();
            tileListPong?.Dispose();
            finalTileListBuffer?.Dispose();
            travQTIndirectArgsBuffer?.Dispose();
            traverseQuadTreeCntBuffer?.Dispose();
            finalTileCntBuffer?.Dispose();

        
        }

        // Kernels 
        public static readonly int TRAVERSE_QUAD_TREE_ID = Shader.PropertyToID("TraverseQuadTree");


        // Compute Buffers 
        public static readonly int APPEND_FINAL_TILE_LIST_ID = Shader.PropertyToID("AppendFinalTileList");
        public static readonly int CONSUME_TILE_LIST_ID = Shader.PropertyToID("ConsumeTileList");
        public static readonly int APPEND_TILE_LIST_ID = Shader.PropertyToID("AppendTileList");
        public static readonly int TILE_DESCRIPTORS_ID = Shader.PropertyToID("TileDescriptors");

        // Terrain Basic Data
        public static readonly int TERRAIN_WORLD_SIZE_ID = Shader.PropertyToID("_TerrainWorldSize");
        public static readonly int TERRAIN_OFFSET_WS_ID = Shader.PropertyToID("_TerrainOffsetWS");
        public static readonly int TERRAIN_CAMERA_POSITION_WS_ID = Shader.PropertyToID("_TerrainCameraPositionWS");
        public static readonly int TERRAIN_CAMERA_FRUSTUM_PLANES_ID = Shader.PropertyToID("_TerrainCameraFrustumPlanes");
        public static readonly int WORLD_LOD_PARAMS_ID = Shader.PropertyToID("WorldLodParams");
        public static readonly int TILE_ID_OFFSETS_BY_LOD_ID = Shader.PropertyToID("TileIDOffsetByLOD");
        public static readonly int TERRAIN_LOD_LEVEL_ID = Shader.PropertyToID("_TerrainsLODLevel");

        // For Traverse Quad Tree
        public static readonly int PASS_LOD_ID = Shader.PropertyToID("_PassLOD");
        public static readonly int TILE_EVALUATION_RANGE_ID = Shader.PropertyToID("_TileEvaluationRange");


    }
}