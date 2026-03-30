using System;
using GameWorld.Core.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace GameWorld.Core.Rendering
{
    public class OutlineFilter : IDisposable
    {
        private GraphicsDevice _graphicsDevice;
        private QuadRenderer _quadRenderer;
        private Effect _outlineEffect;
        private EffectPass _outlinePass;
        private EffectParameter _screenTextureParameter;
        private EffectParameter _inverseResolutionParameter;
        private RenderTarget2D _outlineTarget;

        public OutlineFilter() { }

        public void Load(GraphicsDevice graphicsDevice, ResourceLibrary resourceLibrary, QuadRenderer quadRenderer)
        {
            _graphicsDevice = graphicsDevice;
            _quadRenderer = quadRenderer;

            _outlineEffect = resourceLibrary.LoadEffect(@"Shaders/OutlinePostProcess", ShaderTypes.OutlinePostProcess);
            _outlinePass = _outlineEffect.Techniques["Outline"].Passes[0];

            _screenTextureParameter = _outlineEffect.Parameters["ScreenTexture"];
            _inverseResolutionParameter = _outlineEffect.Parameters["InverseResolution"];

            // Outline color: orange (1.0, 0.5, 0.0)
            var colorParam = _outlineEffect.Parameters["OutlineColor"];
            colorParam?.SetValue(new Vector3(1.0f, 0.5f, 0.0f));
        }

        public void Draw(RenderTarget2D selectionMask, int screenWidth, int screenHeight)
        {
            if (selectionMask == null)
                return;

            // Ensure outline target matches screen size
            if (_outlineTarget == null || _outlineTarget.Width != screenWidth || _outlineTarget.Height != screenHeight)
            {
                _outlineTarget?.Dispose();
                _outlineTarget = new RenderTarget2D(_graphicsDevice, screenWidth, screenHeight, false, SurfaceFormat.Color, DepthFormat.None);
            }

            // Run outline post-process: edge detection on selection mask
            _graphicsDevice.SetRenderTarget(_outlineTarget);
            _graphicsDevice.Clear(Color.Transparent);

            _screenTextureParameter.SetValue(selectionMask);
            _inverseResolutionParameter.SetValue(new Vector2(1.0f / screenWidth, 1.0f / screenHeight));

            _outlinePass.Apply();
            _quadRenderer.RenderQuad(_graphicsDevice, Vector2.One * -1, Vector2.One);

            _graphicsDevice.SetRenderTarget(null);
        }

        public RenderTarget2D GetOutlineTarget() => _outlineTarget;

        public void Dispose()
        {
            _outlineTarget?.Dispose();
            _outlineTarget = null;
        }
    }
}
