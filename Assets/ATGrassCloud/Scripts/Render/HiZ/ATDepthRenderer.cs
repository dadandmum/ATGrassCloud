using Microsoft.SqlServer.Server;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace ATGrassCloud
{
    /// <summary>
    /// DepthRenderer is responsible for capturing the camera's depth buffer and generating 
    /// a Hierarchical Z (Hi-Z) mipmap pyramid. This Hi-Z texture can be used for efficient 
    /// GPU-based culling (e.g., grass, vegetation, or object visibility).
    /// 
    /// Features:
    /// - Copies depth from camera's depth target
    /// - Handles MSAA and platform-specific Y-flipping
    /// - Creates a mipmapped depth texture (Hi-Z buffer)
    /// - Exposes results via global shader properties
    /// 
    /// Usage: Part of a custom URP render pass, typically injected before culling or rendering.
    /// </summary>
    public class ATDepthRenderer
    {

        public ATHiZData data;

        // --- Materials ---
        public Material m_DepthCopyMaterial; // Material used to copy depth buffe
        private Material m_HiZMaterial;  // Material used to downsample depth into Hi-Z pyramid (not yet used in this script)
        
        // --- Render Textures ---
        private RTHandle m_CustomDepthRT;  // custom generated depth texture (full resolution)
        private RTHandle m_FinalDepthRT;  // Final Hi-Z texture with multiple mip levels

        private RTHandle[] m_tempCopyTex;
        public RTHandle m_DepthCopyRT;    // Final copied depth texture (RFloat format, no depth buffer)
        public RTHandle DepthCopyRT => m_DepthCopyRT;

        public static RTHandle MainCameraDepthCopyRT;

        // --- Configuration Parameters ---
        private int m_DepthTexSize;    // Size of Hi-Z texture (next power of two)
        private int m_MipmapCount;     // Number of mipmap levels in Hi-Z texture
        private int m_StopHiZLevel;    // Minimum resolution at which to stop generating mipss
        private int m_tempRTWidth, m_tempRTHeight;
        private LayerMask m_LayerMask;

        private Vector3 depthCameraPosition;
        private Matrix4x4 depthCameraViewProjection;

        private Vector3 depthCopyCameraPosiiton;
        private Matrix4x4 depthCopyCameraViewProjection;

        public static Vector3 MainCameraDepthCopyPosition;
        public static Matrix4x4 MainCameraDepthCopyViewProjection;

        // --- Shader Pass Tags ---
        private List<ShaderTagId> customDepthTagList = new List<ShaderTagId>();

        // --- Settings ---
        public bool useCustomDepthPass => data.depthGenerationType == ATHiZData.DepthGenerationType.CustomDepthPrepass;

        /// <summary>
        /// Initializes the DepthRenderer with given configuration data.
        /// </summary>
        public ATDepthRenderer( ATHiZData data )
        {
            this.data = data;
            Init();
        }

        /// <summary>
        /// Initializes materials and internal settings.
        /// </summary>
        public void Init()
        {
            if ( data == null )
                return;

            InitMaterial();

            m_StopHiZLevel = data.stopHiZLevel;
            m_LayerMask = data.layerMask;

            customDepthTagList = new List<ShaderTagId>()
            {
                new ShaderTagId("DepthOnly"),
            };

            m_tempCopyTex = new RTHandle[15];

            m_DepthCopyRT = null;
            m_CustomDepthRT = null;
            m_FinalDepthRT = null;

        }

        /// <summary>
        /// Creates materials from assigned shaders.
        /// Ensures materials are ready for rendering.
        /// </summary>
        public void InitMaterial()
        {
            if ( data.copyDepthShader != null )
                m_DepthCopyMaterial  = new Material(data.copyDepthShader);
            if ( data.hiZShader != null )
                m_HiZMaterial  = new Material(data.hiZShader);
        }

        /// <summary>
        /// Configures Hi-Z render textures based on camera resolution.
        /// Allocates m_FinalDepthRT with multiple mip levels for hierarchical depth.
        /// </summary>
        public void ConfigureHiZ( CommandBuffer comd , ref RenderingData renderingData)
        {
            if ( m_HiZMaterial == null )
                return;

            if(SystemInfo.usesReversedZBuffer){
                m_HiZMaterial.EnableKeyword("AT_REVERSE_Z");
            }else{
                m_HiZMaterial.DisableKeyword("AT_REVERSE_Z");
            }
            int depthWidth = 1;
            int depthHeight = 1;

            Camera mainCam = Camera.main;
            
            if ( mainCam != null )
            {
                depthWidth = mainCam.pixelWidth;
                depthHeight = mainCam.pixelHeight;
            }
            depthWidth = Mathf.Max(depthWidth, 1);
            depthHeight = Mathf.Max(depthHeight, 1);

            if (useCustomDepthPass)
            {
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
                    ref m_CustomDepthRT,
                    customDesc,
                    name: "_CustomDepthTex");
            }


            m_DepthTexSize = Mathf.NextPowerOfTwo(Mathf.Max(depthWidth, depthHeight));
            m_MipmapCount = Mathf.CeilToInt(Mathf.Log(m_DepthTexSize, 2)) - Mathf.CeilToInt(Mathf.Log(m_StopHiZLevel, 2));

            m_MipmapCount = Mathf.Min(m_MipmapCount, 15);

            var depthFinalDesc = new RenderTextureDescriptor();
            depthFinalDesc.width = m_DepthTexSize;
            depthFinalDesc.height = m_DepthTexSize;
            depthFinalDesc.colorFormat = RenderTextureFormat.RFloat;
            depthFinalDesc.depthBufferBits = 0; 
            depthFinalDesc.enableRandomWrite = false;
            depthFinalDesc.useMipMap = true;
            depthFinalDesc.mipCount = m_MipmapCount;
            depthFinalDesc.autoGenerateMips = false;
            depthFinalDesc.msaaSamples = 1;
            depthFinalDesc.dimension = TextureDimension.Tex2D;
            depthFinalDesc.sRGB = false;

            m_tempRTWidth = depthFinalDesc.width;
            m_tempRTHeight = depthFinalDesc.height;

            // create the RT for depth Texture 
            RenderingUtils.ReAllocateIfNeeded(
                ref m_FinalDepthRT,
                depthFinalDesc,
                name: "_DepthHiZTex");


            for ( int i = 0; i < m_MipmapCount; i++ )
            {
                var texSize = m_DepthTexSize >> i;
                    
                RenderTextureDescriptor desc = new RenderTextureDescriptor(
                    texSize,
                    texSize,
                    RenderTextureFormat.RFloat,
                    0
                );
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.useMipMap = false;

                RenderingUtils.ReAllocateIfNeeded(
                    ref m_tempCopyTex[i],
                    desc,
                    FilterMode.Point,
                    TextureWrapMode.Clamp,
                    isShadowMap: false,
                    anisoLevel: 1,
                    mipMapBias: 0f,
                    name: "_CopyDepthTex_Mip" + i);
                    
            }

            
        }

        /// <summary>
        /// Configures the depth copy render texture to match camera target.
        /// Prepares a texture to receive depth data via blit.
        /// </summary>
        public void ConfigureCopyDepth(CommandBuffer cmd, ref RenderingData renderingData , ScriptableRenderPass pass )
        {
            if ( m_DepthCopyMaterial == null )
                return;
                
            if(SystemInfo.usesReversedZBuffer){
                m_DepthCopyMaterial.EnableKeyword("AT_REVERSE_Z");
            }else{
                m_DepthCopyMaterial.DisableKeyword("AT_REVERSE_Z");
            }

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            desc.msaaSamples = 1;
            desc.colorFormat = RenderTextureFormat.RFloat;
        
            RenderingUtils.ReAllocateIfNeeded(ref m_DepthCopyRT, desc, name: "Depth_Copy");
            
            pass.ConfigureTarget(m_DepthCopyRT);
            pass.ConfigureClear(ClearFlag.All, Color.clear);
        }

        /// <summary>
        /// Renders the Hierarchical Z buffer
        /// </summary>
        public void RenderHiZ( ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd , ScriptableRenderPass pass )
        {
            if ( m_HiZMaterial == null )
                return;
            
            // set up basic data 
            Camera camera = Camera.main;
            if ( camera == null )
                camera = renderingData.cameraData.camera;

            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 projMatrix = camera.projectionMatrix;// GL.GetGPUProjectionMatrix( camera.projectionMatrix , false);
            depthCameraPosition = camera.transform.position;
            depthCameraViewProjection = viewMatrix * projMatrix;

            // Do Depth Pre Pass 
            if ( useCustomDepthPass && m_CustomDepthRT != null )
            {

                cmd.SetViewProjectionMatrices(viewMatrix, projMatrix);//Update the camera marticies

                cmd.SetRenderTarget(m_CustomDepthRT,
                    RenderBufferLoadAction.Load,
                    RenderBufferStoreAction.Store,
                    RenderBufferLoadAction.DontCare,
                    RenderBufferStoreAction.Store);
            
                cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 0));

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                using (new ProfilingScope(cmd, new ProfilingSampler("[AT] Depth Pre Pass")))
                {
                    var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                    var drawSetting = pass.CreateDrawingSettings(customDepthTagList, ref renderingData, sortFlags);
                    drawSetting.perObjectData = PerObjectData.None;
                    
                    var filterSetting = new FilteringSettings(RenderQueueRange.opaque, m_LayerMask );
                    context.DrawRenderers(renderingData.cullResults, ref drawSetting, ref filterSetting);
                }

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                cmd.SetViewProjectionMatrices(renderingData.cameraData.camera.worldToCameraMatrix, renderingData.cameraData.camera.projectionMatrix);
                cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget,BuiltinRenderTextureType.Depth);
            }

            RTHandle depthSource =  renderingData.cameraData.renderer.cameraDepthTargetHandle;
            if ( data.depthGenerationType == ATHiZData.DepthGenerationType.CustomDepthPrepass )
                depthSource = m_CustomDepthRT;
            else if ( data.depthGenerationType == ATHiZData.DepthGenerationType.LastFrameDepth)
            {
                depthSource = MainCameraDepthCopyRT;
                if ( depthSource == null )
                {
                    depthSource = m_DepthCopyRT;
                }
            }


            int dealWidth = m_DepthTexSize;
            int mipmapLevel = 0;

            RTHandle current = null;
            RTHandle prev = null;

            using (new ProfilingScope(cmd, new ProfilingSampler("[AT] HiZ Copy")))
            {
                for (int i = 0; i < m_MipmapCount; i++)
                {
                    mipmapLevel = i;
                    current = m_tempCopyTex[mipmapLevel];

                    if (prev == null)
                    {
                        cmd.Blit(depthSource, current);
                    }
                    else
                    {
                        cmd.Blit(prev, current, m_HiZMaterial);
                    }

                    cmd.CopyTexture(current, 0, 0, m_FinalDepthRT, 0, mipmapLevel);

                    prev = current;
                }
            }

            SetupGlobalHiZ(cmd , ref renderingData);
        }

        public void SetupGlobalHiZ(CommandBuffer cmd , ref RenderingData renderingData)
        {
            cmd.SetGlobalInt(GLOBAL_DEPTH_MIP_MAP_COUNT_ID, m_MipmapCount);
            cmd.SetGlobalInt(GLOBAL_HIZ_SIZE_ID, m_DepthTexSize);
            cmd.SetGlobalTexture(GLOBAL_DEPTH_TEXTURE_ID, m_FinalDepthRT);

            if ( data.depthGenerationType == ATHiZData.DepthGenerationType.CustomDepthPrepass)
            {
                cmd.SetGlobalVector(GLOBAL_DEPTH_CAMERA_POSITION, depthCameraPosition);
                cmd.SetGlobalMatrix(GLOBAL_DEPTH_CAMERA_VIEW_PROJECTION, depthCameraViewProjection);
            }else if ( data.depthGenerationType == ATHiZData.DepthGenerationType.LastFrameDepth)
            {
                cmd.SetGlobalVector(GLOBAL_DEPTH_CAMERA_POSITION, MainCameraDepthCopyPosition);
                cmd.SetGlobalMatrix(GLOBAL_DEPTH_CAMERA_VIEW_PROJECTION, MainCameraDepthCopyViewProjection);
                
            }else{
                cmd.SetGlobalVector(GLOBAL_DEPTH_CAMERA_POSITION, depthCameraPosition);
                cmd.SetGlobalMatrix(GLOBAL_DEPTH_CAMERA_VIEW_PROJECTION, depthCameraViewProjection);

            }

        }

        /// <summary>
        /// Renders the depth copy pass: copies camera depth into m_DepthCopyRT.
        /// Handles MSAA and Y-flipping for correct sampling across platforms.
        /// </summary>
        public void RenderCopyDepth( ScriptableRenderContext context, ref RenderingData renderingData , CommandBuffer cmd )
        {
            if ( m_DepthCopyMaterial == null || m_DepthCopyRT == null )
                return;

            var source = renderingData.cameraData.renderer.cameraDepthTargetHandle;
            
            var cameraData = renderingData.cameraData;
            bool yflip = cameraData.IsHandleYFlipped(source) != cameraData.IsHandleYFlipped(m_DepthCopyRT); 
            Vector2 viewportScale = source.useScaling ? new Vector2(source.rtHandleProperties.rtHandleScale.x, source.rtHandleProperties.rtHandleScale.y) : Vector2.one;
            Vector4 scaleBias = yflip ? new Vector4(viewportScale.x, -viewportScale.y, 0, viewportScale.y) : new Vector4(viewportScale.x, viewportScale.y, 0, 0);


            int msaaSamples = renderingData.cameraData.cameraTargetDescriptor.msaaSamples;

            switch (msaaSamples)
            {
                case 8:
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                    break;

                case 4:
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                    break;

                case 2:
                    cmd.EnableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                    break;

                // MSAA disabled, auto resolve supported or ms textures not supported
                default:
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa2);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa4);
                    cmd.DisableShaderKeyword(ShaderKeywordStrings.DepthMsaa8);
                    break;
            }
            cmd.DisableShaderKeyword("_OUTPUT_DEPTH");

            m_DepthCopyMaterial.SetVector(BLIT_SCALE_BIAS_ID, scaleBias);

            cmd.SetGlobalTexture(CAMERA_DEPTH_ATTACHMENT_ID, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            cmd.Blit(source, m_DepthCopyRT, m_DepthCopyMaterial );

            cmd.SetGlobalTexture(GLOBAL_DEPTH_FULL_TEX_ID, m_DepthCopyRT);
            cmd.SetGlobalVector(GLOBAL_DEPTH_FULL_SIZE_ID, new Vector4(renderingData.cameraData.cameraTargetDescriptor.width, renderingData.cameraData.cameraTargetDescriptor.height, 0, 0));


            if (renderingData.cameraData.cameraType == CameraType.Game)
            {
                MainCameraDepthCopyRT = m_DepthCopyRT;
                MainCameraDepthCopyPosition = renderingData.cameraData.camera.transform.position;

                var projMatrix = GL.GetGPUProjectionMatrix(renderingData.cameraData.camera.projectionMatrix,true);
                MainCameraDepthCopyViewProjection = projMatrix * renderingData.cameraData.camera.worldToCameraMatrix;
             
            }
        }


        public void Dispose()
        {
            Debug.Log("Dispose in Depth Renderer");

            m_FinalDepthRT?.Release();
            m_DepthCopyRT?.Release();
            m_CustomDepthRT?.Release();

            if (m_tempCopyTex != null)
            {
                for (int i = 0; i < m_tempCopyTex.Length; i++)
                {
                    m_tempCopyTex[i]?.Release();
                    m_tempCopyTex[i] = null;

                }
            }

        }

        // --- Global Shader Property IDs ---
        static public int BLIT_SCALE_BIAS_ID =                      Shader.PropertyToID("_BlitScaleBias");
        static public int CAMERA_DEPTH_ATTACHMENT_ID =              Shader.PropertyToID("_CameraDepthAttachment");
        static public int GLOBAL_DEPTH_FULL_TEX_ID =                Shader.PropertyToID("_DepthFullTex");
        static public int GLOBAL_DEPTH_FULL_SIZE_ID =               Shader.PropertyToID("_DepthFullSize");
        public static int GLOBAL_DEPTH_TEXTURE_ID =                 Shader.PropertyToID("_DepthHiZTex");    
        public static int GLOBAL_DEPTH_MIP_MAP_COUNT_ID =           Shader.PropertyToID("_DepthHiZMipmapCount");
        public static int GLOBAL_HIZ_SIZE_ID =                      Shader.PropertyToID("_DepthHiZSize");
        public static int GLOBAL_DEPTH_CAMERA_POSITION =            Shader.PropertyToID("_DepthHiZCameraPosition");
        public static int GLOBAL_DEPTH_CAMERA_VIEW_PROJECTION =     Shader.PropertyToID("_DepthHiZ_VP");


    }
}