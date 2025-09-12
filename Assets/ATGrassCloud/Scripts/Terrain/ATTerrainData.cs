using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ATGrassCloud
{

    [CreateAssetMenu(fileName = "ATTerrainData", menuName = "ATGrassCloud/Terrain Data")]
    public class ATTerrainData : ScriptableObject
    {
        public Texture heightMap;
        public Texture grassTypeMap;
        public Texture grassColorMap;

    }

}