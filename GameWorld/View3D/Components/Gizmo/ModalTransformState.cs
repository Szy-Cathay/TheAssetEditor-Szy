using GameWorld.Core.Components.Selection;
using GameWorld.Core.Rendering.Geometry;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace GameWorld.Core.Components.Gizmo
{
    /// <summary>
    /// Stores the initial vertex state for modal transform operations.
    /// Based on Blender's TransData structure (iloc, irot, iscale).
    /// Used to restore state on cancel or apply deltas from original state each frame.
    /// </summary>
    public class ModalTransformState
    {
        /// <summary>
        /// The target mesh object being transformed
        /// </summary>
        public MeshObject TargetMesh { get; private set; }

        /// <summary>
        /// Initial vertex positions saved at transform start (iloc in Blender)
        /// </summary>
        private List<Vector3> _initialPositions;

        /// <summary>
        /// The selection state for weighted vertex transforms
        /// </summary>
        private readonly VertexSelectionState _vertexSelectionState;

        /// <summary>
        /// Original pivot point for this transform
        /// </summary>
        public Vector3 PivotPoint { get; private set; }

        /// <summary>
        /// Create a new state snapshot for object-level transform
        /// </summary>
        public ModalTransformState(MeshObject mesh, Vector3 pivotPoint)
        {
            TargetMesh = mesh;
            PivotPoint = pivotPoint;
            _vertexSelectionState = null;
            SaveInitialState();
        }

        /// <summary>
        /// Create a new state snapshot for vertex-level transform with weights
        /// </summary>
        public ModalTransformState(MeshObject mesh, Vector3 pivotPoint, VertexSelectionState vertexState)
        {
            TargetMesh = mesh;
            PivotPoint = pivotPoint;
            _vertexSelectionState = vertexState;
            SaveInitialState();
        }

        /// <summary>
        /// Save current vertex positions as initial state (called when transform starts)
        /// </summary>
        public void SaveInitialState()
        {
            _initialPositions = new List<Vector3>();
            for (int i = 0; i < TargetMesh.VertexCount(); i++)
            {
                _initialPositions.Add(TargetMesh.GetVertexById(i));
            }
        }

        /// <summary>
        /// Restore target mesh to initial state (called on cancel)
        /// Like Blender's restoreElement function
        /// </summary>
        public void Restore()
        {
            for (int i = 0; i < _initialPositions.Count; i++)
            {
                SetVertexPosition(i, _initialPositions[i]);
            }
            TargetMesh.RebuildVertexBuffer();
        }

        /// <summary>
        /// Apply translation to all vertices from initial state
        /// Like Blender's transdata_elem_translate: loc = iloc + delta
        /// </summary>
        public void ApplyTranslation(Vector3 delta)
        {
            if (_vertexSelectionState != null)
            {
                // Weighted vertex transform
                for (int i = 0; i < _initialPositions.Count; i++)
                {
                    if (i < _vertexSelectionState.VertexWeights.Count && _vertexSelectionState.VertexWeights[i] != 0)
                    {
                        float weight = _vertexSelectionState.VertexWeights[i];
                        Vector3 weightedDelta = delta * weight;
                        SetVertexPosition(i, _initialPositions[i] + weightedDelta);
                    }
                }
            }
            else
            {
                // Full object transform
                for (int i = 0; i < _initialPositions.Count; i++)
                {
                    SetVertexPosition(i, _initialPositions[i] + delta);
                }
            }
            TargetMesh.RebuildVertexBuffer();
        }

        /// <summary>
        /// Apply rotation from initial state around pivot
        /// Like Blender's ElementRotation
        /// </summary>
        public void ApplyRotation(Quaternion rotation, Vector3 pivot)
        {
            Matrix rotMatrix = Matrix.CreateTranslation(-pivot) *
                              Matrix.CreateFromQuaternion(rotation) *
                              Matrix.CreateTranslation(pivot);

            ApplyTransformMatrix(rotMatrix);
        }

        /// <summary>
        /// Apply scale from initial state around pivot
        /// </summary>
        public void ApplyScale(Vector3 scale, Vector3 pivot)
        {
            Matrix scaleMatrix = Matrix.CreateTranslation(-pivot) *
                                Matrix.CreateScale(scale) *
                                Matrix.CreateTranslation(pivot);

            ApplyTransformMatrix(scaleMatrix);
        }

        /// <summary>
        /// Apply uniform scale from initial state around pivot
        /// </summary>
        public void ApplyScale(float uniformScale, Vector3 pivot)
        {
            ApplyScale(new Vector3(uniformScale), pivot);
        }

        /// <summary>
        /// Apply a transform matrix to all vertices from initial state
        /// </summary>
        private void ApplyTransformMatrix(Matrix transform)
        {
            if (_vertexSelectionState != null)
            {
                // Weighted vertex transform
                for (int i = 0; i < _initialPositions.Count; i++)
                {
                    if (i < _vertexSelectionState.VertexWeights.Count && _vertexSelectionState.VertexWeights[i] != 0)
                    {
                        float weight = _vertexSelectionState.VertexWeights[i];
                        Vector3 initialPos = _initialPositions[i];
                        Vector3 fullTransform = Vector3.Transform(initialPos, transform);
                        Vector3 result = Vector3.Lerp(initialPos, fullTransform, weight);
                        SetVertexPosition(i, result);
                    }
                }
            }
            else
            {
                // Full object transform
                for (int i = 0; i < _initialPositions.Count; i++)
                {
                    SetVertexPosition(i, Vector3.Transform(_initialPositions[i], transform));
                }
            }
            TargetMesh.RebuildVertexBuffer();
        }

        /// <summary>
        /// Set vertex position by directly modifying the vertex array
        /// </summary>
        private void SetVertexPosition(int index, Vector3 position)
        {
            TargetMesh.VertexArray[index].Position = new Vector4(position, 1);
        }
    }
}
