using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ATGrassCloud
{
    [CreateAssetMenu(fileName = "ATCloudCascadeData", menuName = "ATGrassCloud/Cloud Cascade Data" , order = 100 )]
    public class ATCloudCascadeData : ScriptableObject
    {
        [BoxGroup("Settings")]
        public bool isRender = true;
        [BoxGroup("Settings")]
        public string cascadeName = "Cloud_Cascade_0";
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
        [BoxGroup("Range")]
        public float snapDistance = 1.0f;


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
            return snapDistance;
        }

        [BoxGroup("Settings")]
        public float SDFGridSize = 1.0f;


        [BoxGroup("Material")]
        public Material cloudRenderMaterial;

        [BoxGroup("Material")]
        public bool updateMaterial = false;

        [BoxGroup]
        [InlineEditor]
        public ATCloudRenderData cloudRenderData;

        [BoxGroup]
        [InlineEditor]
        public ATCloudNoiseData cloudNoiseData;



        [BoxGroup("Debug")]
        public bool debugCascade = false;

        [BoxGroup("Debug")]
        public Color debugColor = Color.white;


        public void SetMaterialParameter(Material mat)
        {
            mat.SetVector("_CascadeRange", GetRangeData());
            if ( cloudRenderData != null )
                cloudRenderData.SetMaterialParameter(mat);
        }



    }
}
