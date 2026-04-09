using System;
using System.Linq;
using GameWorld.Core.Components.Rendering;
using GameWorld.Core.Rendering.Geometry;
using GameWorld.Core.Rendering.RenderItems;
using GameWorld.Core.SceneNodes;
using Microsoft.Xna.Framework;

namespace GameWorld.Core.Components
{
    public class FpsComponent : BaseComponent
    {
        private int _frames;
        private int _liveFrames;
        private TimeSpan _timeElapsed;
        private readonly RenderEngineComponent _renderEngineComponent;
        private readonly SceneManager _sceneManager;

        // Cached scene statistics (updated once per second)
        private int _objectCount;
        private int _vertexCount;
        private int _faceCount;

        public FpsComponent(RenderEngineComponent renderEngineComponent, SceneManager sceneManager)
        {
            _renderEngineComponent = renderEngineComponent;
            _sceneManager = sceneManager;
        }

        public override void Update(GameTime gameTime)
        {
            _timeElapsed += gameTime.ElapsedGameTime;
            if (_timeElapsed >= TimeSpan.FromSeconds(1))
            {
                _timeElapsed -= TimeSpan.FromSeconds(1);
                _frames = _liveFrames;
                _liveFrames = 0;

                // Update scene statistics
                UpdateSceneStatistics();
            }
        }

        private void UpdateSceneStatistics()
        {
            var meshNodes = SceneNodeHelper.GetChildrenOfType<IEditableGeometry>(_sceneManager.RootNode);
            _objectCount = meshNodes.Count;
            _vertexCount = 0;
            _faceCount = 0;
            foreach (var node in meshNodes)
            {
                if (node.Geometry != null)
                {
                    _vertexCount += node.Geometry.VertexCount();
                    _faceCount += node.Geometry.IndexArray.Length / 3;
                }
            }
        }

        public override void Draw(GameTime gameTime)
        {
            _liveFrames++;

            var fpsItem = new FontRenderItem(_renderEngineComponent, $"FPS: {_frames}", new Vector2(5, 5), Color.White);
            _renderEngineComponent.AddRenderItem(RenderBuckedId.Font, fpsItem);

            var statsItem = new FontRenderItem(_renderEngineComponent, $"Objects: {_objectCount}  Verts: {_vertexCount}  Faces: {_faceCount}", new Vector2(5, 25), Color.LightGray);
            _renderEngineComponent.AddRenderItem(RenderBuckedId.Font, statsItem);
        }
    }
}
