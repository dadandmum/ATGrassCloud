
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace ATGrassCloud
{
    public class ATTerrainPreRenderPass: ScriptableRenderPass
    {
        private ATTerrainRenderData data;

        private ATTerrainRenderer renderer;


        public ATTerrainPreRenderPass(ATTerrainRenderData data, ATTerrainRenderer renderer)
        {
            this.data = data;
            this.renderer = renderer;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if ( renderer == null )
            {
                return;
            }
            renderer.SetupPrepass(cmd, ref renderingData);
        }


        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ( renderer == null )
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("[AT] Terrain Pre Render Pass");

            renderer.PreRender(context, ref renderingData, cmd);
            
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

        override public void FrameCleanup(CommandBuffer cmd)
        {
            base.FrameCleanup(cmd);
        }

        public void Dispose()
        {

        }


    }
}
