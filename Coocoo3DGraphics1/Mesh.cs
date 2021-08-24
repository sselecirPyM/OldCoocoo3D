using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics1
{
    public class Mesh : IDisposable
    {
        public ID3D12Resource vertex;
        public ID3D12Resource index;
        public UnnamedInputLayout unnamedInputLayout;
        public int sizeInByte;
        public int stride;
        public int indexCount;
        public int indexSizeInByte;
        public string Name;
        public Format indexFormat;

        public void Dispose()
        {
            vertex?.Dispose();
            vertex = null;
            index?.Dispose();
            index = null;
        }
    }
}
