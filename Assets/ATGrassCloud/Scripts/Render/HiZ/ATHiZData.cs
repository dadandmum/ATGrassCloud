
using Sirenix.OdinInspector;
using UnityEngine;


namespace ATGrassCloud
{
    [CreateAssetMenu(fileName = "ATHiZData", menuName = "ATGrassCloud/HiZData")]
    public class ATHiZData : ScriptableObject
    {
        public enum DepthGenerationType
        {
            Default,
            CustomDepthPrepass,
            LastFrameDepth,
        }

        [TabGroup("Settings")]
        public DepthGenerationType depthGenerationType = DepthGenerationType.CustomDepthPrepass;


        [TabGroup("Settings")]
        public int stopHiZLevel = 4;

        [TabGroup("Settings")]
        public LayerMask layerMask;


        [TabGroup("Ref")]
        public Shader hiZShader;

        [TabGroup("Ref")]
        public Shader copyDepthShader;

        
           
    }
}