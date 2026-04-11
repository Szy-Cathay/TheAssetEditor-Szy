using GameWorld.Core.Utility;
using Microsoft.Xna.Framework.Graphics;

namespace GameWorld.Core.Rendering.Geometry
{
    public interface IGraphicsCardGeometry
    {
        IndexBuffer IndexBuffer { get; }
        VertexBuffer VertexBuffer { get; }

        void RebuildIndexBuffer(ushort[] indexList);
        void RebuildVertexBuffer(VertexPositionNormalTextureCustom[] vertArray, VertexDeclaration vertexDeclaration);
        void RebuildVertexBufferPartial(VertexPositionNormalTextureCustom[] vertArray, int startIndex, int count, VertexDeclaration vertexDeclaration, int vertexStride);

        IGraphicsCardGeometry Clone();
        void Dispose();
    }

    public class GraphicsCardGeometry : IGraphicsCardGeometry
    {
        private readonly GraphicsDevice Device;
        public VertexBuffer VertexBuffer { get; private set; }
        public IndexBuffer IndexBuffer { get; private set; }

        public GraphicsCardGeometry(GraphicsDevice device)
        {
            Device = device;
        }

        public void RebuildIndexBuffer(ushort[] indexList)
        {
            if (indexList.Length == 0)
            {
                if (IndexBuffer != null)
                {
                    IndexBuffer.Dispose();
                    IndexBuffer = null;
                }
                return;
            }

            // Reuse existing buffer if large enough
            if (IndexBuffer != null && IndexBuffer.IndexCount >= indexList.Length)
            {
                IndexBuffer.SetData(indexList);
                return;
            }

            // Only dispose+recreate if buffer is too small or null
            if (IndexBuffer != null)
                IndexBuffer.Dispose();

            IndexBuffer = new IndexBuffer(Device, typeof(ushort), indexList.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indexList);
        }

        public virtual void RebuildVertexBuffer(VertexPositionNormalTextureCustom[] vertArray, VertexDeclaration vertexDeclaration)
        {
            if (vertArray.Length == 0)
            {
                if (VertexBuffer != null)
                {
                    VertexBuffer.Dispose();
                    VertexBuffer = null;
                }
                return;
            }

            // Reuse existing buffer if large enough
            if (VertexBuffer != null && VertexBuffer.VertexCount >= vertArray.Length)
            {
                VertexBuffer.SetData(vertArray);
                return;
            }

            // Only dispose+recreate if buffer is too small or null
            if (VertexBuffer != null)
                VertexBuffer.Dispose();

            VertexBuffer = new VertexBuffer(Device, vertexDeclaration, vertArray.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(vertArray);
        }

        public virtual void RebuildVertexBufferPartial(VertexPositionNormalTextureCustom[] vertArray, int startIndex, int count, VertexDeclaration vertexDeclaration, int vertexStride)
        {
            if (VertexBuffer == null || startIndex < 0 || count <= 0)
                return;
            int offsetInBytes = startIndex * vertexStride;
            VertexBuffer.SetData(offsetInBytes, vertArray, startIndex, count, vertexStride);
        }

        public IGraphicsCardGeometry Clone()
        {
            return new GraphicsCardGeometry(Device);
        }

        public void Dispose()
        {
            if (IndexBuffer != null)
                IndexBuffer.Dispose();
            if (VertexBuffer != null)
                VertexBuffer.Dispose();
        }
    }

    public interface IGeometryGraphicsContextFactory
    {
        IGraphicsCardGeometry Create();
    }
    public class GeometryGraphicsContextFactory : IGeometryGraphicsContextFactory
    {
        private readonly IDeviceResolver _deviceResolverComponent;

        public GeometryGraphicsContextFactory(IDeviceResolver deviceResolverComponent)
        {
            _deviceResolverComponent = deviceResolverComponent;
        }

        public IGraphicsCardGeometry Create()
        {
            return new GraphicsCardGeometry(_deviceResolverComponent.Device);
        }
    }






}
