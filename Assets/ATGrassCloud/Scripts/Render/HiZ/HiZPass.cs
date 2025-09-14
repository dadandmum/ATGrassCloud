using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.SqlServer.Server;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ATGrassCloud
{
    public class HiZPass : ScriptableRenderPass
    {
        private int m_StopHiZLevel;
        private Material m_material;


        private RTHandle m_customDepthRT;
        private RTHandle _depthDepthRT;
        private List<ShaderTagId> customDepthTagList = new List<ShaderTagId>();
        // Render Target Identifier for depth
        private RTHandle m_finalDepthRT;

        private RTHandle[] m_tempCopyTex;
        private int m_tempRTWidth, m_tempRTHeight;
        private bool m_UseDepthPrePass;
        private bool m_UseCustomDepthPass;


        private int m_DepthTexSize;
        private int m_MipmapCount;
        private LayerMask layerMask;

        public static int globalDepthTextureID =  Shader.PropertyToID("_DepthHiZTex");
        public static int globalDepthMipmapCountID =  Shader.PropertyToID("_DepthHiZMipmapCount");
        public static int globalHiZSizeID =  Shader.PropertyToID("_DepthHiZSize");
        public static int tempRTID =  Shader.PropertyToID("_TempRT");

        public static int[] copyTextureNameIDs = new int[]
        {
            Shader.PropertyToID("_TempRTCopy0"),
            Shader.PropertyToID("_TempRTCopy1"),
            Shader.PropertyToID("_TempRTCopy2"),
            Shader.PropertyToID("_TempRTCopy3"),
            Shader.PropertyToID("_TempRTCopy4"),
            Shader.PropertyToID("_TempRTCopy5"),
            Shader.PropertyToID("_TempRTCopy6"),
            Shader.PropertyToID("_TempRTCopy7"),
            Shader.PropertyToID("_TempRTCopy8"),
            Shader.PropertyToID("_TempRTCopy9"),
            Shader.PropertyToID("_TempRTCopy10"),
            Shader.PropertyToID("_TempRTCopy11"),
            Shader.PropertyToID("_TempRTCopy12"),
            Shader.PropertyToID("_TempRTCopy13"),
            Shader.PropertyToID("_TempRTCopy14"),
            Shader.PropertyToID("_TempRTCopy15"),
            Shader.PropertyToID("_TempRTCopy16"),
            Shader.PropertyToID("_TempRTCopy17"),
            Shader.PropertyToID("_TempRTCopy18"),
        };

        public HiZPass( Shader hiZShader ,int stopHiZLevel , bool useDepthPrePass, bool useCustomDepthPass, LayerMask layerMask )
        {
            m_StopHiZLevel = stopHiZLevel;
            this.layerMask = layerMask;
            m_UseDepthPrePass = useDepthPrePass;
            m_UseCustomDepthPass = useCustomDepthPass;



            if (hiZShader != null)
                m_material = new Material(hiZShader);
            else
                Debug.LogError("HiZShader is null! HiZPass will not function.");
            

            customDepthTagList = new List<ShaderTagId>()
            {
                new ShaderTagId("DepthOnly"),
            };

            m_tempCopyTex = new RTHandle[15];
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            // if ( renderingData.cameraData.cameraType != CameraType.Game)
            //     return;

            Camera camera = renderingData.cameraData.camera;

            Camera mainCam = Camera.main;
            var depthWidth = mainCam.pixelWidth;
            var depthHeight = mainCam.pixelHeight;
            depthWidth = Mathf.Max(depthWidth, 1);
            depthHeight = Mathf.Max(depthHeight, 1);

            // var customDesc = renderingData.cameraData.cameraTargetDescriptor;
            var customDesc = new RenderTextureDescriptor(
                depthWidth,
                depthHeight,
                RenderTextureFormat.RFloat,
                0);

            customDesc.depthBufferBits = 32;
            customDesc.graphicsFormat = GraphicsFormat.None;
            customDesc.colorFormat = RenderTextureFormat.RFloat;
            customDesc.depthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
            customDesc.width = depthWidth;
            customDesc.height = depthHeight;
            customDesc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref m_customDepthRT,
                customDesc,
                name: "_CustomDepthTex");

            var customDepthDesc = customDesc;
            customDepthDesc.colorFormat = RenderTextureFormat.RFloat;
            customDepthDesc.depthBufferBits = 0;
            customDepthDesc.msaaSamples = 1;

            RenderingUtils.ReAllocateIfNeeded(
                ref _depthDepthRT,
                customDepthDesc,
                name: "_depthDepthRT");
            
            // Debug.Log("Depth Desc " +
            //  "colorFormat: " + depthDesc.colorFormat + "\n" +
            //  "depthBufferBits: " + depthDesc.depthBufferBits + "\n" +
            //  "enableRandomWrite: " + depthDesc.enableRandomWrite + "\n" +
            //  "useMipMap: " + depthDesc.useMipMap + "\n" +
            //  "mipCount: " + depthDesc.mipCount + "\n" +
            //  "dimension: " + depthDesc.dimension + "\n" +
            //  "sRGB: " + depthDesc.sRGB + "\n" +
            //  "msaa: " + depthDesc.msaaSamples

            //  );

            m_DepthTexSize = Mathf.NextPowerOfTwo(Mathf.Max(depthWidth, depthHeight));
            m_MipmapCount = Mathf.CeilToInt(Mathf.Log(m_DepthTexSize, 2)) - Mathf.CeilToInt(Mathf.Log(m_StopHiZLevel, 2));


            m_MipmapCount = Mathf.Min(m_MipmapCount, 15);
            // Debug.Log("Mipmap Count " + m_MipmapCount);

            // m_MipmapCount = 3;

            // var depthFinalDesc = renderingData.cameraData.cameraTargetDescriptor;
            var depthFinalDesc = new RenderTextureDescriptor();
            depthFinalDesc.width = m_DepthTexSize;
            depthFinalDesc.height = m_DepthTexSize;
            depthFinalDesc.colorFormat = RenderTextureFormat.RFloat;
            depthFinalDesc.depthBufferBits = 0; // 建议使用 32-bit 深度
            depthFinalDesc.enableRandomWrite = false;
            depthFinalDesc.useMipMap = true;
            depthFinalDesc.mipCount = m_MipmapCount;
            depthFinalDesc.autoGenerateMips = false;
            depthFinalDesc.msaaSamples = 1;
            depthFinalDesc.dimension = TextureDimension.Tex2D;
            depthFinalDesc.sRGB = false;

            // Debug.Log("Mipmap Count" + depthDesc.mipCount + " text size " + m_DepthTexSize);


            m_tempRTWidth = depthFinalDesc.width;
            m_tempRTHeight = depthFinalDesc.height;

            // create the RT for depth Texture 
            RenderingUtils.ReAllocateIfNeeded(
                ref m_finalDepthRT,
                depthFinalDesc,
                name: "_DepthHiZTex");


            // m_tempCopyTex = new RTHandle[m_MipmapCount];
            // RenderTextureDescriptor copyDesc = depthDesc;
            // copyDesc.mipCount = 1;
            // copyDesc.useMipMap = false;

            // for (int i = 0; i < m_MipmapCount; i++)
            // {
            //     copyDesc.width = depthDesc.width >> i;
            //     copyDesc.height = depthDesc.height >> i;

            //     RenderingUtils.ReAllocateIfNeeded(
            //         ref m_tempCopyTex[i],
            //         copyDesc,
            //         name: "_DepthCopyTex_"+i);

            // }



        }

        public void SetGlobalParameters( ComputeShader computeShader , int kernel )
        {
            if ( computeShader == null )
                return;

            if ( m_finalDepthRT == null )
                return;

            
            computeShader.SetInt(globalHiZSizeID, m_DepthTexSize);
            computeShader.SetInt(globalDepthMipmapCountID, m_MipmapCount);
            computeShader.SetTexture(kernel, globalDepthTextureID, m_finalDepthRT);
        }

        

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ( m_material == null )
                return; 

            CommandBuffer cmd = CommandBufferPool.Get();

            using (new ProfilingScope(cmd, new ProfilingSampler("HiZPass")))
            {
                // how to detect if use prepass

                
                RTHandle depthRT = renderingData.cameraData.renderer.cameraDepthTargetHandle;

                // RTHandle depthRT = m_customDepthRT;
                // Do Depth Pre Pass 
                if ( m_UseCustomDepthPass)
                {
                    Camera camera = Camera.main;
                    Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
                    Matrix4x4 projMatrix = camera.projectionMatrix;// GL.GetGPUProjectionMatrix( camera.projectionMatrix , false);

                    cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);//Update the camera marticies

                    cmd.SetRenderTarget(m_customDepthRT,
                        RenderBufferLoadAction.Load,
                        RenderBufferStoreAction.Store,
                        RenderBufferLoadAction.DontCare,
                        RenderBufferStoreAction.Store);
                
                    cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSetting = CreateDrawingSettings(customDepthTagList, ref renderingData, sortFlags);
                    drawSetting.perObjectData = PerObjectData.None;
                    
                    var filterSetting = new FilteringSettings(RenderQueueRange.opaque, layerMask );
                    context.DrawRenderers(renderingData.cullResults, ref drawSetting, ref filterSetting);


                    depthRT = m_customDepthRT;

                // get current handelle
                // RTHandle current = ;

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.camera.projectionMatrix);
                    cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,BuiltinRenderTextureType.Depth);


                }


                int dealWidth = m_DepthTexSize;
                int mipmapLevel = 0;

                RTHandle current = null;
                // int currentID = copyTextureNameIDs[0];
                RTHandle prev = null;
                // int prevID = -1;

                // cmd.CopyTexture(depthRT , 0 , 0 , m_finalDepthRT, 0 , mipmapLevel);
                // cmd.GetTemporaryRT(copyTextureNameIDs[0] , 
                //     m_DepthTexSize >> 1 ,
                //     m_DepthTexSize >> 1 ,
                //     0,
                //     FilterMode.Point,
                //     RenderTextureFormat.RFloat,
                //     RenderTextureReadWrite.Linear,
                //     1
                //     );
                // cmd.Blit(depthRT,copyTextureNameIDs[0],m_material);
                // cmd.CopyTexture(depthRT , 0 , 0 , m_finalDepthRT, 0 , 1);

                // cmd.ReleaseTemporaryRT(copyTextureNameIDs[0]);

                // Debug.Log("Final MipMap" + m_finalDepthRT.rt.mipmapCount);

                while( dealWidth > m_StopHiZLevel && mipmapLevel < m_MipmapCount )
                {
                    // currentID = copyTextureNameIDs[mipmapLevel];

                    RenderTextureDescriptor desc = new RenderTextureDescriptor(
                        dealWidth,
                        dealWidth,
                        RenderTextureFormat.RFloat,
                        0
                    );
                    desc.msaaSamples = 1;
                    desc.useMipMap = false;

                    RenderingUtils.ReAllocateIfNeeded(
                        ref current,
                        desc,
                        FilterMode.Point,
                        TextureWrapMode.Clamp,
                        false,
                        1,
                        0f,
                        name: "Copy_" + mipmapLevel
                    );

                    m_tempCopyTex[mipmapLevel] = current;


                    // cmd.GetTemporaryRT(
                    //     currentID , 
                    //     dealWidth,
                    //     dealWidth ,
                    //     0,
                    //     FilterMode.Point,
                    //     RenderTextureFormat.RFloat,
                    //     RenderTextureReadWrite.Linear,
                    //     1
                    //     );
                    // get current handle;


                    // if ( prevID < 0 )
                    if ( prev == null )
                    {
                        // cmd.Blit(depthRT,currentID);
                        // cmd.CopyTexture(depthRT, 0 , 0, current , 0 , 0 );
                        cmd.Blit(depthRT,current);

                    }else {
                        // cmd.Blit(prevID , currentID , m_material);

                        cmd.Blit(prev , current , m_material);

                    }
                    // cmd.CopyTexture(currentID , 0 , 0 , m_finalDepthRT.nameID, 0 , mipmapLevel);
                    cmd.CopyTexture(current , 0 , 0 , m_finalDepthRT, 0 , mipmapLevel);

                    // if ( prevID > 0 )
                    // {
                    //     // cmd.ReleaseTemporaryRT(prevID);
                    // }

                    // prevID = currentID;

                    prev = current;
                    dealWidth /= 2;
                    mipmapLevel++;          
                }

                // cmd.ReleaseTemporaryRT(currentID);

                cmd.SetGlobalInt(globalDepthMipmapCountID, m_MipmapCount);
                cmd.SetGlobalInt(globalHiZSizeID, m_DepthTexSize);
                cmd.SetGlobalTexture(globalDepthTextureID, m_finalDepthRT);

            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);


        }

        public void Dispose()
        {
            m_finalDepthRT?.Release();
            m_customDepthRT?.Release();

            if (m_tempCopyTex != null)
            {
                for (int i = 0; i < m_tempCopyTex.Length; i++)
                {
                    m_tempCopyTex[i]?.Release();
                    m_tempCopyTex[i] = null;

                }
            }

        }



    }
}