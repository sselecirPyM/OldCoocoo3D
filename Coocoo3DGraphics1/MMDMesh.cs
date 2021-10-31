using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics
{
    public class MMDMesh
    {
        public ID3D12Resource vertexBuffer;
        public ID3D12Resource indexBuffer;

        public int m_indexCount;
        public int m_vertexCount;

        public VertexBufferView vertexBufferView;
        public IndexBufferView indexBufferView;

        public PrimitiveTopology primitiveTopology;

        public bool updated;

        public byte[] m_verticeData;
        public byte[] m_indexData;

        public int vertexStride = 0;

        public static MMDMesh Load1(byte[] verticeData, int[] indexData, int vertexStride, PrimitiveTopology pt)
        {
            MMDMesh mesh = new MMDMesh();
            mesh.Reload1(verticeData, indexData, vertexStride, pt);
            return mesh;
        }

        public void Reload1(byte[] verticeData, int[] indexData, int vertexStride, PrimitiveTopology pt)
        {
            this.vertexStride = vertexStride;
            primitiveTopology = pt;
            this.m_verticeData = new byte[verticeData.Length];
            Array.Copy(verticeData, m_verticeData, verticeData.Length);
            this.m_indexData = new byte[indexData.Length * 4];
            MemoryMarshal.Cast<int, byte>(indexData).CopyTo(this.m_indexData);
        }
        public void Reload1(byte[] verticeData, byte[] indexData, int vertexStride, PrimitiveTopology pt)
        {
            this.vertexStride = vertexStride;
            primitiveTopology = pt;
            this.m_verticeData = new byte[verticeData.Length];
            Array.Copy(verticeData, m_verticeData, verticeData.Length);
            this.m_indexData = new byte[indexData.Length];
            Array.Copy(indexData, m_indexData, indexData.Length);
        }
        public void ReloadNDCQuad()
        {
            Vector3[] positions = {
                new Vector3(-1, -1, 0),
                new Vector3(-1, 1, 0),
                new Vector3(1, -1, 0),
                new Vector3(1, 1, 0),
            };
            int[] indices =
            {
                0, 1, 2,
                2, 1, 3,
            };
            vertexStride = 12;
            m_vertexCount = 4;
            m_indexCount = 6;
            primitiveTopology = PrimitiveTopology.TriangleList;
            m_verticeData = new byte[12 * 4];
            MemoryMarshal.Cast<Vector3, byte>(positions).CopyTo(m_verticeData);
            m_indexData = new byte[4 * 6];
            MemoryMarshal.Cast<int, byte>(indices).CopyTo(m_indexData);
        }

        public int GetIndexCount()
        {
            return m_indexCount;
        }

        public int GetVertexCount()
        {
            return m_vertexCount;
        }

        public void SetIndexFormat(Format format)
        {
            indexBufferView.Format = format;
        }
    }
}
