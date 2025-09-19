using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ATGrassCloud
{
    [CreateAssetMenu(fileName = "ATGrassData", menuName = "ATGrassCloud/Grass Data" , order = 80)]

    public class ATGrassData : ScriptableObject
    {        
        public bool generateHeightMat;
        [ShowIf("generateHeightMat")]

        public Material heightMapMat;
        public ComputeShader computeShader;

        [InlineEditor]
        public List<ATGrassCascadeData> cascadeDataList;

        [BoxGroup("Debug")]
        [OnValueChanged("UpdateDebug")]
        public bool debugCascade = false;

        public void UpdateDebug()
        {
            for (int i = 0; i < cascadeDataList.Count; i++)
            {
                cascadeDataList[i].debugCascade = debugCascade;
            }

        }
    }
}
