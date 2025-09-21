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
        [Range(-5f, 5f)]
        public float detailNoiseScale;
        [TabGroup("Noise")]
        [Range(-5.0f,5.0f)]
        public float detailNoiseMultiplier;
        [TabGroup("Noise")]
        [Range(-5.0f,5.0f)]
        public float detailShapeNoiseInfluence;
        [TabGroup("Noise")]
        [Range(-1.0f,1.0f)]
        public float noiseOffset;
        [TabGroup("Noise")]
        public Vector4 detailNoiseWeights = new Vector4( 10.0f, 2.0f, 0.5f, 1.0f);

        [TabGroup("Noise")]
        public Vector3 noiseSpeed = Vector3.zero;


        [TabGroup("RayMarch")]
        public int MaxRaymarchSteps = 64;
        [TabGroup("RayMarch")]
        public int MaxLightmarchSteps = 8;

        [TabGroup("RayMarch")]
        public float raymarchRange = 10f;
        
        [TabGroup("RayMarch")]
        public float lightmarchRange = 5f;
        
        [TabGroup("RayMarch")]
        public float raymarchNoiseOffset = 10f;

        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float brightness = 0.8f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float transmitThreshold=0.5f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float inAbsorption = 0.8f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float outAbsorption = 0.8f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float forwardScatter = 0.8f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float backwardScatter = 0.8f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float scatterMultiplier = 0.8f;



        public float GetRaymarchStep() {
            return raymarchRange / MaxRaymarchSteps;
        }

        public float GetLightmarchStep() {
            return lightmarchRange / MaxLightmarchSteps;
        }

        public Texture2D blueNoise;

        public void SetMaterialParameter(Material material) {

            material.SetFloat("_DetailNoiseScale", Mathf.Exp( detailNoiseScale));
            material.SetFloat("_DetailNoiseMultiplier", Mathf.Exp(detailNoiseMultiplier));
            material.SetFloat("_ShapeNoiseInfluence", detailShapeNoiseInfluence);
            material.SetFloat("_NoiseOffset", noiseOffset);
            material.SetVector("_DetailNoiseWeights", detailNoiseWeights);
            material.SetVector("_NoiseVelocity", noiseSpeed);

            material.SetFloat("_CloudDensityMultiplier", cloudDensityMultiplier);
            material.SetFloat("_CloudDensityByDistance", cloudDensityByDistance);
            material.SetFloat("_CloudDensityMax", cloudDensityMax);
            material.SetFloat("_CloudVolumeOffset", cloudVolumeOffset);
            material.SetFloat("_CloudDensityOffset", cloudDensityOffset);

            material.SetFloat("_Brightness", brightness);
            material.SetFloat("_TransmitThreshold", transmitThreshold);
            material.SetFloat("_InAbsorption", inAbsorption);
            material.SetFloat("_OutAbsorption", outAbsorption);
            material.SetFloat("_ForwardScatter", forwardScatter);
            material.SetFloat("_BackwardScatter", backwardScatter);
            material.SetFloat("_ScatterMultiplier", scatterMultiplier);

            material.SetFloat("_MaxRaymarchSteps", MaxRaymarchSteps);
            material.SetFloat("_RaymarchRange", raymarchRange);
            material.SetFloat("_RaymarchStep", GetRaymarchStep());
            material.SetFloat("_MaxLightmarchSteps", MaxLightmarchSteps);
            material.SetFloat("_LightmarchRange", lightmarchRange);
            material.SetFloat("_LightmarchStep", GetLightmarchStep());
            material.SetFloat("_RaymarchNoiseOffset", raymarchNoiseOffset);
            material.SetTexture("_BlueNoise", blueNoise);

        }

    }
}
