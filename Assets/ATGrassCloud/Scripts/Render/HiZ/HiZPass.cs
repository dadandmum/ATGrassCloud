using System.Collections;
using System.Collections.Generic;
using Microsoft.SqlServer.Server;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class HiZPass : ScriptableRenderPass
    {
        private int m_StopHiZLevel;
        public HiZPass(int stopHiZLevel)
        {
            m_StopHiZLevel = stopHiZLevel;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            
        }


    }

}