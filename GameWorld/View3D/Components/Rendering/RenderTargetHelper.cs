using Microsoft.Xna.Framework.Graphics;

namespace GameWorld.Core.Components.Rendering
{
    internal class RenderTargetHelper
    {
        /// <summary>
        /// MSAA sample count for wireframe anti-aliasing. 4x is universally supported.
        /// MonoGame automatically clamps to GPU's maximum supported value.
        /// </summary>
        private const int TargetMsaaSampleCount = 4;

        public static RenderTarget2D GetRenderTarget(GraphicsDevice device, RenderTarget2D existingRenderTarget, bool enableMsaa = false)
        {
            var width = device.Viewport.Width;
            var height = device.Viewport.Height;
            var msaaCount = enableMsaa ? TargetMsaaSampleCount : 0;

            if (existingRenderTarget == null)
            {
                return new RenderTarget2D(device, width, height, false,
                    SurfaceFormat.Color, DepthFormat.Depth24, msaaCount,
                    RenderTargetUsage.DiscardContents);
            }

            if (existingRenderTarget.Width == width && existingRenderTarget.Height == height
                && existingRenderTarget.MultiSampleCount == msaaCount)
                return existingRenderTarget;

            existingRenderTarget.Dispose();
            return new RenderTarget2D(device, width, height, false,
                SurfaceFormat.Color, DepthFormat.Depth24, msaaCount,
                RenderTargetUsage.DiscardContents);
        }
    }
}