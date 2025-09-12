using System.Collections;
using System.Collections.Generic;
using Microsoft.SqlServer.Server;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
    {
    public class WindPrePass : ScriptableRenderPass
    {
        // RTHandle：推荐方式管理 Render Texture
        private RTHandle m_WindRT_Ping;
        private RTHandle m_WindRT_Pong;
        private RTHandle WindRT_current => m_UseFirst ? m_WindRT_Ping : m_WindRT_Pong;
        private RTHandle WindRT_next => m_UseFirst ? m_WindRT_Pong : m_WindRT_Ping;
        private bool m_UseFirst = true;

        private Material m_UpdateMaterial;
        private CameraType m_CameraType = CameraType.Game;
        private string m_ProfilerTag = "Wind Pre-Pass";


        private WindData data;


        public WindPrePass(WindData data)
        {
            if ( data == null )
            {
                return;
            }

            if ( data.updateMaterial != null )
            {
                m_UpdateMaterial = new Material( data.updateMaterial);
                m_UpdateMaterial.CopyPropertiesFromMaterial(data.updateMaterial);
                data.SetMaterialByType(m_UpdateMaterial);

            }
            this.data = data;


        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            int texSize = data.GetTextureSize();

            // 可选：根据相机分辨率缩放
            // width = (int)(desc.width * 0.25f);
            // height = (int)(desc.height * 0.25f);

            // 自动分配或重用，仅当尺寸/格式变化时重新分配
            RenderingUtils.ReAllocateIfNeeded(ref m_WindRT_Ping, new RenderTextureDescriptor(
                texSize, 
                texSize, 
                RenderTextureFormat.RGFloat,
                 0));
            RenderingUtils.ReAllocateIfNeeded(ref m_WindRT_Pong, new RenderTextureDescriptor(
                texSize, 
                texSize, 
                RenderTextureFormat.RGFloat,
                 0));

        }

        
        public static Vector3 SnapPosition( Vector3 pos  , float snapDistance )
        {
            Vector3 snappedPos = pos;
            snappedPos.x = Mathf.Round(pos.x / snapDistance) * snapDistance;
            snappedPos.z = Mathf.Round(pos.z / snapDistance) * snapDistance;
            return snappedPos;
        }

        public Vector4 GetWindPositionParams()
        {
            Camera camera = Camera.main;
            Vector3 cameraPos = camera.transform.position;
            Vector3 snappedPos = SnapPosition(cameraPos, data.windWorldSnap);
            Vector4 params4 = new Vector4(snappedPos.x, snappedPos.z, data.GetWindFullRange() , 1.0f / data.GetWindFullRange() );
            return params4;

        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (m_UpdateMaterial == null) return;

            if (renderingData.cameraData.cameraType != m_CameraType) return;

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler(m_ProfilerTag)))
            {
                if ( data.syncMaterial )
                {
                    m_UpdateMaterial.CopyPropertiesFromMaterial(data.updateMaterial);
                    data.SetMaterialByType(m_UpdateMaterial);
                }
                cmd.SetGlobalVector("_WindPositionParams", GetWindPositionParams());
                // 使用 Blit 将 current -> next，通过材质更新风场
                cmd.Blit(WindRT_current, WindRT_next, m_UpdateMaterial, 0);

                // Ping-Pong 交换
                m_UseFirst = !m_UseFirst;

                cmd.SetGlobalTexture("_WindResultTex", WindRT_current);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);

                
            }
        }


        /// <summary>
        /// 外部获取当前风场纹理
        /// </summary>
        public RTHandle GetCurrentWindTexture()
        {
            return WindRT_current;
        }


        /// <summary>
        /// 显式释放资源（如 Feature 被禁用）
        /// </summary>
        public void Dispose()
        {
            m_WindRT_Ping?.Release();
            m_WindRT_Pong?.Release();

        }
    }
}