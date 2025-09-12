using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ATGrassCloud
{
    [CreateAssetMenu(fileName = "ATGrassData", menuName = "ATGrassCloud/Grass Data")]
    public class ATGrassData : ScriptableObject
    {        
        public bool generateHeightMat;
        [ShowIf("generateHeightMat")]

        public Material heightMapMat;
        public ComputeShader computeShader;

        [InlineEditor]
        public List<ATGrassCascadeData> cascadeDataList;
    }
}
