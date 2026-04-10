using System;
using System.Collections.Generic;
using GameWorld.Core.Components.Rendering;
using GameWorld.Core.Rendering;
using GameWorld.Core.Rendering.Materials.Shaders;
using GameWorld.Core.Rendering.RenderItems;
using GameWorld.Core.SceneNodes;
using GameWorld.Core.Services;
using GameWorld.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shared.Core.Events;

namespace GameWorld.Core.Components.Selection
{
    public class SelectionChangedEvent
    {
        public ISelectionState NewState { get; internal set; }
    }

    public class SelectionManager : BaseComponent, IDisposable
    {
        ISelectionState _currentState;
        private readonly IEventHub _eventHub;
        private readonly RenderEngineComponent _renderEngine;
        BasicShader _wireframeEffect;
        BasicShader _selectedFacesEffect;
        BasicShader _outlineEffect;

        VertexInstanceMesh _vertexRenderer;
        float _vertexSelectionFalloff = 0;
        private readonly IScopedResourceLibrary _resourceLib;
        private readonly IDeviceResolver _deviceResolverComponent;

        public SelectionManager(IEventHub eventHub, RenderEngineComponent renderEngine, IScopedResourceLibrary resourceLib, IDeviceResolver deviceResolverComponent)
        {
            _eventHub = eventHub;
            _renderEngine = renderEngine;
            _resourceLib = resourceLib;
            _deviceResolverComponent = deviceResolverComponent;
        }

        public override void Initialize()
        {
            CreateSelectionSate(GeometrySelectionMode.Object, null, false);

            _vertexRenderer = new VertexInstanceMesh(_deviceResolverComponent, _resourceLib);

            _wireframeEffect = new BasicShader(_deviceResolverComponent.Device);
            _wireframeEffect.DiffuseColour = new Vector3(0.15f, 0.15f, 0.18f); // Dim wireframe overlay for face/vertex topology

            _selectedFacesEffect = new BasicShader(_deviceResolverComponent.Device);
            _selectedFacesEffect.DiffuseColour = new Vector3(1, 0, 0);
            _selectedFacesEffect.SpecularColour = new Vector3(1, 0, 0);
            _selectedFacesEffect.EnableDefaultLighting();

            _outlineEffect = new BasicShader(_deviceResolverComponent.Device);
            _outlineEffect.DiffuseColour = new Vector3(1.0f, 1.0f, 1.0f); // White for selection mask

            base.Initialize();
        }


        public ISelectionState CreateSelectionSate(GeometrySelectionMode mode, ISelectable selectedObj, bool sendEvent = true)
        {
            if (_currentState != null)
            {
                _currentState.Clear();
                _currentState.SelectionChanged -= SelectionManager_SelectionChanged;
            }

            switch (mode)
            {
                case GeometrySelectionMode.Object:
                    _currentState = new ObjectSelectionState();
                    break;

                case GeometrySelectionMode.Face:
                    _currentState = new FaceSelectionState();
                    break;

                case GeometrySelectionMode.Edge:
                    _currentState = new EdgeSelectionState();
                    break;

                case GeometrySelectionMode.Vertex:
                    _currentState = new VertexSelectionState(selectedObj, _vertexSelectionFalloff);
                    break;
                case GeometrySelectionMode.Bone:
                    _currentState = new BoneSelectionState(selectedObj);
                    break;

                default:
                    throw new Exception();
            }

            _currentState.SelectionChanged += SelectionManager_SelectionChanged;
            SelectionManager_SelectionChanged(_currentState, sendEvent);
            return _currentState;
        }

        public ISelectionState GetState() => _currentState;
        public State GetState<State>() where State : class, ISelectionState => _currentState as State;
        public ISelectionState GetStateCopy() => _currentState.Clone();
        public State GetStateCopy<State>() where State : class, ISelectionState => GetState<State>().Clone() as State;

        public void SetState(ISelectionState state)
        {
            if (state == null)
                return;

            if (_currentState != null)
                _currentState.SelectionChanged -= SelectionManager_SelectionChanged;

            _currentState = state;
            _currentState.SelectionChanged += SelectionManager_SelectionChanged;
            SelectionManager_SelectionChanged(_currentState, true);
        }

        private void SelectionManager_SelectionChanged(ISelectionState state, bool sendEvent)
        {
            _eventHub.Publish(new SelectionChangedEvent { NewState = state });
        }

        public override void Draw(GameTime gameTime)
        {
            var selectionState = GetState();

            if (selectionState is ObjectSelectionState objectSelectionState)
            {
                foreach (var item in objectSelectionState.CurrentSelection())
                {
                    if (item is Rmv2MeshNode mesh)
                    {
                        // Render selected mesh to outline mask (white, screen-space outline post-process handles the rest)
                        _renderEngine.AddRenderItem(RenderBuckedId.Outline, new GeometryRenderItem(mesh.Geometry, _outlineEffect, mesh.RenderMatrix));
                    }
                }
            }

            if (selectionState is FaceSelectionState selectionFaceState && selectionFaceState.RenderObject is Rmv2MeshNode meshNode)
            {
                _renderEngine.AddRenderItem(RenderBuckedId.Selection, new PartialGeometryRenderItem(meshNode.Geometry, meshNode.RenderMatrix, _selectedFacesEffect, selectionFaceState.SelectedFaces));
                _renderEngine.AddRenderItem(RenderBuckedId.Wireframe, new GeometryRenderItem(meshNode.Geometry, _wireframeEffect, meshNode.RenderMatrix));
            }

            if (selectionState is VertexSelectionState selectionVertexState && selectionVertexState.RenderObject != null)
            {
                var vertexObject = selectionVertexState.RenderObject as Rmv2MeshNode;
                _renderEngine.AddRenderItem(RenderBuckedId.Normal, new VertexRenderItem() { Node = vertexObject, ModelMatrix = vertexObject.RenderMatrix, SelectedVertices = selectionVertexState, VertexRenderer = _vertexRenderer });
                _renderEngine.AddRenderItem(RenderBuckedId.Wireframe, new GeometryRenderItem(vertexObject.Geometry, _wireframeEffect, vertexObject.RenderMatrix));

                // Draw gradient edges connected to selected vertices (Blender style)
                if (selectionVertexState.SelectedVertices.Count > 0)
                {
                    var geo = vertexObject.Geometry;
                    var matrix = vertexObject.RenderMatrix;
                    var selectedSet = new HashSet<int>(selectionVertexState.SelectedVertices);
                    var processedEdges = new HashSet<(int, int)>();
                    var weights = selectionVertexState.VertexWeights;
                    var wireframeColor = new Color(0.15f, 0.15f, 0.18f);
                    var highlightColor = Color.White;

                    for (var i = 0; i < geo.IndexArray.Length; i += 3)
                    {
                        var i0 = geo.IndexArray[i];
                        var i1 = geo.IndexArray[i + 1];
                        var i2 = geo.IndexArray[i + 2];

                        var edgeList = new[] {
                            (Math.Min(i0, i1), Math.Max(i0, i1)),
                            (Math.Min(i1, i2), Math.Max(i1, i2)),
                            (Math.Min(i0, i2), Math.Max(i0, i2))
                        };

                        foreach (var edge in edgeList)
                        {
                            if (processedEdges.Contains(edge))
                                continue;

                            var v0Selected = selectedSet.Contains(edge.Item1);
                            var v1Selected = selectedSet.Contains(edge.Item2);
                            if (!v0Selected && !v1Selected)
                                continue;

                            processedEdges.Add(edge);

                            var p0 = Vector3.Transform(geo.GetVertexById(edge.Item1), matrix);
                            var p1 = Vector3.Transform(geo.GetVertexById(edge.Item2), matrix);

                            // Gradient: lerp between wireframe color and highlight based on selection weight
                            var w0 = weights[edge.Item1];
                            var w1 = weights[edge.Item2];
                            var c0 = Color.Lerp(wireframeColor, highlightColor, w0);
                            var c1 = Color.Lerp(wireframeColor, highlightColor, w1);

                            _renderEngine.AddRenderLines(new VertexPositionColor[]
                            {
                                new VertexPositionColor(p0, c0),
                                new VertexPositionColor(p1, c1)
                            });
                        }
                    }
                }
            }

            if (selectionState is EdgeSelectionState selectionEdgeState && selectionEdgeState.RenderObject is Rmv2MeshNode edgeNode)
            {
                _renderEngine.AddRenderItem(RenderBuckedId.Wireframe, new GeometryRenderItem(edgeNode.Geometry, _wireframeEffect, edgeNode.RenderMatrix));
                // Render selected edges as highlighted line segments
                var geometry = edgeNode.Geometry;
                var matrix = edgeNode.RenderMatrix;
                foreach (var edge in selectionEdgeState.SelectedEdges)
                {
                    var p0 = Vector3.Transform(geometry.GetVertexById(edge.v0), matrix);
                    var p1 = Vector3.Transform(geometry.GetVertexById(edge.v1), matrix);
                    _renderEngine.AddRenderLines(new VertexPositionColor[]
                    {
                        new VertexPositionColor(p0, Color.Orange),
                        new VertexPositionColor(p1, Color.Orange)
                    });
                }
            }

            if (selectionState is BoneSelectionState selectionBoneState && selectionBoneState.RenderObject != null)
            {
                var sceneNode = selectionBoneState.RenderObject as Rmv2MeshNode;
                var animPlayer = sceneNode.AnimationPlayer;
                var currentFrame = animPlayer.GetCurrentAnimationFrame();
                var skeleton = selectionBoneState.Skeleton;

                if (currentFrame != null && skeleton != null)
                {
                    var bones = selectionBoneState.CurrentSelection();
                    var renderMatrix = sceneNode.RenderMatrix;
                    var parentWorld = Matrix.Identity;
                    foreach (var boneIdx in bones)
                    {
                        //var currentBoneMatrix = boneMatrix * Matrix.CreateScale(ScaleMult);
                        //var parentBoneMatrix = Skeleton.GetAnimatedWorldTranform(parentIndex) * Matrix.CreateScale(ScaleMult);
                        //_lineRenderer.AddLine(Vector3.Transform(currentBoneMatrix.Translation, parentWorld), Vector3.Transform(parentBoneMatrix.Translation, parentWorld));
                        var bone = currentFrame.GetSkeletonAnimatedWorld(skeleton, boneIdx);
                        bone.Decompose(out var _, out var _, out var trans);
                        _renderEngine.AddRenderLines(LineHelper.CreateCube(Matrix.CreateScale(0.06f) * bone * renderMatrix * parentWorld, Color.Red));
                    }
                }
            }

            base.Draw(gameTime);
        }

        public void Dispose()
        {
            _eventHub?.UnRegister(this);

            if(_currentState != null)
                _currentState.SelectionChanged -= SelectionManager_SelectionChanged;

            if (_wireframeEffect != null)
            {
                _wireframeEffect.Dispose();
                _wireframeEffect = null;
            }

            if (_selectedFacesEffect != null)
            {
                _selectedFacesEffect.Dispose();
                _selectedFacesEffect = null;
            }

            if (_outlineEffect != null)
            {
                _outlineEffect.Dispose();
                _outlineEffect = null;
            }

            if (_vertexRenderer != null)
            {
                _vertexRenderer.Dispose();
                _vertexRenderer = null;
            }

            _currentState?.Clear();
            _currentState = null;
        }

        public void UpdateVertexSelectionFallof(float newValue)
        {
            _vertexSelectionFalloff = Math.Clamp(newValue, 0, float.MaxValue);
            var vertexSelectionState = GetState<VertexSelectionState>();
            if (vertexSelectionState != null)
                vertexSelectionState.UpdateWeights(_vertexSelectionFalloff);
        }

        public float VertexSelectionFalloff => _vertexSelectionFalloff;
    }
}

