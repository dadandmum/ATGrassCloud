using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ATGrassCloud
{
    [CreateAssetMenu(fileName = "ATTerrainRenderData", menuName = "ATGrassCloud/ATTerrainRenderData", order = 300 )]
    public class ATTerrainRenderData : ScriptableObject
    {
        #region  Basic Data 
        // ==================== Basic Data ============================
        [Header("LOD")]
        [TabGroup("Basic Data")]
        [OnValueChanged("UpdateTileInfo")]
        [Range(1,7)]
        public int LODLevel = 6;
        public static int MAX_TERRAIN_LOD_LEVEL = 7;

        [InfoBox("Tile Number of LOD Level Max")]
        [TabGroup("Basic Data")]
        [OnValueChanged("UpdateTileInfo")]
        [Range(1, 10)]
        public int TopLevelTileCount = 5; 
        [TabGroup("Basic Data")]
        [ReadOnly]
        public float tileSize = 6.4f;

        [TabGroup("Basic Data")]
        [ReadOnly]
        public int totalTileCount = 0;

        [Header("World Size")]
        [TabGroup("Basic Data")]
        [OnValueChanged("UpdateTileInfo")]
        public float worldSizeXZ = 1024.0f;
        [TabGroup("Basic Data")]
        [OnValueChanged("UpdateTileInfo")]
        public float worldSizeHeight = 200.0f;

        [TabGroup("Basic Data")]
        [ReadOnly]
        public Vector3 terrainOffsetWS = Vector3.zero;
        [TabGroup("Basic Data")]
        [OnValueChanged("UpdateTileInfo")]
        public Vector3 terrainPivotCenter = new Vector3(0.5f, 0.0f, 0.5f);

        [Header("Tile Evaluation")]
        [TabGroup("Basic Data")]
        [OnValueChanged("UpdateTileInfo")]
        public float tileEvaluationRange = 1.2f;



        [TabGroup("Basic Data")]
        [ReadOnly]
        public Vector3 worldSize = Vector3.zero;

        public void UpdateTileInfo()
        {
            tileSize = worldSizeXZ / TopLevelTileCount / Mathf.Pow( 2.0f , LODLevel - 1 );
            totalTileCount = TotalTileCount;
            worldSize = WorldSize;
            terrainOffsetWS = GetWorldOffset();

        }

        public int TotalTileCount {
            get {
                int nodeCount = 0;
                for (int i = 0; i < LODLevel; i++)
                {
                    nodeCount += ( TopLevelTileCount * TopLevelTileCount) * ( 1 << (i*2) );
                }
                return nodeCount;
            }
        }
        public Vector3 WorldSize {
            get {
                return new Vector3( worldSizeXZ , worldSizeHeight , worldSizeXZ );
            }
        }
        /// <summary>
        /// Calculates the size (side length) of a terrain tile at the specified LOD (Level of Detail) level.
        /// The tile size is halved at each higher LOD level (more detailed level).
        /// </summary>
        public float GetTileSize(int lodLevel , bool clampByLODLevel = false)
        {
            var level = Mathf.Clamp(lodLevel, 0, clampByLODLevel ? LODLevel - 1 : MAX_TERRAIN_LOD_LEVEL - 1);
            return worldSizeXZ / TopLevelTileCount / Mathf.Pow(2.0f, level);
        }
        /// <summary>
        /// Calculates the size of a single patch within a tile at the given LOD level.
        /// Assumes each tile is divided into a 16x16 grid of patches.
        /// </summary>
        public float GetPatchExtent(int lodLevel , bool clampByLODLevel = false)
        {
           return GetTileSize(lodLevel, clampByLODLevel) / 16.0f;
        }

        /// <summary>
        /// Gets the number of sectors (or sub-tiles) per node in one dimension at the given LOD level.
        /// Sector count increases with LOD level: 1 at level 0, 4 at level 1, 16 at level 2, etc.
        /// Total sectors per node = (return value)^2.
        /// </summary>        
        public int GetSectorCountPerNode(int lodLevel , bool clampByLODLevel = false)
        {
            var level = Mathf.Clamp(lodLevel, 0, clampByLODLevel ? LODLevel - 1 : MAX_TERRAIN_LOD_LEVEL - 1);
            return 1 << (level * 2);
        }

        /// <summary>
        /// Gets the total number of tiles along one row (X or Z axis) in the entire terrain at the given LOD level.
        /// As LOD increases, more tiles are used to represent the same world area with higher detail.
        /// </summary>
        public int GetTileCountPerRow( int lodLevel , bool clampByLODLevel = false)
        {
            var level = Mathf.Clamp(lodLevel, 0, clampByLODLevel ? LODLevel - 1 : MAX_TERRAIN_LOD_LEVEL - 1);
            return ( TopLevelTileCount) * ( 1 << (level) );
        }
        /// <summary>
        /// Generates an array of Vector4 parameters for all LOD levels, which can be passed to shaders.
        /// Each Vector4 contains:
                /// x: Tile size (side length)
                /// y: Patch extent (size of one patch within a tile)
                /// z: Tile count per row in the entire terrain
                /// w: Sector count per node (along one axis)
        /// </summary>
        /// <returns>An array of Vector4, one for each LOD level up to MAX_TERRAIN_LOD_LEVEL.</returns>
        public Vector4[] GetWorldLodParam()
        {
            Vector4[] worldLodParams = new Vector4[MAX_TERRAIN_LOD_LEVEL];
            for (int i = 0; i < MAX_TERRAIN_LOD_LEVEL; i++)
            {
                int lod = LODLevel - i - 1 ;
                worldLodParams[i] = new Vector4( GetTileSize(lod , true) , GetPatchExtent(lod , true) , GetTileCountPerRow(lod , true) , GetSectorCountPerNode(lod , true));
            }
            return worldLodParams;
        }

        public int GetTileIDOffset(int lodLevel , bool clampByLODLevel = false)
        {
            var level = Mathf.Clamp(lodLevel, 0, clampByLODLevel ? LODLevel - 1 : MAX_TERRAIN_LOD_LEVEL - 1);

            int offset = 0;
            for (int i = 0; i < level; i++)
            {
                offset += ( TopLevelTileCount * TopLevelTileCount) * ( 1 << (i * 2) );
            }
            return offset;
        }

        public float[] GetTileIDOffsetArray()
        {
            float[] tileOffsets = new float[MAX_TERRAIN_LOD_LEVEL]; 
            for (int i = 0; i < MAX_TERRAIN_LOD_LEVEL; i++)
            {
                int lod = LODLevel - i - 1 ;
                tileOffsets[i] = (float)GetTileIDOffset(lod , true );
            }
            return tileOffsets;
        }

        // move the terrain according to the pivot center
        public Vector3 GetWorldOffset()
        {
            return new Vector3( -terrainPivotCenter.x * WorldSize.x , 0.0f , -terrainPivotCenter.z * WorldSize.z );
        }

        #endregion Basic Data


        // ======================== Terrain Data =================================

        [TabGroup("Terrain Data")]
        public int textureSize = 2048;

        [TabGroup("Terrain Data")]
        [PreviewField(128)]
        public Texture2D heightMap;

        [TabGroup("Terrain Data")]
        [PreviewField(128)]
        public Texture2D normalMap;

        [TabGroup("Terrain Data")]
        [PreviewField(128)]
        public Texture2D SplatMap;
        [TabGroup("Terrain Data")]
        [PreviewField(128)]
        public Texture2D SplatMap2;

        // ======================= Patch Mesh ================================
        [TabGroup("Mesh")]
        [PreviewField(128)]
        public Mesh patchMesh;

        [TabGroup("Mesh")]
        public int meshSize = 16;

        [TabGroup("Mesh")]
        public string outputDir = "Assets/ATGrassCloud/Data/Terrain/Mesh";

        [TabGroup("Mesh")]
        [Button]
        public void GeneratePatchMesh()
        {

#if UNITY_EDITOR
            var mesh = ATTerrainUtils.CreatePlaneMesh(meshSize);
            
            string exportPath = outputDir + "/PatchMesh_" + meshSize + ".mesh";
            UnityEditor.AssetDatabase.CreateAsset(mesh,exportPath);
            UnityEditor.AssetDatabase.Refresh();
            Debug.Log("Generate Patch Mesh " + meshSize + " to " + exportPath);

            // load from asset database
            patchMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(exportPath);
#endif

        }


        // ======================== Reference =================================
        // [TabGroup("Reference")]
        // public ComputeShader computeShader;
        [TabGroup("Reference")]
        public ComputeShader traverseQuadTreeShader;
        [TabGroup("Reference")]
        public ComputeShader buildLodMapShader;
        [TabGroup("Reference")]
        public ComputeShader buildPatchesShader;

        [TabGroup("Reference")]
        public Material material;

        

        // ======================= Debug =====================
        [BoxGroup("Debug")]
        public bool debugInfoQuadTree = false;
        

    }
}