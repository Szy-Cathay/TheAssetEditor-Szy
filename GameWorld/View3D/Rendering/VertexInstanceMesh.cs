using GameWorld.Core.Components.Selection;
using GameWorld.Core.Rendering.Geometry;
using GameWorld.Core.Services;
using GameWorld.Core.Utility;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Runtime.InteropServices;

namespace GameWorld.Core.Rendering
{
    /// <summary>
    /// Instance data for vertex point rendering.
    /// Each instance is a camera-facing quad rendered as a circular point.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct VertexPointInstanceData : IVertexType
    {
        public Vector3 InstancePosition;   // World position of the vertex
        public float InstanceScale;        // World-space scale for screen-space size
        public Vector3 InstanceColor;      // RGB color (lerped between selected/deselected)
        public float InstanceWeight;       // Selection weight (0.0 = unselected, 1.0 = selected)

        public static readonly VertexDeclaration VertexDeclaration;
        static VertexPointInstanceData()
        {
            var elements = new VertexElement[]
            {
                new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 1),
                new VertexElement(sizeof(float) * 3, VertexElementFormat.Single, VertexElementUsage.Normal, 1),
                new VertexElement(sizeof(float) * 4, VertexElementFormat.Vector3, VertexElementUsage.Normal, 2),
                new VertexElement(sizeof(float) * 7, VertexElementFormat.Single, VertexElementUsage.Normal, 3),
            };
            VertexDeclaration = new VertexDeclaration(elements);
        }

        VertexDeclaration IVertexType.VertexDeclaration => VertexDeclaration;
    }

    /// <summary>
    /// Renders edit mode vertices as camera-facing circular points with screen-space size.
    /// Based on Blender's overlay_edit_mesh_vert.glsl approach.
    /// </summary>
    public class VertexInstanceMesh : IDisposable
    {
        Effect _effect;
        VertexDeclaration _instanceVertexDeclaration;

        DynamicVertexBuffer _instanceBuffer;
        VertexBuffer _geometryBuffer;
        IndexBuffer _indexBuffer;

        VertexBufferBinding[] _bindings;
        VertexPointInstanceData[] _instanceData;

        readonly int _maxInstanceCount = 50000;
        int _currentInstanceCount;

        // Colors - EXACT Blender match: unselected = black (visible via z-bias), selected = orange
        // Blender theme: TH_VERTEX = 0x000000ff (black), TH_VERTEX_SELECT = 0xff7a00ff (orange)
        Vector3 _selectedColour = new(1.0f, 0.47f, 0.0f);           // Orange (255, 122, 0)
        Vector3 _deselectedColour = new(0.0f, 0.0f, 0.0f);          // Black (0, 0, 0)

        // Screen-space vertex size in pixels (diameter) - EXACT Blender match
        // Blender: sizes.vert = max(1.0, TH_VERTEX_SIZE * sqrt2 / 2) = ~2.12, then * 2.0 = 4.24 pixels
        public float VertexPixelSize { get; set; } = 5.5f;

        // Additional size boost for selected vertices (pixels added to diameter)
        // Blender uses same base size, but we add slight boost for visibility
        public float SelectedSizeBoost { get; set; } = 2.0f;

        // Selection threshold multiplier (selection radius = render radius * this)
        public float SelectionThresholdMultiplier { get; set; } = 2.0f;

        public VertexInstanceMesh(IDeviceResolver deviceResolverComponent, IScopedResourceLibrary resourceLibrary)
        {
            Initialize(deviceResolverComponent.Device, resourceLibrary);
        }

        void Initialize(GraphicsDevice device, IScopedResourceLibrary resourceLib)
        {
            _effect = resourceLib.GetStaticEffect(ShaderTypes.VertexPoint);

            _instanceVertexDeclaration = VertexPointInstanceData.VertexDeclaration;
            GenerateGeometry(device);
            _instanceBuffer = new DynamicVertexBuffer(device, _instanceVertexDeclaration, _maxInstanceCount, BufferUsage.WriteOnly);
            _instanceData = new VertexPointInstanceData[_maxInstanceCount];

            _bindings = new VertexBufferBinding[2];
            _bindings[0] = new VertexBufferBinding(_geometryBuffer);
            _bindings[1] = new VertexBufferBinding(_instanceBuffer, 0, 1);
        }

        /// <summary>
        /// Generate a unit quad [-0.5, 0.5] for billboard rendering.
        /// The shader will clip this to a circle.
        /// </summary>
        void GenerateGeometry(GraphicsDevice device)
        {
            // Unit quad centered at origin, with UV coordinates for circle clipping
            var vertices = new VertexPositionTexture[4];
            vertices[0] = new VertexPositionTexture(new Vector3(-0.5f, -0.5f, 0), new Vector2(0, 1));  // Bottom-left
            vertices[1] = new VertexPositionTexture(new Vector3(0.5f, -0.5f, 0), new Vector2(1, 1));   // Bottom-right
            vertices[2] = new VertexPositionTexture(new Vector3(-0.5f, 0.5f, 0), new Vector2(0, 0));   // Top-left
            vertices[3] = new VertexPositionTexture(new Vector3(0.5f, 0.5f, 0), new Vector2(1, 0));    // Top-right

            _geometryBuffer = new VertexBuffer(device, VertexPositionTexture.VertexDeclaration, 4, BufferUsage.WriteOnly);
            _geometryBuffer.SetData(vertices);

            // Two triangles forming a quad
            var indices = new int[6];
            indices[0] = 0; indices[1] = 1; indices[2] = 2;  // First triangle
            indices[3] = 1; indices[4] = 3; indices[5] = 2;  // Second triangle

            _indexBuffer = new IndexBuffer(device, typeof(int), 6, BufferUsage.WriteOnly);
            _indexBuffer.SetData(indices);
        }

        /// <summary>
        /// Update instance data for all vertices.
        /// Calculates screen-space size based on camera FOV and viewport height.
        /// </summary>
        /// <param name="geo">Mesh geometry</param>
        /// <param name="modelMatrix">Model world matrix</param>
        /// <param name="cameraPos">Camera position in world space</param>
        /// <param name="cameraFov">Camera field of view in radians</param>
        /// <param name="viewportHeight">Viewport height in pixels</param>
        /// <param name="selectedVertexes">Vertex selection state with weights</param>
        public void Update(MeshObject geo, Matrix modelMatrix, Vector3 cameraPos,
            float cameraFov, float viewportHeight, VertexSelectionState selectedVertexes)
        {
            _currentInstanceCount = Math.Min(geo.VertexCount(), _maxInstanceCount);

            // Pre-calculate scale factor for screen-space size
            // Formula: worldSize = pixelSize * distance * (2 * tan(fov/2) / viewportHeight)
            float fovScale = 2.0f * MathF.Tan(cameraFov / 2.0f) / viewportHeight;

            for (var i = 0; i < _currentInstanceCount && i < _maxInstanceCount; i++)
            {
                // World position of the vertex
                var vertPos = Vector3.Transform(geo.GetVertexById(i), modelMatrix);

                // Distance from camera
                var distance = (cameraPos - vertPos).Length();

                // Color based on selection weight
                var weight = selectedVertexes.VertexWeights[i];
                var color = Vector3.Lerp(_deselectedColour, _selectedColour, weight);

                // Screen-space size in world units
                // Scale by distance to maintain constant pixel size
                // Selected vertices are larger (like Blender)
                var effectivePixelSize = VertexPixelSize + weight * SelectedSizeBoost;
                var worldScale = effectivePixelSize * distance * fovScale;

                _instanceData[i].InstancePosition = vertPos;
                _instanceData[i].InstanceScale = worldScale;
                _instanceData[i].InstanceColor = color;
                _instanceData[i].InstanceWeight = weight;
            }

            _instanceBuffer.SetData(_instanceData, 0, Math.Min(_currentInstanceCount, _maxInstanceCount), SetDataOptions.None);
        }

        /// <summary>
        /// Calculate the world-space selection threshold for a vertex.
        /// Used by IntersectionMath for ray-vertex hit testing.
        /// </summary>
        public float GetSelectionThresholdWorld(float distanceToCamera, float cameraFov, float viewportHeight)
        {
            float fovScale = 2.0f * MathF.Tan(cameraFov / 2.0f) / viewportHeight;
            // Selection radius = render radius * multiplier
            return (VertexPixelSize * 0.5f * SelectionThresholdMultiplier) * distanceToCamera * fovScale;
        }

        public void Draw(Matrix view, Matrix projection, Vector3 cameraPos, GraphicsDevice device)
        {
            _effect.CurrentTechnique = _effect.Techniques["VertexPoint"];
            _effect.Parameters["View"].SetValue(view);
            _effect.Parameters["ViewProjection"].SetValue(view * projection);
            _effect.Parameters["CameraPosition"].SetValue(cameraPos);

            // Alpha blending required for anti-aliased circle edges and outline ring transparency
            device.BlendState = BlendState.AlphaBlend;

            device.Indices = _indexBuffer;
            _effect.CurrentTechnique.Passes[0].Apply();

            device.SetVertexBuffers(_bindings);
            // Draw 2 triangles (one quad) per instance
            device.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, 4, 0, 2, _currentInstanceCount);

            device.BlendState = BlendState.Opaque;
        }

        public void Dispose()
        {
            _instanceVertexDeclaration?.Dispose();
            _instanceBuffer?.Dispose();
            _geometryBuffer?.Dispose();
            _indexBuffer?.Dispose();
        }
    }
}