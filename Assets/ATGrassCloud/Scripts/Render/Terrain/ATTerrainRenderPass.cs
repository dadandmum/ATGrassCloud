
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;


namespace ATGrassCloud
{
    public class ATTerrainRenderPass: ScriptableRenderPass
    {
        private ATTerrainRenderData data;

        private ATTerrainRenderer renderer;


        public ATTerrainRenderPass(ATTerrainRenderData data, ATTerrainRenderer renderer)
        {
            this.data = data;
            this.renderer = renderer;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if ( data.material == null )
            {
                return;
            }

            CommandBuffer cmd = CommandBufferPool.Get("[AT] Terrain Render Pass");

            renderer.Render(context, ref renderingData, cmd);
            
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
