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
        private GraphicsBuffer tileListPing;
        private GraphicsBuffer tileListPong;
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
        private ComputeBuffer _debug_finalTileCntBuffer;

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
        private ComputeBuffer _debug_traverseQuadTreeCntBuffer;
        /// <summary>
        /// Buffer that stores terrain patch data that has passed culling (e.g., frustum culling).
        /// Each patch contains rendering information such as position, LOD level, and bounds.
        /// This buffer is typically used as an AppendBuffer, allowing patches to be dynamically added 
        /// in the Compute Shader only if they are visible or relevant.
        /// It will later be used as input for GPU indirect drawing (DrawProceduralIndirect) to render terrain efficiently.
        /// </summary>        
        private ComputeBuffer culledPatchBuffer;

        /// <summary>
        /// Indirect arguments buffer used to pass parameters to Graphics.DrawProceduralIndirect.
        /// The standard layout is:
        ///   uint[0]: The number of patches to render (filled by ComputeBuffer.CopyCount())
        ///   uint[1]: Instance count (usually 1)
        ///   uint[2]: Starting vertex position (usually 0)
        ///   uint[3]: Starting instance position (usually 0)
        ///   uint[4]: address offset (usually 0)
        /// After the Compute Shader runs, CopyCount is called to write the actual number of valid patches 
        /// from culledPatchBuffer into this buffer, enabling dynamic, cull-driven rendering.
        /// </summary>
        private ComputeBuffer patchIndirectArgsBuffer;
        private ComputeBuffer _debug_culledPatchCntBuffer;
        private ComputeBuffer patchBoundsBuffer;
        private ComputeBuffer patchBoundsIndirectArgsBuffer;
        private ComputeBuffer buildPatchIndirectArgsBuffer;


        private RTHandle lodMapRT;
        private int lodMapSize = 1;
        private RTHandle heightMapRT;
        private RTHandle minMaxHeightMapRT;
        private bool IsRTInited = false;

        public int[] traverseQuadTreeCntData = new int[ATTerrainRenderData.MAX_TERRAIN_LOD_LEVEL];
        public int[] _debug_finalTileCntData = new int[1];
        public int[] culledPatchCntData = new int[1];

        public int[] _debug_finalTileListData = new int[1024 * 3];
        public uint[] _debug_tileDescriptorsData;

        // material
        public Material renderMaterail;
        public Material debugMaterial;

        // kernel id 
        public int traverseQuadTreeKernelID = 0;
        public int buildLodMapKernelID = 0;
        public int buildPatchesKernelID = 0;

        private int _finalTileBufferSize = 400;
        private int _tempNodeBufferSize = 200;
        private int _batchBufferSize = 400 * 64;
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
            _tempNodeBufferSize = data.tileBufferSize;
            // guess the approximate max tile count in the final tile list
            _finalTileBufferSize = 25 + data.tileBufferSize * ( data.LODLevel - 2);
            _batchBufferSize = _finalTileBufferSize * data.PatchCountPerTileInRow * data.PatchCountPerTileInRow;

            // For QuadTree
            topLevelTileList?.Release();
            topLevelTileList = new ComputeBuffer(data.TopLevelTileCount * data.TopLevelTileCount,TileID_INT2_SIZE, ComputeBufferType.Append);
            InitTopLevelNodeBuffer();
            
            tileListPing?.Release();
            // tileListPing = new ComputeBuffer(_tempNodeBufferSize,TileID_INT2_SIZE, ComputeBufferType.Append);
            tileListPing = new GraphicsBuffer( GraphicsBuffer.Target.Append, _tempNodeBufferSize,TileID_INT2_SIZE );
            tileListPong?.Release();
            // tileListPong = new ComputeBuffer(_tempNodeBufferSize,TileID_INT2_SIZE, ComputeBufferType.Append);
            tileListPong = new GraphicsBuffer( GraphicsBuffer.Target.Append, _tempNodeBufferSize,TileID_INT2_SIZE);

            finalTileListBuffer?.Release();
            finalTileListBuffer = new ComputeBuffer(_finalTileBufferSize, TileID_INT3_SIZE, ComputeBufferType.Append);
            tileDescriptors?.Release();
            tileDescriptors = new ComputeBuffer( data.TotalTileCount, Descriptor_INT_SIZE);
            // set all tile descriptor to 0
            tileDescriptors.SetData(new uint[data.TotalTileCount]);
            _debug_tileDescriptorsData = new uint[data.TotalTileCount];

            travQTIndirectArgsBuffer?.Release();
            travQTIndirectArgsBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            travQTIndirectArgsBuffer.SetData(new uint[]{1,1,1});
            // For debug
            _debug_traverseQuadTreeCntBuffer?.Release();
            _debug_traverseQuadTreeCntBuffer = new ComputeBuffer(ATTerrainRenderData.MAX_TERRAIN_LOD_LEVEL , sizeof(int), ComputeBufferType.Raw);
            _debug_finalTileCntBuffer?.Release();
            _debug_finalTileCntBuffer = new ComputeBuffer( 1 , sizeof(int), ComputeBufferType.Raw);

            // For Patches
            culledPatchBuffer?.Release();
            culledPatchBuffer = new ComputeBuffer(_batchBufferSize, PATCH_SIZE, ComputeBufferType.Append);
            patchIndirectArgsBuffer?.Release();
            patchIndirectArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            if (data.patchMesh != null)
            {
                int subMeshIndex = 0;
                patchIndirectArgsBuffer.SetData(new uint[]{
                (uint)data.patchMesh.GetIndexCount(subMeshIndex),
                1, // This value is the instance count, we set it to 1 for now.
                (uint)data.patchMesh.GetIndexStart(subMeshIndex),
                (uint)data.patchMesh.GetBaseVertex(subMeshIndex),
                0 // the offset in the vertex buffer
                });
            }else{
                patchIndirectArgsBuffer.SetData(new uint[]{0,0,0,0,0});
            }
            patchBoundsBuffer?.Release();
            patchBoundsBuffer = new ComputeBuffer(_batchBufferSize, sizeof(float) * 6, ComputeBufferType.Append);
            patchBoundsIndirectArgsBuffer?.Release();
            patchBoundsIndirectArgsBuffer = new ComputeBuffer(5, sizeof(uint), ComputeBufferType.IndirectArguments);
            if (data.boundingBoxMesh != null)
            {
                int subMeshIndex = 0;
                patchBoundsIndirectArgsBuffer.SetData(new uint[]{
                (uint)data.boundingBoxMesh.GetIndexCount(subMeshIndex),
                1, // This value is the instance count, we set it to 1 for now.
                (uint)data.boundingBoxMesh.GetIndexStart(subMeshIndex),
                (uint)data.boundingBoxMesh.GetBaseVertex(subMeshIndex),
                0 // the offset in the vertex buffer
                });
            }else{
                patchBoundsIndirectArgsBuffer.SetData(new uint[]{0,0,0,0,0});
            }
            buildPatchIndirectArgsBuffer?.Release();
            buildPatchIndirectArgsBuffer = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.IndirectArguments);
            buildPatchIndirectArgsBuffer.SetData(new uint[]{1,1,1});
            _debug_culledPatchCntBuffer?.Release();
            _debug_culledPatchCntBuffer = new ComputeBuffer( 1 , sizeof(int), ComputeBufferType.Raw);
            _debug_culledPatchCntBuffer.SetData(new int[]{0});

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
            descMinMaxHeightMap.mipCount = data.MinMaxHeightMap.Count - 1;

            RenderingUtils.ReAllocateIfNeeded(ref minMaxHeightMapRT, descMinMaxHeightMap, data.MinMaxHeightMap[0].filterMode );
        
            for (int i = 0; i < data.MinMaxHeightMap.Count; i++)
            {
                cmd.CopyTexture(data.MinMaxHeightMap[i], 0 , 0 , minMaxHeightMapRT, 0, i);
            }
        
        }

        public void InitMaterial()
        {
            if (data.material != null)
            {
                renderMaterail = new Material(data.material);
                renderMaterail.CopyMatchingPropertiesFromMaterial(data.material);
            }else{
                renderMaterail = null;
            }
            if (data.boundingBoxMaterial != null)
            {
                debugMaterial = new Material(data.boundingBoxMaterial);
                debugMaterial.CopyMatchingPropertiesFromMaterial(data.boundingBoxMaterial);
            }else{
                debugMaterial = null;
            }

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
            cmd.SetBufferCounterValue(tileListPong, 0);
            cmd.SetBufferCounterValue(culledPatchBuffer, 0);
            cmd.SetBufferCounterValue(finalTileListBuffer, 0);
            cmd.SetBufferCounterValue(patchBoundsBuffer, 0);
        }
        private void SetupComputeBuffer( CommandBuffer cmd)
        {
            // InitTopLevelNodeBuffer();
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
            cmd.SetComputeIntParam(shader,MIN_MAX_HEIGHT_MAP_TEX_SIZE_ID, data.textureSize);
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
            if ( data.UseHiZOcclusionCull)
            {
                cmd.EnableKeyword(buildPatchesShader, new LocalKeyword(buildPatchesShader,"ENABLE_HIZ_CULL"));
            }else{
                cmd.DisableKeyword(buildPatchesShader, new LocalKeyword(buildPatchesShader,"ENABLE_HIZ_CULL"));
            }

            if ( data.debugRenderBoundingBox)
            {
                cmd.EnableKeyword(buildPatchesShader, new LocalKeyword(buildPatchesShader,"BOUNDS_DEBUG"));
            }else{
                cmd.DisableKeyword(buildPatchesShader, new LocalKeyword(buildPatchesShader,"BOUNDS_DEBUG"));
            }
            cmd.SetComputeIntParam(buildPatchesShader,SECTOR_COUNT_WORLD_ID, data.GetTileCountInRow(0,false));

            cmd.SetComputeFloatParam(buildPatchesShader,BOUNDS_HEIGHT_REDUNDANCE_ID, data.boundsHeightRedundance);
            cmd.SetComputeBufferParam(buildPatchesShader, buildPatchesKernelID, FINAL_TILE_LIST_ID, finalTileListBuffer);

            cmd.SetComputeTextureParam(buildPatchesShader, buildPatchesKernelID, LOD_MAP_TEXTURE_ID,lodMapRT);
            cmd.SetComputeTextureParam(buildPatchesShader, buildPatchesKernelID, HEIGHT_MAP_TEXTURE_ID, heightMapRT);
            cmd.SetComputeTextureParam(buildPatchesShader, buildPatchesKernelID, MIN_MAX_HEIGHT_MAP_TEXTURE_ID, minMaxHeightMapRT);
            cmd.SetComputeTextureParam(buildPatchesShader, buildPatchesKernelID, MIN_MAX_HEIGHT_MAP_TEXTURE_ID, minMaxHeightMapRT);
            cmd.SetComputeBufferParam(buildPatchesShader, buildPatchesKernelID, CULLED_PATCH_LIST_ID, culledPatchBuffer);
            
            if ( data.debugRenderBoundingBox)
            {
                cmd.SetComputeBufferParam(buildPatchesShader, buildPatchesKernelID, PATCH_BOUNDS_LIST_ID, patchBoundsBuffer);
            }

            SetupTerrainBasicData(buildPatchesShader, cmd);
        }

        public void SetupMaterial( Material material , CommandBuffer cmd )
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
            material.SetInt(PATCH_MESH_GRID_SIZE_ID, data.meshGridCountInRow);

            material.SetBuffer(PATCH_LIST_ID, culledPatchBuffer);
            // cmd.SetGlobalBuffer(PATCH_LIST_ID, culledPatchBuffer);

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

            material.SetVector(TERRAIN_WORLD_SIZE_ID, data.WorldSize);
            material.SetMatrix(RENDER_WORLD_TO_NORMAL_MAP_MATRIX_ID,Matrix4x4.Scale(data.WorldSize).inverse);
            material.SetFloatArray(MESH_SCALE_BY_LOD_ID, data.GetMeshScaleByLOD());
            
            material.SetVectorArray(WORLD_LOD_PARAMS_ID, data.GetWorldLodParam());

            if ( data.debugRenderBoundingBox)
            {
                material.SetBuffer(BOUNDS_LIST_ID, patchBoundsBuffer);
            }else{
            }

        }

        #endregion 


        #region  Camera

        public Vector3 GetSnappedCameraPosition ( Camera cam , float snapDistance )
        {
            Vector3 position = cam.transform.position;
            position.x = Mathf.Round(position.x / snapDistance) * snapDistance;
            position.z = Mathf.Round(position.z / snapDistance) * snapDistance;
            return position;
        }


        #endregion
        public void PreRender( ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd)
        {
            if ( traverseQuadTreeShader == null )
                return;

            var cam = Camera.main;

            if ( cam == null )
                return;

            if ( data.debugInfoQuadTree || data.debugInfoCulledBatch || data.debugInfoDesctiption)
                Debug.Log(">>> Temp Camera " + renderingData.cameraData.camera.name);


            if ( data.snapCamera )
            {
                var newCamPos = GetSnappedCameraPosition(cam, data.GetTileSize(0)  );

                if (data.onlyUpdateWhenCameraMove && newCamPos.Equals(cameraPositionWS))
                {
                    if (data.debugInfoQuadTree || data.debugInfoCulledBatch || data.debugInfoDesctiption)
                        Debug.Log(">> Skip Rebuild Quad Tree");
                    return;
                }

                cameraPositionWS = newCamPos;
            }
            else
            {
                 cameraPositionWS = cam.transform.position;
            }


            SetupComputeBuffer(cmd);
            UpdateCameraFrustumPlanes(cam);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();


            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Traverse Quad Tree")))
            {
                cmd.CopyCounterValue(topLevelTileList, travQTIndirectArgsBuffer,0);

                GraphicsBuffer consumeNodeList = tileListPing;
                GraphicsBuffer appendNodeList = tileListPong;
                SetupTraverseQuadTree(cmd);
                
                for (int lod = data.LODLevel - 1 ; lod >= 0; lod -- )
                {
                    cmd.SetComputeIntParam( traverseQuadTreeShader, PASS_LOD_ID, lod);

                    if ( lod == data.LODLevel - 1 )
                    {
                        cmd.SetComputeBufferParam(traverseQuadTreeShader,traverseQuadTreeKernelID,CONSUME_TILE_LIST_ID,topLevelTileList);
                    } else {
                        cmd.SetComputeBufferParam(traverseQuadTreeShader,traverseQuadTreeKernelID,CONSUME_TILE_LIST_ID,consumeNodeList);
                    }
                    cmd.SetComputeBufferParam(traverseQuadTreeShader,traverseQuadTreeKernelID,APPEND_TILE_LIST_ID,appendNodeList);

                    cmd.SetBufferCounterValue(appendNodeList, 0);

                    cmd.DispatchCompute(traverseQuadTreeShader,traverseQuadTreeKernelID,travQTIndirectArgsBuffer,0);

                    cmd.CopyCounterValue(appendNodeList, travQTIndirectArgsBuffer, 0);
                    cmd.CopyCounterValue(appendNodeList, _debug_traverseQuadTreeCntBuffer, (uint)lod * sizeof(uint));

                    // context.ExecuteCommandBuffer(cmd);
                    // cmd.Clear();
                    
                    // ping pong the node list 
                    var temp = consumeNodeList;
                    consumeNodeList = appendNodeList;
                    appendNodeList = temp;
                }
            

                cmd.CopyCounterValue(finalTileListBuffer, _debug_finalTileCntBuffer, 0);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                if (data.debugInfoQuadTree)
                {
                    _debug_finalTileCntBuffer.GetData(_debug_finalTileCntData);
                    Debug.Log("Final Tile Count From GPU:" + _debug_finalTileCntData[0]);
                    // For debug 
                    _debug_traverseQuadTreeCntBuffer.GetData(traverseQuadTreeCntData);
                    int acc = 0;
                    int pre = data.TopLevelTileCount * data.TopLevelTileCount;
                    for (int lod = data.LODLevel - 1 ; lod >= 0; lod--)
                    {
                        if (lod == 0)
                        {
                            Debug.Log("LOD " + lod + " Rest Tile Count From GPU:" + (_debug_finalTileCntData[0] - acc) + " Saved to Final : " + (_debug_finalTileCntData[0]));
                        }
                        else
                        {
                            var temp = traverseQuadTreeCntData[lod];
                            acc += (pre - temp / 4);
                            pre = temp;
                            Debug.Log("LOD " + lod + " Expended Tile Count From GPU:" + traverseQuadTreeCntData[lod] + " Saved To Final : " + acc);
                        }
                    }

                    finalTileListBuffer.GetData(_debug_finalTileListData,0,0,_debug_finalTileCntData[0] * 3 );
                    string finalTileLog = "";
                    for (int i = 0; i < _debug_finalTileCntData[0]; i++)
                    {
                        finalTileLog += "id" + i + ":" + _debug_finalTileListData[3 * i] + " " + _debug_finalTileListData[3 * i + 1] + " " + _debug_finalTileListData[3 * i + 2] + "|" ;
                    }
                    Debug.Log("Final Tile List From GPU:" + finalTileLog);
                }

                if ( data.debugInfoDesctiption)
                {
                    tileDescriptors.GetData(_debug_tileDescriptorsData);
                    // show first 100 tile descriptors in 10 x 10 ints 
                    string debugInfo="";
                    for (int i = 0; i < 10; i++)
                    {
                        debugInfo += " [" + i + "] : ";
                        for (int j = 0; j < 10; j++)
                        {
                            debugInfo += " " + _debug_tileDescriptorsData[i * 10 + j];
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
                
                cmd.CopyCounterValue(culledPatchBuffer, _debug_culledPatchCntBuffer, 0);

                if (data.debugInfoCulledBatch)
                {
                    _debug_culledPatchCntBuffer.GetData(culledPatchCntData);
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

            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Terrain Rendering")))
            {
                SetupMaterial(renderMaterail, cmd);

                cmd.CopyCounterValue(culledPatchBuffer, patchIndirectArgsBuffer, 1 * sizeof(uint));
                cmd.DrawMeshInstancedIndirect(
                    data.patchMesh,
                    0,
                    renderMaterail,
                    0,
                    patchIndirectArgsBuffer,
                    0);
                    
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

            }

            if ( data.debugRenderBoundingBox && debugMaterial != null)
            {

                using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Debug Render Bounding Box")))
                {
                    SetupMaterial(debugMaterial, cmd);

                    cmd.CopyCounterValue(patchBoundsBuffer, patchBoundsIndirectArgsBuffer, 1 * sizeof(uint));
                    cmd.DrawMeshInstancedIndirect(
                        data.boundingBoxMesh,
                        0,
                        debugMaterial,
                        0,
                        patchBoundsIndirectArgsBuffer,
                        0);

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }
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
            _debug_traverseQuadTreeCntBuffer?.Release();
            _debug_finalTileCntBuffer?.Release();

            culledPatchBuffer?.Release();
            _debug_culledPatchCntBuffer?.Release();
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
        public static readonly int STRUCTURE_TILE_LIST_ID = Shader.PropertyToID("StructureTileList");
        public static readonly int APPEND_TILE_LIST_ID = Shader.PropertyToID("AppendTileList");
        public static readonly int TILE_DESCRIPTORS_ID = Shader.PropertyToID("TileDescriptors");

        public static readonly int CULLED_PATCH_LIST_ID = Shader.PropertyToID("CulledPatchList");
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
        public static readonly int MIN_MAX_HEIGHT_MAP_TEX_SIZE_ID = Shader.PropertyToID("_MinMaxHeightMapTexSize");

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
        public static readonly int BOUNDS_LIST_ID = Shader.PropertyToID("_BoundsList");
        public static readonly int PATCH_MESH_GRID_SIZE_ID = Shader.PropertyToID("_PatchMeshGridSize");
        public static readonly int MESH_SCALE_BY_LOD_ID = Shader.PropertyToID("MeshScaleByLOD");
    }
}