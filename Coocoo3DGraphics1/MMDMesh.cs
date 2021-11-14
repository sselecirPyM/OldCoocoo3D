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
    public class _mesh1
    {
        public ID3D12Resource vertex;
        public VertexBufferView vertexBufferView;
        public int actualLength;
        public byte[] data;
        public int slot;
    }
    public class MMDMesh
    {
        public ID3D12Resource indexBuffer;

        public List<_mesh1> vtBuffers = new List<_mesh1>();
        public List<_mesh1> vtBuffersDisposed = new List<_mesh1>();

        public int m_indexCount;
        public int m_vertexCount;

        public IndexBufferView indexBufferView;
        public int indexActualLength;

        public bool updated;

        public byte[] m_indexData;

        public static MMDMesh Load1(byte[] verticeData, int[] indexData, int vertexStride)
        {
            MMDMesh mesh = new MMDMesh();
            mesh.Reload1(verticeData, indexData, vertexStride);
            return mesh;
        }

        public void AddBuffer<T>(Span<T> verticeData, int slot) where T : unmanaged
        {
            Span<byte> dat = MemoryMarshal.Cast<T, byte>(verticeData);
            byte[] verticeData1 = new byte[dat.Length];
            dat.CopyTo(verticeData1);
            var bufDef = new _mesh1();
            bufDef.data = verticeData1;
            bufDef.slot = slot;

            vtBuffers.Add(bufDef);
        }

        internal void AddBuffer(int slot, int length)
        {
            var bufDef = new _mesh1();
            bufDef.slot = slot;
            vtBuffers.Add(bufDef);
        }

        public void Reload1(byte[] verticeData, int[] indexData, int vertexStride)
        {
            vtBuffersDisposed.AddRange(vtBuffers);
            vtBuffers.Clear();
            this.m_vertexCount = verticeData.Length / vertexStride;
            AddBuffer<byte>(verticeData, 0);

            if (indexData != null)
            {
                this.m_indexData = new byte[indexData.Length * 4];
                MemoryMarshal.Cast<int, byte>(indexData).CopyTo(this.m_indexData);
                this.m_indexCount = indexData.Length;
            }
        }
        public void Reload1(byte[] verticeData, byte[] indexData, int vertexStride)
        {
            vtBuffersDisposed.AddRange(vtBuffers);
            vtBuffers.Clear();
            this.m_vertexCount = verticeData.Length / vertexStride;
            AddBuffer<byte>(verticeData, 0);

            if (indexData != null)
            {
                this.m_indexData = new byte[indexData.Length];
                Array.Copy(indexData, m_indexData, indexData.Length);
                m_indexCount = indexData.Length;
            }
        }
        public void ReloadIndex<T>(int vertexCount, Span<T> indexData) where T : unmanaged
        {
            vtBuffersDisposed.AddRange(vtBuffers);
            vtBuffers.Clear();
            this.m_vertexCount = vertexCount;
            if (indexData != null)
            {
                Span<byte> d1 = MemoryMarshal.Cast<T, byte>(indexData);
                this.m_indexData = new byte[d1.Length];
                d1.CopyTo(this.m_indexData);
                this.m_indexCount = indexData.Length;
            }
        }
        public void ReloadNDCQuad()
        {
            this.m_vertexCount = 4;
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
            byte[] _data = new byte[12 * 4];
            MemoryMarshal.Cast<Vector3, byte>(positions).CopyTo(_data);
            AddBuffer<byte>(_data, 0);
            m_indexCount = 6;
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
