using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ATGrassCloud
{
    public enum ATGrassRenderType
    {
        ProcedualMesh,

        Mesh,

        Billboard,
    }

    public enum ATCascadeTexSize
    {
        Default = 0,
        I256 = 1,
        I512 = 2,
        I1024 = 3,
        I2048 = 4,
    }

    [CreateAssetMenu(fileName = "New Grass Cascade Data", menuName = "ATGrassCloud/Grass Cascade Data" , order = 80)]

    public class ATGrassCascadeData : ScriptableObject
    {
        [BoxGroup("Settings")]
        public bool isRender = true;
        [BoxGroup("Settings")]
        public string cascadeName = "Cascade0";
        [BoxGroup("Settings")]
        public int cascadeLevel = 0;

        [BoxGroup("Range")]
        [OnValueChanged("UpdateRange")]
        [MinMaxSlider(0.0f, 1000.0f,true)]
        public Vector2 cascadeRange = new Vector2(0.0f, 2.0f);

        [BoxGroup("Range")]
        [ReadOnly]
        public float cascadeInnerRange = 0.0f;
        [BoxGroup("Range")]
        [MaxValue("cascadeInnerRange")]
        public float cascadeInnerFade = 0.0f;

        [BoxGroup("Range")]
        [ReadOnly]
        public float cascadeOutterRange = 2.0f;
        [BoxGroup("Range")]
        public float cascadeOutterFade = 1.0f;

    

        /// <summary>
        /// Get the range data of the cascade.
        /// </summary>
        /// <returns>( innerRange , outterRange , 1.0f / innerFade , 1.0f / outterFade )</returns>
        public Vector4 GetRangeData()
        {
            return new Vector4(
                cascadeRange.x,
                cascadeRange.y,
                1.0f / Mathf.Max( 0.00001f , cascadeInnerFade),
                1.0f / Mathf.Max( 0.00001f , cascadeOutterFade));
        }

        public void UpdateRange()
        {
            cascadeInnerRange = cascadeRange.x;
            cascadeOutterRange = cascadeRange.y;
        }

        public float GetMaxDistance()
        {
            return cascadeOutterRange + cascadeOutterFade;
        }

        public float GetSnapDistance()
        {
            return HeightMapSnapDistance;
        }


        [BoxGroup("Settings")]
        public ATCascadeTexSize cascadeTexSize = ATCascadeTexSize.Default;
        public int GetTextureSize()
        {
            switch (cascadeTexSize)
            {
                case ATCascadeTexSize.Default:
                    return 1024;
                case ATCascadeTexSize.I256:
                    return 256;
                case ATCascadeTexSize.I512:
                    return 512;
                case ATCascadeTexSize.I1024:
                    return 1024;
                case ATCascadeTexSize.I2048:
                    return 2048;
                default:
                    return 1024;
            }
        }

        [BoxGroup("Settings")]
        [InfoBox("In Milion")]
        [Min(0.01f)]
        public float maxInstanceCount = 1.0f;
        public int GetMaxInstanceCount()
        {
            return Mathf.Max( 1 , (int)(maxInstanceCount * 1000000));

        }
        [BoxGroup("Settings")]
        [Range(1, 8)]
        public int instancePerTile = 4;

        [BoxGroup("Settings")]
        public float TileSize = 0.2f;


        [BoxGroup("HeightMap")]
        public LayerMask heightMapLayer;

        [BoxGroup("HeightMap")]
        public float HeightMapSnapDistance = 10f;

        [BoxGroup("Occlusion")]
        public bool UseFrustumCulling= true;
        [BoxGroup("Occlusion")]
        public float EdgeFrustumCullingOffset = 0.1f;
        [BoxGroup("Occlusion")]
        public float NearPlaneOffset = 2.5f;

        [BoxGroup("Occlusion")]
        public bool UseDepthOcclusionCulling = true;

        [BoxGroup("Occlusion")]
        public float OccludHeightOffset = 1.5f;

        [BoxGroup("Occlusion")]
        public bool distanceDensityCulling = true;
        [ShowIf("distanceDensityCulling")]
        [BoxGroup("Occlusion")]
        public float FullDensityDistance = 40f;



        [BoxGroup("Rendering")]
        public ATGrassRenderType renderType = ATGrassRenderType.ProcedualMesh;


        [BoxGroup("Rendering")]
        [ShowIf("renderType", ATGrassRenderType.ProcedualMesh)]
        public Material ProcedualMeshMaterial;
        
        [BoxGroup("Rendering")]
        [ShowIf("renderType", ATGrassRenderType.ProcedualMesh)]
        public int procedualMeshSegments = 3;


        [BoxGroup("Rendering")]
        [ShowIf("renderType", ATGrassRenderType.Mesh)]
        public Material MeshMaterial;

        [BoxGroup("Rendering")]
        [ShowIf("renderType", ATGrassRenderType.Mesh)]
        public Mesh Mesh;


        [BoxGroup("Rendering")]
        public bool updateMaterial = false;

        [BoxGroup("Debug")]
        public bool debugCascade = false;

        [BoxGroup("Debug")]
        public Color debugColor = Color.red;



    }

}