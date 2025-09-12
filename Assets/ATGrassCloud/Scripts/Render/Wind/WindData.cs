using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ATGrassCloud
{
    [CreateAssetMenu(fileName = "WindData", menuName = "ATGrassCloud/WindData")]
    public class WindData : ScriptableObject
    {
        public Texture2D windMap0;
        public Vector3 windDirection = Vector3.zero;
        public float windSpeed = 0.0f;

        public float windWorldRange = 30f;
        public float windWorldSnap = 5f;
        public float windWorldFade = 10f;

        public float GetWindFullRange()
        {
            return windWorldRange + windWorldFade;

        }

        public Material updateMaterial;

        public bool syncMaterial = false;
        public bool enableDebug = false;


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
    }
}
