using GameWorld.Core.Components.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameWorld.Core.Rendering.RenderItems
{
    public class EdgeQuadRenderItem : IRenderItem
    {
        public EdgeQuadInstanceMesh EdgeQuadRenderer { get; set; }
        public EdgeData[] Edges { get; set; }
        private EdgeData[] _lastUploadedEdges;
        private bool _needsUpload = true;

        public void MarkDirty() => _needsUpload = true;

        public void Draw(GraphicsDevice device, CommonShaderParameters parameters, RenderingTechnique renderingTechnique)
        {
            if (renderingTechnique != RenderingTechnique.Normal)
                return;

            if (Edges == null || Edges.Length == 0 || EdgeQuadRenderer == null)
                return;

            // Only upload to GPU when edge data changed
            if (_needsUpload || _lastUploadedEdges != Edges)
            {
                EdgeQuadRenderer.Update(Edges);
                _lastUploadedEdges = Edges;
                _needsUpload = false;
            }

            var viewportHeight = parameters.ViewportHeight > 0 ? parameters.ViewportHeight : device.Viewport.Height;
            var viewportWidth = parameters.ViewportWidth > 0 ? parameters.ViewportWidth : device.Viewport.Width;

            EdgeQuadRenderer.Draw(parameters.View, parameters.Projection, viewportHeight, viewportWidth, device);
        }
    }
}