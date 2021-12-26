using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
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
    }
    public class MMDMesh : IDisposable
    {
        public ID3D12Resource indexBuffer;

        //public Dictionary<string, _mesh1> vertexBuffers = new Dictionary<string, _mesh1>();

        //public void AddBuffer<T>(Span<T> verticeData, string name) where T : unmanaged
        //{
        //    Span<byte> dat = MemoryMarshal.Cast<T, byte>(verticeData);
        //    byte[] verticeData1 = new byte[dat.Length];
        //    dat.CopyTo(verticeData1);
        //    var bufDef = new _mesh1();
        //    bufDef.data = verticeData1;
        //    vertexBuffers[name] = bufDef;
        //}


        public Dictionary<int, _mesh1> vtBuffers = new Dictionary<int, _mesh1>();
        public List<_mesh1> vtBuffersDisposed = new List<_mesh1>();

        public int m_indexCount;
        public int m_vertexCount;

        public IndexBufferView indexBufferView;
        public int indexActualLength;

        public byte[] m_indexData;

        public void AddBuffer<T>(Span<T> verticeData, int slot) where T : unmanaged
        {
            Span<byte> dat = MemoryMarshal.Cast<T, byte>(verticeData);
            byte[] verticeData1 = new byte[dat.Length];
            dat.CopyTo(verticeData1);
            var bufDef = new _mesh1();
            bufDef.data = verticeData1;

            vtBuffers.Add(slot, bufDef);
        }
        internal _mesh1 AddBuffer(int slot)
        {
            var bufDef = new _mesh1();
            vtBuffers.Add(slot, bufDef);
            return bufDef;
        }

        public void ReloadDontCopy(byte[] verticeData, byte[] indexData, int vertexStride)
        {
            vtBuffersDisposed.AddRange(vtBuffers.Values);
            vtBuffers.Clear();
            this.m_vertexCount = verticeData.Length / vertexStride;

            var bufDef = new _mesh1();
            bufDef.data = verticeData;
            vtBuffers.Add(0, bufDef);

            if (indexData != null)
            {
                this.m_indexData = indexData;
                m_indexCount = indexData.Length / 2;
            }
        }

        //public void Reload1(Span<byte> verticeData, Span<byte> indexData, int vertexStride)
        //{
        //    vtBuffersDisposed.AddRange(vtBuffers.Values);
        //    vtBuffers.Clear();
        //    this.m_vertexCount = verticeData.Length / vertexStride;
        //    AddBuffer<byte>(verticeData, 0);

        //    if (indexData != null)
        //    {
        //        this.m_indexData = new byte[indexData.Length];
        //        indexData.CopyTo(m_indexData);
        //        m_indexCount = indexData.Length / 2;
        //    }
        //}

        public void ReloadIndex<T>(int vertexCount, Span<T> indexData) where T : unmanaged
        {
            vtBuffersDisposed.AddRange(vtBuffers.Values);
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

        public void Dispose()
        {
            indexBuffer?.Release();
            indexBuffer = null;
        }
    }
}
