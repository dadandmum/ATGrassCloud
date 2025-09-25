
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
