using System.Collections;
using System.Collections.Generic;
using ATGrassCloud;
using Sirenix.OdinInspector;
using UnityEngine;


namespace ATGrassCloud
{
    [CreateAssetMenu(fileName = "ATCloudData", menuName = "ATGrassCloud/Cloud Data" , order = 100)]

    public class ATCloudData : ScriptableObject
    {        
        public ComputeShader computeShader;

        [InlineEditor]
        public List<ATCloudCascadeData> cascadeDataList;

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
