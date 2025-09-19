using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ATGrassCloud
{
    [CreateAssetMenu(fileName = "ATCloudRenderData", menuName = "ATGrassCloud/Cloud Render Data" , order = 100)]

    public class ATCloudRenderData : ScriptableObject
    {



        [TabGroup("Shape")]
        [MinValue(0)]
        public float cloudDensityMultiplier = 1.0f;


        [TabGroup("Shape")]
        [MinValue(0)]        
        public float cloudDensityByDistance = 0.5f;

        [TabGroup("Shape")]
        [MinValue(0)]
        public float cloudDensityMax = 10.0f;

        [TabGroup("Shape")]
        public float cloudVolumeOffset;
        [TabGroup("Shape")]
        public float cloudDensityOffset;
        
        [TabGroup("Noise")]
        public float detailNoiseScale;
        [TabGroup("Noise")]
        public float detailNoiseMultiplier;
        [TabGroup("Noise")]
        public Vector4 detailNoiseWeights = new Vector4( 10.0f, 2.0f, 0.5f, 1.0f);

        [TabGroup("Noise")]
        public Vector3 noiseSpeed = Vector3.zero;


        [TabGroup("RayMarch")]
        public int MaxRaymarchSteps = 64;

        [TabGroup("RayMarch")]
        public float raymarchRange = 10f;
        [TabGroup("RayMarch")]
        public float raymarchNoiseOffset = 10f;

        public float GetRaymarchStep() {
            return raymarchRange / MaxRaymarchSteps;
        }

        public Texture2D blueNoise;

        public void SetMaterialParameter(Material material) {

            material.SetFloat("_DetailNoiseScale", detailNoiseScale);
            material.SetFloat("_DetailNoiseMultiplier", detailNoiseMultiplier);
            material.SetVector("_DetailNoiseWeights", detailNoiseWeights);
            material.SetVector("_NoiseVelocity", noiseSpeed);

            material.SetFloat("_CloudDensityMultiplier", cloudDensityMultiplier);
            material.SetFloat("_CloudDensityByDistance", cloudDensityByDistance);
            material.SetFloat("_CloudDensityMax", cloudDensityMax);
            material.SetFloat("_CloudVolumeOffset", cloudVolumeOffset);
            material.SetFloat("_CloudDensityOffset", cloudDensityOffset);

            material.SetFloat("_MaxRaymarchSteps", MaxRaymarchSteps);
            material.SetFloat("_RaymarchRange", raymarchRange);
            material.SetFloat("_RaymarchStep", GetRaymarchStep());
            material.SetFloat("_RaymarchNoiseOffset", raymarchNoiseOffset);
            material.SetTexture("_BlueNoise", blueNoise);

        }

    }
}
