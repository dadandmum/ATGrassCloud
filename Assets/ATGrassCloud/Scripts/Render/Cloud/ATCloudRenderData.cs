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
        [Range(0.01f,1.0f)]
        public float cloudDensityMultiplier = 1.0f;


        [TabGroup("Shape")]
        [MinValue(0)]        
        public float cloudDensityByDistance = 0.5f;

        [TabGroup("Shape")]
        [MinValue(0)]
        [Range(1.0f, 5.0f )]
        public float cloudDensityMax = 10.0f;

        [TabGroup("Shape")]
        [Range(1.0f, 100f)]
        public float cloudDensityScaleRate;
        [TabGroup("Shape")]
        public float cloudDensityOffset;
        
        [TabGroup("Noise")]
        [Range(0, 1f)]
        [OnValueChanged("UpdateDetailSize")]
        public float detailNoiseScale;
        [TabGroup("Noise")]
        [ReadOnly]
        public float detailNoiseSize;
        public void UpdateDetailSize()
        {
            float size = Mathf.Exp( ( detailNoiseScale -0.5f ) * 10.0f )* 0.05f;
            detailNoiseSize = 1.0f / size;
        }

        [TabGroup("Noise")]
        [Range(-5.0f,5.0f)]
        public float detailNoiseMultiplier;
        [TabGroup("Noise")]
        [Range(0.001f, 5.0f)]
        public float detailShapeNoiseInfluenceExtend;
        [TabGroup("Noise")]
        [Range(0.001f, 5.0f)]
        public float detailShapeNoiseInfluenceFade;
        [TabGroup("Noise")]
        [Range(-1.0f,1.0f)]
        public float noiseOffset;
        [TabGroup("Noise")]
        public Vector4 detailNoiseWeights = new Vector4( 10.0f, 2.0f, 0.5f, 1.0f);

        [TabGroup("Noise")]
        public Vector3 noiseSpeed = Vector3.zero;

        [Header("RayMarch")]
        [TabGroup("RayMarch")]
        [OnValueChanged("UpdateRaymarchStep")]
        [Range(1,256)]
        public int MaxRaymarchStepCount = 64;
        [TabGroup("RayMarch")]
        [OnValueChanged("UpdateRaymarchStep")]
        public float raymarchRange = 20f;
        [TabGroup("RayMarch")]
        [ReadOnly]
        public float raymarchStep = 0;
        public void UpdateRaymarchStep()
        {
            raymarchStep = GetRaymarchStep();
        }
        [TabGroup("RayMarch")]
        [Range(0,10f)]
        public float densityRayRandOffset = 5f;
        [TabGroup("RayMarch")]
        public Texture2D blueNoise;

        [Header("LightMarch")]
        [TabGroup("RayMarch")]
        [OnValueChanged("UpdateLightmarchStep")]
        [Range(1,64)]
        public int MaxLightmarchStepCount = 8;
        
        [TabGroup("RayMarch")]
        [OnValueChanged("UpdateLightmarchStep")]
        public float lightmarchRange = 40f;
        [TabGroup("RayMarch")]
        [ReadOnly]
        public float lightmarchStep = 0;
        public void UpdateLightmarchStep()
        {
            lightmarchStep = GetLightmarchStep();
        }
        [TabGroup("RayMarch")]
        [Range(0,10f)]
        public float lightMarchRayRandOffset = 5f;

        [Header("ShortLightMarch")]
        [TabGroup("RayMarch")]
        public bool enableShortLightmarch = true;
        [TabGroup("RayMarch")]
        [ShowIf("enableShortLightmarch")]
        [OnValueChanged("UpdateShortLightmarchStep")]
        [Range(1,64)]
        public int MaxShortLightmarchStepCount = 8;
        [TabGroup("RayMarch")]
        [ShowIf("enableShortLightmarch")]
        [OnValueChanged("UpdateShortLightmarchStep")]
        public float shortLightmarchRange = 2f;
        [TabGroup("RayMarch")]
        [ReadOnly]
        [ShowIf("enableShortLightmarch")]
        public float shortLightmarchStep = 0;
        public void UpdateShortLightmarchStep()
        {
            shortLightmarchStep = GetShortLightmarchStep();
        }

        [Header("MultipleScattering")]
        [TabGroup("RayMarch")]
        public bool enableMultipleScattering = true;
        [TabGroup("RayMarch")]
        [ShowIf("enableMultipleScattering")]
        [OnValueChanged("UpdateMultipleScatteringStep")]
        [Range(1,32)]
        public int MaxMultipleScatteringSteps = 4;
        [TabGroup("RayMarch")]
        [ShowIf("enableMultipleScattering")]
        [OnValueChanged("UpdateMultipleScatteringStep")]
        [Range(1,9)]
        public int MaxMultipleScatteringSampleCount = 4;
    
        [TabGroup("RayMarch")]
        [ShowIf("enableMultipleScattering")]
        [OnValueChanged("UpdateMultipleScatteringStep")]
        public float multipleScatteringRange = 20f;


        [TabGroup("RayMarch")]
        [ShowIf("enableMultipleScattering")]
        [OnValueChanged("UpdateMultipleScatteringStep")]
        [ReadOnly]
        public float multipleScatteringStep = 0;
        public void UpdateMultipleScatteringStep()
        {
            multipleScatteringStep = GetMultipleScatteringStep();
        }
        [TabGroup("RayMarch")]
        [ShowIf("enableMultipleScattering")]
        [ReadOnly]
        public List<Vector4> ScatterDirs = new List<Vector4>();

        [TabGroup("RayMarch")]
        [ShowIf("enableMultipleScattering")]
        [Button]
        public void SetupScaters()
        {
            ScatterDirs = new List<Vector4>();
            for (int i = 0; i < 20; i++)
            {
                var randDir = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
                randDir = (randDir.normalized + new Vector3(0, 0, -1f)).normalized;

                ScatterDirs.Add(new Vector4(randDir.x , randDir.y, randDir.z, 0) );
            }
        }



        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float overallBrightness = 1.0f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float directLightingIntensity = 1.0f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float shortLightingIntensity = 1.0f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float multipleScatterIntensity = 0.8f;
        [Header("Density Transmit - Beer")]
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float densityTransmitBeerAbsorption = 0.4f;
        [TabGroup("Lighting")]
        [Range(0,0.1f)]
        public float densityTransmitThreshold=0f;
        [TabGroup("Lighting")]
        [Range(0,5.0f)]
        public float densityTransmitPower=1.0f;
        [Header("Light March Transmit - Beer")]
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float lightMarchTransmitBeerAbsorption = 0.5f;
        [TabGroup("Lighting")]
        [Range(0,0.1f)]
        public float lightMarchTransmitThreshold=0f;
        [TabGroup("Lighting")]
        [Range(0,5.0f)]
        public float lightmarchTransmitPower=1.0f;
        [Header("Short Light March Transmit - Beer")]
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float shortLightMarchTransmitBeerAbsorption = 0.5f;
        [TabGroup("Lighting")]
        [Range(0,0.1f)]
        public float shortLightMarchTransmitThreshold=0f;
        [TabGroup("Lighting")]
        [Range(0,5.0f)]
        public float shortLightmarchTransmitPower=1.0f;

        
        [Header("Multiple Scattering - Beer")]
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float multipleScatteringBeerAbsorption = 0.5f;
        
        [Header("Powder")]
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float powderAbsorption = 0.4f;

        [Header("HenyeyGreenstein")]
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float forwardScatter = 0.8f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float backwardScatter = 0.8f;
        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float HGScatterMultiplier = 0.8f;

        [Header("Ambient")]
        [TabGroup("Lighting")]
        public Color ambientColor = new Color(0.0f, 0.0f, 0.0f,1.0f);

        [TabGroup("Lighting")]
        [Range(0,1.0f)]
        public float ambientPower = 0.8f;




        public float GetRaymarchStep() {
            return raymarchRange / MaxRaymarchStepCount;
        }

        public float GetLightmarchStep() {
            return lightmarchRange / MaxLightmarchStepCount;
        }
        public float GetShortLightmarchStep() {
            return enableShortLightmarch ? shortLightmarchRange / MaxShortLightmarchStepCount : 0;
        }
        public float GetMultipleScatteringStep() {
            return enableMultipleScattering ? multipleScatteringRange / MaxMultipleScatteringSteps : 0;
        }


        public enum DebugMode
        {
            None = 0 ,
            RayStarDistance = 1,
            RaymarchCount   = 2,
            Transmit        = 3,
            Density         = 4,
            NoiseDensity    = 5,
            BasicLightMarch  = 6,
            ShortLightMarch  = 7,
            DirectionalLighting = 8,
            MultipleScattering = 9,
            Ambient            = 10,
            
        }
        [BoxGroup("Debug")]
        public DebugMode debugMode;
        [BoxGroup("Debug")]
        [Range(0,1.0f)]
        public float debugRate = 0.7f;

        public void SetMaterialParameter(Material material) {

            material.SetFloat("_DetailNoiseScale", Mathf.Exp( ( detailNoiseScale -0.5f ) * 10.0f ) * 0.05f);
            material.SetFloat("_DetailNoiseMultiplier", Mathf.Exp(detailNoiseMultiplier));
            material.SetFloat("_DetailShapeNoiseInfluenceExtend", detailShapeNoiseInfluenceExtend * 10.0f);
            material.SetFloat("_DetailShapeNoiseInfluenceFade", detailShapeNoiseInfluenceFade * 10.0f);
            material.SetFloat("_NoiseOffset", noiseOffset);
            material.SetVector("_DetailNoiseWeights", detailNoiseWeights);
            material.SetVector("_NoiseVelocity", noiseSpeed);

            material.SetFloat("_CloudDensityMultiplier", cloudDensityMultiplier);
            material.SetFloat("_CloudDensityByDistance", cloudDensityByDistance);
            material.SetFloat("_CloudDensityMax", cloudDensityMax);
            material.SetFloat("_CloudVolumeOffset", cloudDensityScaleRate);
            material.SetFloat("_CloudDensityOffset", cloudDensityOffset);

            material.SetFloat("_Brightness", overallBrightness);
            material.SetFloat("_DirectLightingIntensity", directLightingIntensity * 10.0f );
            material.SetFloat("_ShortLightingIntensity", shortLightingIntensity * 10.0f );
            material.SetFloat("_MultipleScatterIntensity", multipleScatterIntensity * 10.0f );

            material.SetFloat("_InTransmitThreshold", densityTransmitThreshold);
            material.SetFloat("_InAbsorption", Mathf.Exp( ( 0.5f - densityTransmitBeerAbsorption) * 10.0f ) * 0.1f );
            material.SetFloat("_InTransmitPower", densityTransmitPower);
            material.SetFloat("_OutAbsorption", Mathf.Exp( ( 0.5f - lightMarchTransmitBeerAbsorption) * 10.0f ) * 0.1f );            
            material.SetFloat("_OutTransmitPower", lightmarchTransmitPower);
            material.SetFloat("_OutTransmitThreshold", lightMarchTransmitThreshold);
            material.SetFloat("_ShortOutTransmitThreshold", shortLightMarchTransmitThreshold);
            material.SetFloat("_ShortOutTransmitPower", shortLightmarchTransmitPower);
            material.SetFloat("_ShortOutAbsorption", Mathf.Exp( ( 0.5f - shortLightMarchTransmitBeerAbsorption ) * 10.0f ) * 0.1f );
            material.SetFloat("_MSBeerAbsorption", Mathf.Exp( ( 0.5f - multipleScatteringBeerAbsorption ) * 10.0f ) * 0.1f );

            material.SetFloat("_PowderAbsorption", powderAbsorption);
            material.SetFloat("_ForwardScatter", forwardScatter);
            material.SetFloat("_BackwardScatter", backwardScatter);
            material.SetFloat("_HGScatterMultiplier", HGScatterMultiplier);
            
            material.SetFloat("_MaxRaymarchStepCount", MaxRaymarchStepCount);
            material.SetFloat("_RaymarchRange", raymarchRange);
            material.SetFloat("_RaymarchStep", GetRaymarchStep());
            material.SetFloat("_RaymarchNoiseOffset", densityRayRandOffset);
            UpdateRaymarchStep();
            material.SetFloat("_MaxLightmarchStepCount", MaxLightmarchStepCount);
            material.SetFloat("_LightmarchRange", lightmarchRange);
            material.SetFloat("_LightmarchStep", GetLightmarchStep());
            material.SetFloat("_LightmarchNoiseOffset", lightMarchRayRandOffset);
            UpdateLightmarchStep();
            material.SetFloat("_MaxShortLightmarchStepCount", enableShortLightmarch ? MaxShortLightmarchStepCount : 0);
            material.SetFloat("_ShortLightmarchRange", shortLightmarchRange);
            material.SetFloat("_ShortLightmarchStep", GetShortLightmarchStep());
            UpdateShortLightmarchStep();
            material.SetFloat("_MaxMultipleScatteringStepCount", enableMultipleScattering ? MaxMultipleScatteringSteps : 0);
            material.SetFloat("_MaxMultipleScatteringSampleCount", MaxMultipleScatteringSampleCount);
            material.SetFloat("_MultipleScatteringRange", multipleScatteringRange);
            material.SetFloat("_MultipleScatteringStep", GetMultipleScatteringStep());
            UpdateMultipleScatteringStep();
            material.SetTexture("_BlueNoise", blueNoise);

            material.SetColor("_AmbientColor", ambientColor);
            material.SetFloat("_AmbientPower", ambientPower);

            if ( ScatterDirs == null || ScatterDirs.Count < MaxMultipleScatteringSampleCount)
            {
                SetupScaters();
            }
            material.SetVectorArray("_ScatterDirs", ScatterDirs);


            material.SetFloat("_DebugRate", debugRate);
            material.SetInt("_DebugMode" , (int)debugMode);
            if ( debugMode != DebugMode.None)
            {
                material.EnableKeyword("_DEBUG_CLOUD");
            }else{
                material.DisableKeyword("_DEBUG_CLOUD");
            }

        }


        [Button]
        public void SaveAsset()
        {

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

#endif
        }
    }
}
